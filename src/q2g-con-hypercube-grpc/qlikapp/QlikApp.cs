namespace q2gconhypercubegrpc.Connection
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NLog;
    using Qlik.EngineAPI;
    using System.Threading;
    #endregion

    public class QlikApp
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        public QlikApp(UserParameter parameter)
        {
            if (parameter.UseDesktop)
                return;

            var domainUser = new DomainUser(parameter.UserName);
            if (domainUser == null)
                throw new Exception("The user must a DomainUser like this UserDirectory\\UserId");

            if (!WinAuth.ValidateWinCredentials(domainUser.UserId, parameter.Password))
            {
                throw new Exception("The windows credentials was not correct.");
            }
        }

        public static ConnectionConfig CreateConfig(UserParameter parameter, string app = null)
        {
            var host = "localhost";
            if (!String.IsNullOrEmpty(parameter.ConnectUri))
                host = parameter.ConnectUri;

            //localhost:4848
            var uri = new Uri("ws://172.30.1.125:9076");
            if (!parameter.UseDesktop)
                uri = new Uri($"wss://{host}:4747");

            var result = new ConnectionConfig()
            {
                ServerUri = uri,
                App = app ?? "engineData",
            };

            if(!parameter.UseDesktop)
            {
                result.Credentials.Type = QlikCredentialType.CERTIFICATE;
                result.Credentials.Value = parameter.UserName;
            }
            return result;
        }

        public List<DocListEntry> GetAllApps(ConnectionConfig config)
        {
            Connection conn = null;

            try
            {
                conn = new Connection(null, config);
                var global = conn.GetGlobelContext();
                var apps = global.GetDocListAsync().Result;
                if (apps == null)
                    return new List<DocListEntry>();
                else
                    return apps;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method {nameof(GetAllApps)} failed.");
                return new List<DocListEntry>();
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