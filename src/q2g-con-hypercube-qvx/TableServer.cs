#region License
/*
Copyright (c) 2018 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace q2gconhypercubeqvx
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using Newtonsoft.Json.Linq;
    using NLog;
    using q2gconhypercubemain;
    using Qlik.EngineAPI;
    using QlikView.Qvx.QvxLibrary;
    #endregion

    public class TableServer : QvxServer
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Variables
        private TableFunc tableFunctions;
        #endregion

        #region Constructor
        public TableServer()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate (Object obj,
                X509Certificate certificate,
                X509Chain chain,
                SslPolicyErrors errors)
            {
                return (true);
            };
        }
        #endregion

        #region private methods
        private string GetObjectId(string value)
        {
            if (value.LastIndexOf("[") > -1 && value.EndsWith("]"))
            {
                var index = value.LastIndexOf("[") + 1;
                return value.Substring(index, (value.Length - index) - 1);
            }
            return null;
        }

        private QvDataContractResponse GetDatabases(UserParameter parameter)
        {
            var databaseList = new List<QlikView.Qvx.QvxLibrary.Database>();
            try
            {
                var config = QlikApp.CreateConfig(parameter);
                var qlikApp = new QlikApp(parameter);
                var apps = qlikApp.GetAllApps(config);
                var appNames = apps.Select(s => s.qDocName).ToList();
                foreach (var app in appNames)
                    databaseList.Add(new QlikView.Qvx.QvxLibrary.Database() { qName = app });
                return new QvDataContractDatabaseListResponse { qDatabases = databaseList.ToArray() };
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"databases not loaded.");
                return new QvDataContractDatabaseListResponse { qDatabases = databaseList.ToArray() };
            }
        }

        private QvDataContractResponse GetTables(UserParameter parameter, string appName)
        {
            var tables = new List<QvxTable>();
            q2gconhypercubemain.Connection connection = null;

            using (MappedDiagnosticsLogicalContext.SetScoped("connectionId", connection?.ConnId))
            {
                try
                {
                    var config = QlikApp.CreateConfig(parameter, appName);
                    var qlikApp = new QlikApp(parameter);
                    var appId = qlikApp.GetAllApps(config).FirstOrDefault(a => a.qDocName == appName).qDocId;
                    config = QlikApp.CreateConfig(parameter, appId);
                    connection = qlikApp.CreateNewConnection(config);

                    var options = new NxGetObjectOptions() { qTypes = new List<string> { "table" } };
                    var tablesObjects = connection.CurrentApp.GetObjectsAsync(options).Result;
                    foreach (var obj in tablesObjects)
                    {
                        var tableObject = connection.CurrentApp.GetObjectAsync(obj.qInfo.qId).Result;
                        dynamic layout = tableObject.GetLayoutAsync<JObject>().Result;
                        tables.Add(new QvxTable() { TableName = $"{layout.title} [{obj.qInfo.qId}]" });
                    }

                    options = new NxGetObjectOptions() { qTypes = new List<string> { "masterobject" } };
                    var visualisations = connection.CurrentApp.GetObjectsAsync(options).Result;
                    foreach (var element in visualisations)
                    {
                        var tableObject = connection.CurrentApp.GetObjectAsync(element.qInfo.qId).Result;
                        dynamic layout = tableObject.GetLayoutAsync<JObject>().Result;
                        tables.Add(new QvxTable() { TableName = $"{layout.qMeta.title} [{element.qInfo.qId}]" });
                    }

                    return new QvDataContractTableListResponse { qTables = tables };

                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"tables form app {appName} not loaded.");
                    return new QvDataContractTableListResponse { qTables = tables };
                }
                finally
                {
                    connection?.Close();
                }
            }
        }

        private QvDataContractResponse GetFields(UserParameter parameter, string appId, string objectId)
        {
            q2gconhypercubemain.Connection connection = null;

            using (MappedDiagnosticsLogicalContext.SetScoped("connectionId", connection?.ConnId))
            {
                try
                {
                    var oId = GetObjectId(objectId);
                    if (String.IsNullOrEmpty(oId))
                        throw new Exception("no object id for field table found.");
                    var script = ScriptCode.Create(appId, oId);
                    var config = QlikApp.CreateConfig(parameter, appId);
                    var qlikApp = new QlikApp(parameter);
                    connection = qlikApp.CreateNewConnection(config);
                    var resultTable = tableFunctions.GetTableInfosFromApp("FieldTable", script, connection.CurrentApp);
                    if (resultTable == null)
                        throw new Exception("no field table found.");
                    var qvxTable = TableUtilities.ConvertTable(resultTable.QvxTable);
                    return new QvDataContractFieldListResponse { qFields = qvxTable.Fields };
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"fields from app {appId} and table {objectId} not loaded.");
                    return new QvDataContractFieldListResponse { qFields = new QvxField[0] };
                }
                finally
                {
                    connection?.Close();
                }
            }
        }

        private QvDataContractResponse GetPreview(UserParameter parameter, string appId, string objectId)
        {
            q2gconhypercubemain.Connection connection = null;

            try
            {
                var oId = GetObjectId(objectId);
                if (String.IsNullOrEmpty(oId))
                    throw new Exception("no object id for preview table found.");
                var config = QlikApp.CreateConfig(parameter, appId);
                var qlikApp = new QlikApp(parameter);
                connection = qlikApp.CreateNewConnection(config);
                var script = ScriptCode.Create(appId, oId);
                var resultTable = tableFunctions.GetTableInfosFromApp("PreviewTable", script, connection.CurrentApp);
                if (resultTable == null)
                    throw new Exception("no preview table found.");
                return resultTable.Preview as PreviewResponse;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"fields from app {appId} and table {objectId} not loaded.");
                return new PreviewResponse();
            }
            finally
            {
                connection?.Close();
            }
        }
        #endregion

        #region public methods
        public override QvxConnection CreateConnection()
        {
            try
            {
                tableFunctions = new TableFunc();
                return new TableConnection();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The connection could not be created.");
                return null;
            }
        }

        public override string CreateConnectionString()
        {
            try
            {
                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The connection string could not be created.");
                return null;
            }
        }

        public override string HandleJsonRequest(string method, string[] userParameters, QvxConnection connection)
        {
            try
            {
                QvDataContractResponse response;
                var parameter = UserParameter.Create(connection?.MParameters);
                logger.Trace($"HandleJsonRequest {method}");
                switch (method)
                {
                    case "getVersion":
                        response = new Info { qMessage = GitVersionInformation.InformationalVersion };
                        break;
                    case "getUsername":
                        response = new Info { qMessage = parameter.UserName };
                        break;
                    case "getDatabases":
                        response = GetDatabases(parameter);
                        break;
                    case "getTables":
                        response = GetTables(parameter, userParameters[0]);
                        break;
                    case "getFields":
                        response = GetFields(parameter, userParameters[0], userParameters[1]);
                        break;
                    case "getPreview":
                        response = GetPreview(parameter, userParameters[0], userParameters[1]);
                        break;
                    default:
                        response = new Info { qMessage = "Unknown command" };
                        break;
                }
                return ToJson(response);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return ToJson(new Info { qMessage = "Error " + ex.ToString() });
            }
        }
        #endregion
    }
}