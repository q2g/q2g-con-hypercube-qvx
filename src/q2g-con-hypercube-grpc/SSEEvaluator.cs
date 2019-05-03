namespace q2gconhypercubegrpc
{
    #region Usings
    using Google.Protobuf;
    using Grpc.Core;
    using NLog;
    using Qlik.Connect;
    using SSEDemo.Connection;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using static Qlik.Connect.Connector;
    #endregion

    public class SseEvaluator : ConnectorBase, IDisposable
    {
        #region Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Variables
        private TableFunc tableFunctions;
        #endregion

        #region Constructor
        public SseEvaluator()
        {
            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;
        }
        #endregion

        #region Private Methods
        private static bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain,
                                                      SslPolicyErrors error)
        {
            //Check Server Certificate
            return true;
        }

        private ResultTable GetData(ScriptCode script, UserParameter parameter)
        {
            SSEDemo.Connection.Connection connection = null;

            try
            {
                var config = QlikApp.CreateConfig(parameter, script.AppId);
                var qlikApp = new QlikApp(parameter);
                connection = qlikApp.CreateNewConnection(config);
                if (!connection.Connect())
                    return null;

                foreach (var filter in script.Filter)
                {
                    logger.Debug($"Filter: {filter}");
                    foreach (var value in filter.Values)
                    {
                        var selection = new QlikSelections(connection.CurrentApp);
                        var result = selection.SelectValue(filter.Name, value);
                        if (result == false)
                        {
                            logger.Error($"The Dimension \"{filter.Name}\" could not found.");
                            return null;
                        }
                    }
                }

                var resultTable = tableFunctions.GetTableInfosFromApp($"Table_{script.AppId}_{script.ObjectId}", script, connection.CurrentApp);
                return resultTable.QvxTable;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The table script can not be executed.");
                return null;
            }
            finally
            {
                connection?.Close();
            }
        }

        private DataChunk BuildDataChunk(string value)
        {
            var dataChunk = new DataChunk();
            if (value == null)
            {
                dataChunk.StringBucket.Add(value);
                dataChunk.StringCodes.Add(-1);
                dataChunk.NumberCodes.Add(-1);
            }
            else
            {
                dataChunk.StringBucket.Add(value);
                dataChunk.StringCodes.Add(value.Length - 1);
                dataChunk.NumberCodes.Add(-1);
            }
            return dataChunk;
        }

        #endregion

        #region Public Methods
        public override Task GetData(DataRequest request, IServerStreamWriter<DataChunk> responseStream, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    var metaResponse = GetDataResponseInfo();
                    var metaData = new Metadata();
                    metaData.Add("x-qlik-getdata-bin", metaResponse.ToByteArray());
                    context.WriteResponseHeadersAsync(metaData).Wait();

                    var query = request?.Parameters?.Statement ?? null;
                    logger.Debug($"Parse query {query}");
                    var script = ScriptCode.Parse(query);
                    if (script == null)
                        throw new Exception("The sql script is not valid.");

                    var userParameter = UserParameter.Create(request.Connection.ConnectionString);
                    var qvxTable = GetData(script, userParameter);
                    //var result = new QvxDataTable(qvxTable);
                    //result.Select(qvxTable.Fields);
                    logger.Debug($"Send result table {qvxTable.Name}");
                    //return result;

                    var dataChunk = BuildDataChunk("Hallo Welt");
                    responseStream.WriteAsync(dataChunk);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "The query could not be executed.");
                    LogManager.Flush();
                    responseStream.WriteAsync(new DataChunk());
                    //return new QvxDataTable(new QvxTable() { TableName = "Error" });
                }
            });
        }

        public override Task<MetaInfo> GetMetaInfo(MetaInfoRequest request, ServerCallContext context)
        {
            logger.Info("Connector was called.");
            var metaInfo = new MetaInfo()
            {
                Developer = "akquinet",
                Name = "Demo Connector",
                Version = "0.0.1"
            };
            return Task.FromResult<MetaInfo>(metaInfo);
        }

        public GetDataResponse GetDataResponseInfo()
        {
            var dataResponse = new GetDataResponse()
            {
                TableName = "TestName",
            };
            dataResponse.FieldInfo.Add(new FieldInfo()
            {
                Name = "test1",
                SemanticType = SemanticType.Default,
                FieldAttributes = new FieldAttributes()
                {
                    Type = FieldAttrType.Text
                }
            });
            return dataResponse;
        }

        public void Dispose() { }
        #endregion
    }
}