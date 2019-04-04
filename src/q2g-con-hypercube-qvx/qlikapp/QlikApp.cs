namespace q2gconhypercubeqvx.Connection
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NLog;
    using Ser.Api;
    using Qlik.EngineAPI;
    using q2gconhypercubeqvx;
    using System.Threading;
    #endregion

    public class QlikApp
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        public QlikApp(ConnectorParameter parameter)
        {
            var domainUser = new DomainUser(parameter.UserName);
            if(domainUser == null)
                throw new Exception("The user must a DomainUser like this UserDirectory\\UserId");

            if (WinAuth.ValidateWinCredentials(domainUser.UserId, parameter.Password))
            {
                throw new Exception("The windows credentials was not correct.");
            }
        }

        public static ConnectionConfig CreateConfig(ConnectorParameter parameter, string app = null)
        {
            var host = "localhost";
            if (!String.IsNullOrEmpty(parameter.ConnectUri))
                host = parameter.ConnectUri;

            var uri = new Uri("ws://localhost:4848");
            if (!parameter.UseDesktop)
                uri = new Uri($"wss://{host}:4747");

            return new ConnectionConfig()
            {
                ServerUri = uri,
                App = app ?? "engineData",
                Credentials = new SerCredentials()
                {
                    Type = QlikCredentialType.CERTIFICATE,
                    Value = parameter.UserName,
                }
            };
        }

        public List<string> GetAllApps(ConnectionConfig config)
        {
            Connection conn = null;

            try
            {
                conn = new Connection(null, config);
                var global = conn.GetGlobelContext();
                var apps = global.GetDocListAsync().Result;
                if (apps == null)
                    return new List<string>();
                else
                    return apps.Select(s => s.qDocName).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method {nameof(GetAllApps)} failed.");
                return new List<string>();
            }
            finally
            {
                conn?.Close();
            }
        }

        public Connection CreateNewConnection(ConnectionConfig config)
        {
            try
            {
                var conn = new Connection(null, config);
                if (!conn.Connect())
                    throw new Exception("No connection to Qlik.");
                return conn;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method {nameof(CreateNewConnection)} failed.");
                return null;
            }
        }
    }
}