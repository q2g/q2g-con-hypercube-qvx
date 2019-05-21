namespace q2gconhypercubegrpc
{
    #region Usings
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.Extensions.PlatformAbstractions;
    using NLog;
    using NLog.Config;
    #endregion

    class Program
    {
        #region Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        static void Main(string[] args)
        {
            try
            {
                SetLoggerSettings("App.config");
                logger.Info("GRPC-Demo-Connector");

                var connector = new SseConnector();
                connector.Start();
                Console.WriteLine("Wait for Shutdown...");
                Console.ReadLine();
                connector.Stop();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private static void SetLoggerSettings(string configName)
        {
            var path = Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, configName);
            if (!File.Exists(path))
            {
                var root = new FileInfo(path).Directory?.Parent?.Parent?.Parent;
                var files = root.GetFiles("App.config", SearchOption.AllDirectories).ToList();
                path = files.FirstOrDefault()?.FullName;
            }

            logger.Factory.Configuration = new XmlLoggingConfiguration(path, false);
        }
    }
}
