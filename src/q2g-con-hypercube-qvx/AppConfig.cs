using Newtonsoft.Json;
using QlikTableConnector.QlikApplication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace QlikTableConnector
{
    public class AppConfig
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
        #endregion

        public static QlikApp GetQlikInstance(string appId = null)
        {
            try
            {
                var appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var configPath = Path.Combine(appDir, "web\\config.json");
                if (!File.Exists(configPath))
                    throw new Exception($"The config file {configPath} not found.");
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<ConnectorConfig>(json);

                CertificateAuth certCred = null;
                var connectUri = "Qlik Sense Desktop";
                if (!config.UseDesktop)
                {
                    var userInfo = config.UserId.Split('/');
                    certCred = new CertificateAuth(userInfo[1], userInfo[0]);
                    connectUri = config.ConnectUri;
                }

                var qlikApp = new QlikApp(appId, connectUri, certCred);
                return qlikApp;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "app connection failed");
                return null;
            }
        }
    }

    public class ConnectorConfig
    {
        public string UserId { get; set; }
        public bool UseDesktop { get; set; }
        public string ConnectUri { get; set; }
    }
}
