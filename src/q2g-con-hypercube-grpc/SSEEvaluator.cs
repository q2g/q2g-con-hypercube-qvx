namespace q2gconhypercubegrpc
{
    #region Usings
    using Google.Protobuf;
    using Grpc.Core;
    using NLog;
    using Qlik.Connect;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using static Qlik.Connect.Connector;
    using q2gconhypercubemain;
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
            tableFunctions = new TableFunc();
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
            q2gconhypercubemain.Connection connection = null;

            try
            {
                var config = QlikApp.CreateConfig(parameter, script.AppId);
                var qlikApp = new QlikApp(parameter);
                connection = qlikApp.CreateNewConnection(config);
                if (connection == null)
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

        private DataChunk GetDataChunk(ResultTable table)
        {
            try
            {
                var chunkIndex = -1;
                var rowChunk = new DataChunk();
                foreach (var header in table.Headers)
                {
                    foreach (var row in table.Rows)
                    {
                        chunkIndex++;
                        if (chunkIndex > 0)
                            rowChunk = new DataChunk(rowChunk);
                        if (row.Value == null)
                        {
                            rowChunk.StringBucket.Add(row.Value);
                            rowChunk.StringCodes.Add(-1);
                            rowChunk.NumberCodes.Add(-1);
                        }
                        else
                        {
                            rowChunk.StringBucket.Add(row.Value);
                            rowChunk.StringCodes.Add(chunkIndex);
                            rowChunk.NumberCodes.Add(-1);
                        }
                    }
                }
                return rowChunk;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "No Datachunks build from table.");
                return new DataChunk();
            }
        }

        #endregion

        #region Public Methods
        public override Task GetData(DataRequest request, IServerStreamWriter<DataChunk> responseStream, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                try
                {
                    var query = request?.Parameters?.Statement ?? null;
                    logger.Debug($"Parse query {query}");
                    var script = ScriptCode.Parse(query);
                    if (script == null)
                        throw new Exception("The sql script is not valid.");

                    var userParameter = UserParameter.Create(request.Connection.ConnectionString);
                    var resultTable = GetData(script, userParameter);
                    logger.Debug($"Send result table {resultTable.Name}");

                    //Write meta table
                    var metaResponse = GetDataResponseInfo(resultTable);
                    var metaData = new Metadata();
                    metaData.Add("x-qlik-getdata-bin", metaResponse.ToByteArray());
                    context.WriteResponseHeadersAsync(metaData).Wait();

                    //Write data
                    var dataChunk = GetDataChunk(resultTable);
                    responseStream.WriteAsync(dataChunk);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "The query could not be executed.");
                    LogManager.Flush();
                    var errorChunk = new DataChunk();
                    errorChunk.StringCodes.Add(-1);
                    responseStream.WriteAsync(errorChunk);
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

        public GetDataResponse GetDataResponseInfo(ResultTable table)
        {
            var dataResponse = new GetDataResponse()
            {
                TableName = table.Name,
            };
            foreach (var header in table.Headers)
            {
                dataResponse.FieldInfo.Add(new FieldInfo()
                {
                    Name = header.Name,
                    SemanticType = SemanticType.Default,
                    FieldAttributes = new FieldAttributes()
                    {
                        Type = FieldAttrType.Text
                    }
                });
            }
            return dataResponse;
        }

        public void Dispose() { }
        #endregion
    }
}