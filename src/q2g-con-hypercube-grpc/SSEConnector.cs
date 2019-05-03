namespace q2gconhypercubegrpc
{
    #region Usings
    using Grpc.Core;
    using NLog;
    using Qlik.Connect;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    #endregion

    public class SseConnector
    {
        #region Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        private Server server;
        private SseEvaluator sseEvaluator;
        #endregion

        public void Start()
        {
            try
            {
                logger.Debug("Service running...");
                logger.Debug($"Start Service on Port \"50053\" with Host \"localhost");
                logger.Debug($"Server start...");

                using (sseEvaluator = new SseEvaluator())
                {
                    server = new Server()
                    {
                        Services = { Connector.BindService(sseEvaluator) },
                        Ports = { new ServerPort("0.0.0.0", 50053, ServerCredentials.Insecure) },
                    };

                    server.Start();
                    logger.Info($"gRPC listening on port 50053 on Host localhost");
                    logger.Info($"Ready...");
                }

                while (true)
                {
                    Thread.Sleep(250);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Service could not be started.");
            }
        }

        public void Stop()
        {
            try
            {
                logger.Info("Shutdown SSEDemo...");
                server?.ShutdownAsync().Wait();
                sseEvaluator.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Service could not be stoppt.");
            }
        }
    }
}
