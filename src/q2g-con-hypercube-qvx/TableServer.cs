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
    using QlikView.Qvx.QvxLibrary;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Collections.Generic;
    using q2gconhypercubeqvx.QlikApplication;
    using Qlik.Sense.Client.Visualizations;
    using System.Linq;
    using Qlik.Engine;
    using Qlik.Sense.Client;
    using System.IO;
    using System.Reflection;
    using Newtonsoft.Json;
    using NLog;
    #endregion

    public class TableServer : QvxServer
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Variables
        private TableFunc tableFunctions;
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

        private QvDataContractResponse GetDatabases(ConnectorParameter parameter)
        {
            var databaseList = new List<QlikView.Qvx.QvxLibrary.Database>();
            try
            {               
                var qlikApp = AppInstance.GetQlikInstance(parameter);
                if (qlikApp == null)
                    return new QvDataContractDatabaseListResponse { qDatabases = databaseList.ToArray() };
                var apps = qlikApp.GetAllApps();
                foreach (var app in apps)
                    databaseList.Add(new QlikView.Qvx.QvxLibrary.Database() { qName = app });
                return new QvDataContractDatabaseListResponse { qDatabases = databaseList.ToArray() };
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"databases not loaded.");
                return new QvDataContractDatabaseListResponse { qDatabases = databaseList.ToArray() };
            }
        }

        private QvDataContractResponse GetTables(ConnectorParameter parameter, string appId)
        {
            var tables = new List<QvxTable>();

            try
            {                
                var qlikApp = AppInstance.GetQlikInstance(parameter, appId);
                if (qlikApp == null)
                    return new QvDataContractTableListResponse { qTables = tables };
                var options = new NxGetObjectOptions() { Types = new List<string> { "table" } };
                var appObjects = qlikApp.FirstSession.CurrentApp.GetObjectsAsync(options).Result;
                foreach (var obj in appObjects)
                {
                    var table = obj.AsObject<Table>();
                    tables.Add(new QvxTable() { TableName = $"{table.Title} [{table.Id}]" });
                }
                var masterObjectList = qlikApp.FirstSession.CurrentApp.GetMasterObjectListAsync().Result;
                var listLayout = masterObjectList?.GetLayout()?.As<MasterObjectListLayout>();
                if (listLayout != null)
                {
                    foreach (var item in listLayout.AppObjectList.Items)
                    {
                        if (item.Data.Visualization == "table")
                            tables.Add(new QvxTable() { TableName = $"{item.Data.Title} [{item.Info.Id}]" });
                    }
                }
                return new QvDataContractTableListResponse { qTables = tables };
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"tables form app {appId} not loaded.");
                return new QvDataContractTableListResponse { qTables = tables };
            }
        }

        private QvDataContractResponse GetFields(ConnectorParameter parameter, string appId, string objectId)
        {
            try
            {
                var oId = GetObjectId(objectId);
                if (String.IsNullOrEmpty(oId))
                    throw new Exception("no object id for field table found.");
                var script = ScriptCode.Create(appId, oId);
                var resultTable = tableFunctions.GetTableInfosFromApp("FieldTable", script, parameter);
                if (resultTable == null)
                    throw new Exception("no field table found.");
                return new QvDataContractFieldListResponse { qFields = resultTable.QvxTable.Fields };
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"fields from app {appId} and table {objectId} not loaded.");
                return new QvDataContractFieldListResponse { qFields = new QvxField[0] };
            }
        }

        private QvDataContractResponse GetPreview(ConnectorParameter parameter, string appId, string objectId)
        {
            try
            {
                var oId = GetObjectId(objectId);
                if (String.IsNullOrEmpty(oId))
                    throw new Exception("no object id for preview table found.");
                var script = ScriptCode.Create(appId, oId);
                var resultTable = tableFunctions.GetTableInfosFromApp("PreviewTable", script, parameter);
                if (resultTable == null)
                    throw new Exception("no preview table found.");
                return resultTable.Preview;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"fields from app {appId} and table {objectId} not loaded.");
                return new TableFunc.PreviewResponse();
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
                AppInstance.LoadMemory();
                logger.Warn(method + "*****");   
                var parameter = ConnectorParameter.Create(connection.MParameters);
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
                AppInstance.SaveMemory();
                AppInstance.Dispose();
                return ToJson(response);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "ERROR " + ex.StackTrace.ToString());
                return ToJson(new Info { qMessage = "Error" });
            }
        }
        #endregion
    }
}