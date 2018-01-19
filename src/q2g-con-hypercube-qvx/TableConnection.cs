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
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using QlikView.Qvx.QvxLibrary;
    using System.IO;
    using System.Diagnostics;
    using System.Threading;
    using Qlik.Sense.Client.Visualizations;
    using Qlik.Engine;
    using QlikTableConnector.QlikApplication;
    #endregion

    public class TableConnection : QvxConnection
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
        #endregion

        #region Init
        public override void Init() { }
        #endregion

        #region Methods
        private bool IsUsedField(string field, ScriptCode code)
        {
            if (code.Fields.Count == 0)
                return true;

            return code.Fields.IndexOf(field) > -1;
        }

        private QvxTable GetData(ScriptCode script)
        {
            try
            {
                var resultTable = new QvxTable()
                {
                    TableName = "CopyTable",
                };

                var fields = new List<QvxField>();
                var rows = new List<QvxDataRow>();

                var qlikApp = AppConfig.GetQlikInstance(script.AppId);
                if (!qlikApp.Connect(true))
                    throw new Exception("No connection possible.");

                //where
                //foreach (var filter in script.Filter)
                //{
                //    filter.Name = "",
                //    filter.Value = "",
                //}

                var table = qlikApp.FirstSession.CurrentApp.GetObject<Table>(script.TableId);
                var width = table.ColumnWidths.Count();

                foreach (var dimInfo in table.DimensionInfo)
                {
                    if (IsUsedField(dimInfo.FallbackTitle, script))
                    {
                        fields.Add(new QvxField(dimInfo.FallbackTitle, QvxFieldType.QVX_TEXT,
                                                QvxNullRepresentation.QVX_NULL_FLAG_SUPPRESS_DATA,
                                                QlikView.Qvx.QvxLibrary.FieldAttrType.ASCII));
                    }
                }

                foreach (var measureInfo in table.MeasureInfo)
                {
                    if (IsUsedField(measureInfo.FallbackTitle, script))
                    {
                        fields.Add(new QvxField(measureInfo.FallbackTitle, QvxFieldType.QVX_TEXT,
                                            QvxNullRepresentation.QVX_NULL_FLAG_SUPPRESS_DATA,
                                            QlikView.Qvx.QvxLibrary.FieldAttrType.ASCII));
                    }
                }

                var initalPage = new NxPage { Top = 0, Left = 0, Width = width, Height = 1000 };
                var allPages = table.HyperCubePager.IteratePages(new[] { initalPage }, Pager.Next);
                var allMatrix = allPages.SelectMany(pages => pages.First().Matrix);
                foreach (var matrix in allMatrix)
                {
                    var row = new QvxDataRow();
                    foreach (var order in table.ColumnOrder)
                    {
                        if (order < fields.Count)
                        {
                            var field = fields[order];
                            row[field] = matrix[order].Text;
                        }
                    }
                    rows.Add(row);
                }

                resultTable.Fields = fields.ToArray();
                resultTable.GetRows = () => { return rows; };
                return resultTable;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The table script can not be executed.");
                return null;
            }
        }

        public override QvxDataTable ExtractQuery(string query, List<QvxTable> tables)
        {
            try
            {
                //Thread.Sleep(12000);
                //parameters comes over sql script
                //this.MParameters.TryGetValue("userId", out string userId);
                //this.MParameters.TryGetValue("userDirectory", out string userDirectory);
                var script = ScriptCode.Parse(query);
                if (script == null)
                    throw new Exception("The sql script is not valid.");    

                var qvxTable = GetData(script);
                var result = new QvxDataTable(qvxTable);
                result.Select(qvxTable.Fields);

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The query could not be executed.");
                return new QvxDataTable(new QvxTable() { TableName = "Error" });
            }
        }
        #endregion
    }
}