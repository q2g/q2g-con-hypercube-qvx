namespace QlikTableConnector.QlikApplication
{
    #region Usings
    using System;
    using System.Linq;
    using System.Net;
    using Qlik.Engine;
    using Qlik.Engine.Communication;
    using System.Security.Cryptography.X509Certificates;
    using System.IO;
    using System.Collections.Generic;
    using QlikTableConnector;
    #endregion

    public class QlikApp : QlikAuth
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
        #endregion

        #region Properties & Variables
        private IQlikCredentials Credentials { get; set; }
        private Uri AppUri { get; set; }
        private bool UseSsl { get; set; }
        private string AppName { get; set; }
        private Guid AppId { get; set; }
        private ILocation LocationInfo { get; set; }
        private IAppIdentifier Identifier { get; set; }
        private object sync { get; set; }
        
        public bool IsServerConnection { get; private set; }
        public List<QlikAppSession> Sessions { get; private set; }
        public QlikAppSession FirstSession
        {
            get { return Sessions.FirstOrDefault(); }
        }
        #endregion

        #region Constructor & Init
        public QlikApp(string connectUri) : this(null, connectUri, null) { }

        public QlikApp(string appName, string connectUri, IQlikCredentials credentials)
        {
            if (String.IsNullOrEmpty(connectUri) || connectUri == "Qlik Sense Desktop")
            {
                //Qlik Sense Desktop
                AppName = appName;
                AppUri = new Uri("ws://127.0.0.1:4848");
                IsServerConnection = false;
                logger.Info($"Use Qlik Sense Desktop {AppUri.OriginalString} with name {AppName}.");
            }
            else
            {
                //Qlik Sense Server
                var resultValue = Guid.TryParse(appName, out var result) ? (Guid?)result : null;
                if (resultValue == null)
                    AppName = appName;
                else
                    AppId = resultValue.Value;

                AppUri = new Uri(connectUri);
                IsServerConnection = true;
                logger.Info($"Use Qlik Sense Server {AppUri.OriginalString} with name {AppName}.");
            }

            Credentials = credentials;
            Sessions = new List<QlikAppSession>();
        }
        #endregion

        private List<IAppIdentifier> GetApps()
        {
            LocationInfo = Location.FromUri(AppUri);
            LocationInfo.IsVersionCheckActive = false;
            if (IsServerConnection == false)
            {
                LocationInfo.AsDirectConnectionToPersonalEdition();
            }
            else if (IsServerConnection == true && UseSsl == false)
            {
                if (Credentials?.Type == QlikCredentialType.CERTIFICATE)
                {
                    var userCert = Credentials as CertificateAuth;
                    var clientCert = GetClientCertificate(userCert?.CertificatePath, userCert?.Password);
                    var certCollect = new X509Certificate2Collection(clientCert);
                    LocationInfo.AsDirectConnection(userCert?.UserDirectory, userCert?.UserId, false, false, certCollect);
                    logger.Debug($"Credential type: {Credentials.Type}");
                }
                else if (Credentials?.Type == QlikCredentialType.WINDOWSAUTH)
                {
                    var winAuth = Credentials as WindowsAuth;
                    LocationInfo.AsNtlmUserViaProxy(true, new NetworkCredential(winAuth?.Login, winAuth?.Password), false);
                    logger.Debug($"Credential type: {Credentials.Type} with User {winAuth?.Login}");
                }
                else if (Credentials?.Type == QlikCredentialType.SESSION)
                {
                    var sessionAuth = Credentials as SessionAuth;
                    LocationInfo.AsExistingSessionViaProxy(sessionAuth?.SessionId, sessionAuth?.CookieName, true, false);
                    logger.Debug($"Credential type: {Credentials.Type} with User {sessionAuth?.SessionId}");
                }
            }
            else
                throw new Exception("Unknown Qlik connection type.");

            var hub = LocationInfo.Hub();
            return hub.GetAppList().ToList();
        }

        private QlikAppSession GetDebugSession()
        {
            var session = new QlikAppSession(LocationInfo, Identifier, true);
            Sessions.Add(session);
            logger.Debug($"Create new >>Debug<< Session.");
            return session;
        }

        public List<string> GetAllApps()
        {
            try
            {
                var appList = GetApps();
                return appList.Select(s => $"{s.AppName}").ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The apps could not find correctly.");
                return new List<string>();
            }
        }

        public bool Connect(bool createDebugSession = false)
        {
            try
            {
                var appList = GetApps();
                var appIdentifier = appList.Where(c => c.AppId == AppId.ToString() || c.AppName == AppName).SingleOrDefault();
                Identifier = appIdentifier ?? throw new Exception($"No App with the id {AppId.ToString()} oder name {AppName} found.");
                if (createDebugSession)
                    GetDebugSession();
                else
                    GetFreeSession();
                return true;
            }
            catch (Exception ex)
            {
                if (IsServerConnection)
                    logger.Error(ex, $"The connection to Qlik Sense Server with app \"{AppId}\" could not be established.");
                else
                    logger.Error(ex, $"The connection to Qlik Sense Desktop with app \"{AppName}\" could not be established.");
                return false;
            }
        }

        public QlikAppSession GetFreeSession()
        {
            lock (this)
            {
                var session = Sessions.Where(s => s.IsFree == true).FirstOrDefault();
                if (session == null)
                {
                    session = new QlikAppSession(LocationInfo, Identifier);
                    Sessions.Add(session);
                    logger.Debug($"Create new Session {Sessions.Count} - ID: {session.Id}");
                }
                else
                {
                    session.IsFree = false;
                    logger.Debug($"Use Session ID: {session.Id}");
                }

                return session;
            }
        }
    }

    public class QlikAppSession
    {
        public IApp CurrentApp { get; private set; }
        public QlikSelections Selections { get; private set; }
        public Guid Id { get; private set; }
        public bool IsFree { get; set; }

        internal QlikAppSession(ILocation location, IAppIdentifier appIdentifier, bool createDebugSession)
        {
            Id = Guid.NewGuid();
            if (createDebugSession)
                CurrentApp = location.App(appIdentifier, Session.WithApp(appIdentifier, SessionType.Default), true, false);
            else
                CurrentApp = location.App(appIdentifier, Session.Random, true, false);
            Selections = new QlikSelections(CurrentApp);
            IsFree = false;
        }

        public QlikAppSession(ILocation location, IAppIdentifier appIdentifier) :
               this(location, appIdentifier, false) { }
    }
}
