#region License
/*
Copyright (c) 2017 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace QlikTableConnector
{
    #region Usings
    using System;
    using QlikView.Qvx.QvxLibrary;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Collections.Generic;
    using QlikTableConnector.QlikApplication;
    using Qlik.Sense.Client.Visualizations;
    using System.Linq;
    using Qlik.Engine;
    using Qlik.Sense.Client;
    using System.IO;
    using System.Reflection;
    using Newtonsoft.Json;
    #endregion

    public class TableServer : QvxServer
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
        #endregion

        #region Methods
        public override QvxConnection CreateConnection()
        {
            try
            {
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

        

        private QvDataContractResponse GetDatabases()
        {
            var qlikApp = AppConfig.GetQlikInstance();
            var apps = qlikApp.GetAllApps();
            var databaseList = new List<QlikView.Qvx.QvxLibrary.Database>();
            foreach (var app in apps)
                databaseList.Add(new QlikView.Qvx.QvxLibrary.Database() { qName = app });

            return new QvDataContractDatabaseListResponse
            {
                qDatabases = databaseList.ToArray(),
            };
        }

        private QvDataContractResponse GetTables(string appId)
        {
            var tables = new List<QvxTable>();
            var qlikApp = AppConfig.GetQlikInstance(appId);
            if (!qlikApp.Connect(true))
                throw new Exception($"No connection with app {appId}");

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

            return new QvDataContractTableListResponse
            {
                qTables = tables,
            };
        }

        private QvDataContractResponse GetFields(string appId, string tableId)
        {
           // Thread.Sleep(12000);

            var id = String.Empty;
            if (tableId.LastIndexOf("[") > -1 && tableId.EndsWith("]"))
            {
                var index = tableId.LastIndexOf("[") + 1;
                id = tableId.Substring(index, (tableId.Length - index) - 1);
            }

            var fields = new List<QvxField>();
            if (!String.IsNullOrEmpty(id))
            {
                var qlikApp = AppConfig.GetQlikInstance(appId);
                if (!qlikApp.Connect(true))
                    throw new Exception($"No connection with app {appId}");

                var masterObject = qlikApp.FirstSession.CurrentApp.GetMasterObject(id);
                if (masterObject != null)
                {
                    var genericObject = qlikApp.FirstSession.CurrentApp.CreateGenericSessionObjectAsync(masterObject.Properties).Result;
                    var tableLayout = genericObject.GetLayout().As<TableLayout>();
                    var hyperCube = tableLayout.HyperCube.Get<Table>("table");
                    //hyperCube.DimensionInfo
                    //which object to cast ???
                }

                var table = qlikApp.FirstSession.CurrentApp.GetObject<Table>(id);
                if (table != null)
                {
                    foreach (var dimInfo in table.DimensionInfo)
                    {
                        fields.Add(new QvxField(dimInfo.FallbackTitle, QvxFieldType.QVX_TEXT,
                                                QvxNullRepresentation.QVX_NULL_FLAG_SUPPRESS_DATA,
                                                QlikView.Qvx.QvxLibrary.FieldAttrType.ASCII));
                    }

                    foreach (var measureInfo in table.MeasureInfo)
                    {
                        fields.Add(new QvxField(measureInfo.FallbackTitle, QvxFieldType.QVX_TEXT,
                                                QvxNullRepresentation.QVX_NULL_FLAG_SUPPRESS_DATA,
                                                QlikView.Qvx.QvxLibrary.FieldAttrType.ASCII));
                    }
                }
            }

            return new QvDataContractFieldListResponse
            {
                qFields = fields.ToArray(),
            };
        }

        private QvDataContractResponse GetPreview(string appId, string tableId)
        {
            //Thread.Sleep(12000);

            var result = new PreviewResponse();
            // Store the table Header (my table has two columns)
            var row = new PreviewRow();
            row.qValues.Add("Account Group");
            result.qPreview.Add(row);

            row = new PreviewRow();
            row.qValues.Add("1");
            result.qPreview.Add(row);

            row = new PreviewRow();
            row.qValues.Add("2");
            result.qPreview.Add(row);

            return result;
        }

        public override string HandleJsonRequest(string method, string[] userParameters, QvxConnection connection)
        {
            try
            {
                QvDataContractResponse response;

                switch (method)
                {
                    case "getVersion":
                        response = new Info { qMessage = "1.0.0" };
                        break;
                    case "getDatabases":
                        response = GetDatabases();
                        break;
                    case "getTables":
                        response = GetTables(userParameters[0]);
                        break;
                    case "getFields":
                        response = GetFields(userParameters[0], userParameters[1]);
                        break;
                    case "getPreview":
                        response = GetPreview(userParameters[0], userParameters[1]);
                        break;
                    default:
                        response = new Info { qMessage = "Unknown command" };
                        break;
                }

                return ToJson(response);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The json could not be read.");
                return ToJson(new Info { qMessage = "Error" });
            }
        }
        #endregion
    }

    public class PreviewRow
    {
        public List<string> qValues { get; set; }
        public PreviewRow()
        {
            qValues = new List<string>();
        }
    }

    public class PreviewResponse : QvDataContractResponse
    {
        public List<PreviewRow> qPreview { get; set; }
        public PreviewResponse() : base()
        {
            this.qPreview = new List<PreviewRow>();
        }
    }
}