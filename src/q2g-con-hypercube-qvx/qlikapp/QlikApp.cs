#region License
/*
Copyright (c) 2017 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace q2gconhypercubeqvx.QlikApplication
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
    #endregion

    public class QlikApp : QlikAuth, IDisposable
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
        private IHub hub { get; set; }

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
            }

            Credentials = credentials;
            Sessions = new List<QlikAppSession>();
        }
        #endregion

        #region private methods
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
                }
                else if (Credentials?.Type == QlikCredentialType.WINDOWSAUTH)
                {
                    var winAuth = Credentials as WindowsAuth;
                    LocationInfo.AsNtlmUserViaProxy(true, new NetworkCredential(winAuth?.Login, winAuth?.Password), false);
                }
                else if (Credentials?.Type == QlikCredentialType.SESSION)
                {
                    var sessionAuth = Credentials as SessionAuth;
                    LocationInfo.AsExistingSessionViaProxy(sessionAuth?.SessionId, sessionAuth?.CookieName, true, false);
                }
            }
            else
                throw new Exception("Unknown Qlik connection type.");

            hub = LocationInfo.Hub();
            return hub.GetAppList().ToList();
        }

        private QlikAppSession GetDefaultSession()
        {
            var session = new QlikAppSession(LocationInfo, Identifier, true);
            Sessions.Add(session);
            return session;
        }
        #endregion

        #region public methods
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

        public bool Reconnect(IQlikCredentials credentials, bool createDefaultSession = false)
        {
            Credentials = credentials;
            return Connect(createDefaultSession);
        }

        public bool Connect(bool createDefaultSession = false)
        {
            try
            {
                var appList = GetApps();
                var appIdentifier = appList.Where(c => c.AppId == AppId.ToString()).SingleOrDefault() ?? null;
                if (appIdentifier == null)
                    appIdentifier = appList.Where(c => c.AppName == AppName).SingleOrDefault();
                Identifier = appIdentifier ?? throw new Exception($"No App with the id {AppId.ToString()} oder name {AppName} found.");
                if (createDefaultSession)
                    GetDefaultSession();
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
                }
                else
                {
                    session.IsFree = false;
                }

                return session;
            }
        }

        public void Dispose()
        {
            hub.Dispose();
        }
        #endregion
    }

    #region helper classes
    public class QlikAppSession
    {
        public IApp CurrentApp { get; private set; }
        public QlikSelections Selections { get; private set; }
        public Guid Id { get; private set; }
        public string SessionId { get; private set; }
        public bool IsFree { get; set; }

        internal QlikAppSession(ILocation location, IAppIdentifier appIdentifier, bool createDefaultSession)
        {
            Id = Guid.NewGuid();
            if (createDefaultSession)
                CurrentApp = location.App(appIdentifier, Session.WithApp(appIdentifier, SessionType.Default), true, false);
            else
                CurrentApp = location.App(appIdentifier, Session.Random, true, false);
            SessionId = location.SessionCookie?.Split('=').ElementAtOrDefault(1) ?? null;
            Selections = new QlikSelections(CurrentApp);
            IsFree = false;
        }

        public QlikAppSession(ILocation location, IAppIdentifier appIdentifier) :
               this(location, appIdentifier, false) { }
    }
    #endregion
}
