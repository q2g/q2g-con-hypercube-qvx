namespace q2gconhypercubegrpc.Connection
{
    #region Usings
    using System;
    using System.Linq;
    using System.Net;
    using NLog;
    using System.Security.Cryptography.X509Certificates;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using enigma;
    using System.Net.WebSockets;
    using System.Threading;
    using Qlik.EngineAPI;
    using ImpromptuInterface;
    using Q2g.HelperPem;
    using System.Net.Security;
    using System.Net.Http;
    #endregion

    #region Enumeration
    public enum QlikAppMode
    {
        DESKTOP,
        SERVER
    }
    #endregion

    public class Connection
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        public Uri ConnectUri { get; private set; }
        public ConnectionConfig Config { get; private set; }
        public Cookie ConnectCookie { get; private set; }
        public IDoc CurrentApp { get; private set; }
        public QlikAppMode Mode { get; private set; }
        public bool IsFree { get; set; } = false;
        public string Identity { get; set; } = null;
        public string ConnId { get; set; } = Guid.NewGuid().ToString();
        private bool IsSharedSession { get; set; }
        private Session SocketSession = null;
        private readonly object lockObject = new object();
        #endregion

        #region Constructor & Init
        public Connection(string identity, ConnectionConfig config)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls |
                                                   SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback += delegate (object sender, X509Certificate certificate,
                                                                                 X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                return true; // **** Always accept
            };

            Mode = QlikAppMode.SERVER;
            IsSharedSession = true;
            Config = config;
            Identity = identity;

            var connectUrl = SwitchScheme(Config.ServerUri.AbsoluteUri);
            var appurl = Uri.EscapeDataString(SenseUtilities.GetFullAppName(Config.App).TrimStart('/'));
            connectUrl = $"{connectUrl}/app/{appurl}";

            if (identity == null)
            {
                connectUrl = $"{connectUrl}/identity/{Guid.NewGuid().ToString()}";
                IsSharedSession = false;
            }
            else if (!String.IsNullOrEmpty(identity))
            {
                connectUrl = $"{connectUrl}/identity/{identity}";
            }

            ConnectUri = new Uri(connectUrl);
            logger.Info($"Create Qlik connection {ConnId} to {connectUrl} with app {Config.App} and identity {identity}.");
        }
        #endregion

        #region Private Methods
        private string SwitchScheme(string value)
        {
            value = value.Replace("http://", "ws://");
            value = value.Replace("https://", "wss://");
            return value.TrimEnd('/');
        }

        private string GetAppId(IGlobal global)
        {
            if (Guid.TryParse(Config.App, out var result))
                return Config.App;

            dynamic results = global.GetDocListAsync<JArray>().Result;
            foreach (var app in results)
            {
                if (app.qDocName.Value == Config.App)
                    return app.qDocId;
            }
            return Config.App;
        }
        #endregion

        #region Public Methods
        public static Uri BuildQrsUri(Uri connectUrl, Uri baseUrl)
        {
            var virtualProxy = baseUrl?.PathAndQuery?.Split(new char[] { '/' },
                           StringSplitOptions.RemoveEmptyEntries)?.FirstOrDefault() ?? null;
            virtualProxy = $"/{virtualProxy}";

            var qrsBuilder = new UriBuilder()
            {
                Host = connectUrl.Host,
                Path = virtualProxy
            };
            switch (connectUrl.Scheme)
            {
                case "ws":
                    qrsBuilder.Scheme = "http";
                    qrsBuilder.Port = connectUrl.Port;
                    break;
                case "wss":
                    qrsBuilder.Scheme = "https";
                    qrsBuilder.Port = connectUrl.Port;
                    break;
                default:
                    qrsBuilder.Scheme = "https";
                    break;
            }
            return qrsBuilder.Uri;
        }

        public IGlobal GetGlobelContext()
        {
            try
            {
                logger.Debug("Create global context");
                var config = new EnigmaConfigurations()
                {
                    Url = ConnectUri.AbsoluteUri,
                    CreateSocket = async (Url) =>
                    {
                        var webSocket = new ClientWebSocket();
                        webSocket.Options.Cookies = new CookieContainer();

                        var callback = ServicePointManager.ServerCertificateValidationCallback;
                        if (callback == null)
                            throw new NotImplementedException(".NET has no certificate check");

                        var credentials = Config?.Credentials ?? null;
                        var credType = Config?.Credentials?.Type ?? QlikCredentialType.NONE;
                        switch (credType)
                        {
                            case QlikCredentialType.CERTIFICATE:
                                var domainUser = new DomainUser(credentials.Value);
                                var options = new CookieConnectionOptions()
                                {
                                    CertificatePath = credentials?.Cert ?? null,
                                    HeaderName = "X-Qlik-User",
                                    HeaderValue = $"UserDirectory={domainUser.UserDirectory};UserId={domainUser.UserId}",
                                    UseCertificate = true,
                                };

                                var qlikClientCert = new X509Certificate2();
                                qlikClientCert = qlikClientCert.GetQlikClientCertificate(options.CertificatePath);
                                webSocket.Options.ClientCertificates.Add(qlikClientCert);
                                webSocket.Options.SetRequestHeader(options.HeaderName, options.HeaderValue);
                                logger.Debug($"Credential type: {credentials?.Type}");
                                break;
                            case QlikCredentialType.WINDOWSAUTH:
                                webSocket.Options.Credentials = new NetworkCredential(credentials?.Key, credentials?.Value);
                                logger.Debug($"WinAuth type: {credentials?.Type} with User {credentials?.Key}");
                                break;
                            case QlikCredentialType.SESSION:
                                logger.Debug($"Session-Cookie {credentials?.Key}={credentials?.Value}.");
                                ConnectCookie = new Cookie(credentials?.Key, credentials?.Value)
                                {
                                    Secure = true,
                                    Domain = ConnectUri.Host,
                                    Path = "/",
                                };
                                webSocket.Options.Cookies.Add(ConnectCookie);
                                logger.Debug($"Session type: {credentials?.Type} with Session {credentials?.Value}");
                                break;
                            case QlikCredentialType.NONE:
                                logger.Debug($"None type: No Authentication.");
                                // No Authentication for DESKTOP and DOCKER
                                break;
                            default:
                                throw new Exception("Unknown Qlik connection type.");
                        }
                        webSocket.Options.KeepAliveInterval = TimeSpan.FromDays(48);
                        await webSocket.ConnectAsync(new Uri(Url), CancellationToken.None);
                        return webSocket;
                    },
                };

                SocketSession = Enigma.Create(config);
                var globalTask = SocketSession.OpenAsync();
                globalTask.Wait();
                logger.Debug("Found globel context");
                return Impromptu.ActLike<IGlobal>(globalTask.Result);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "No Global context");
                return null;
            }
        }

        public bool Connect()
        {
            try
            {
                logger.Info($"Connect to: {ConnectUri.AbsoluteUri}");
                var global = GetGlobelContext();
                var task = global.IsDesktopModeAsync();
                task.Wait(2500);
                if (!task.IsCompleted)
                    throw new Exception("No connection to qlik.");
                if (task.Result)
                    Mode = QlikAppMode.DESKTOP;
                logger.Debug($"Use connection mode: {Mode}");
                if (IsSharedSession)
                {
                    try
                    {
                        CurrentApp = global.GetActiveDocAsync().Result;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "No existing shared session found. Please open the app in the browser.");
                        return false;
                    }
                }
                else
                {
                    var appName = String.Empty;
                    if (Mode == QlikAppMode.DESKTOP)
                        appName = SenseUtilities.GetFullAppName(Config.App);
                    else
                        appName = GetAppId(global);
                    logger.Debug($"Connect with app name: {appName}");
                    CurrentApp = global.OpenDocAsync(appName).Result;
                }
                logger.Debug("The Connection to Qlik was successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The connection to Qlik Sense with uri \"{ConnectUri}\" app \"{Config.App}\" could not be established.");
                return false;
            }
        }

        public void Close()
        {
            try
            {
                lock (lockObject)
                {
                    if (SocketSession != null)
                    {
                        SocketSession.CloseAsync().Wait(100);
                        SocketSession = null;
                        logger.Debug($"The connection {ConnId} - Uri {ConnectUri?.AbsoluteUri} will be released.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The connection {ConnId} - Uri {ConnectUri?.AbsoluteUri} could not release.");
            }
        }
        #endregion
    }
}