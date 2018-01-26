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
    using Qlik.Engine;
    using Qlik.Sense.Client;
    using Qlik.Sense.Client.Visualizations;
    using q2gconhypercubeqvx.QlikApplication;
    using QlikView.Qvx.QvxLibrary;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    #endregion

    public class TableFunc
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
        #endregion

        #region private methods
        private bool IsUsedField(string field, ScriptCode code)
        {
            if (code == null || code.Script == null)
                return true;

            if (code.Fields == null || code.Fields.Count == 0)
                return true;

            return code.Fields.IndexOf(field) > -1;
        }

        private List<QvxField> GetHyperCubeFields(IEnumerable<TableHyperCubeDimensionq> dimensions, IEnumerable<TableHyperCubeMeasureq> measures, ScriptCode script = null)
        {
            var fields = new List<QvxField>();

            try
            {
                foreach (var dimInfo in dimensions)
                {
                    if (IsUsedField(dimInfo.FallbackTitle, script))
                    {
                        fields.Add(new QvxField(dimInfo.FallbackTitle, QvxFieldType.QVX_TEXT,
                                            QvxNullRepresentation.QVX_NULL_FLAG_SUPPRESS_DATA,
                                            QlikView.Qvx.QvxLibrary.FieldAttrType.ASCII));
                    }
                }

                foreach (var measureInfo in measures)
                {
                    if (IsUsedField(measureInfo.FallbackTitle, script))
                    {
                        fields.Add(new QvxField(measureInfo.FallbackTitle, QvxFieldType.QVX_TEXT,
                                            QvxNullRepresentation.QVX_NULL_FLAG_SUPPRESS_DATA,
                                            QlikView.Qvx.QvxLibrary.FieldAttrType.ASCII));
                    }
                }
                return fields;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "can´t read hypercube fields.");
                return fields;
            }
        }
        #endregion

        #region public methods
        public TableHelper GetTableInfosFromApp(string tableName, ScriptCode script, string parmStr, QlikApp qlikApp = null)
        {
            try
            {
                var resultTable = new QvxTable()
                {
                    TableName = tableName,
                };

                var width = 0;
                HyperCubePager pager = null;
                IEnumerable<int> columnOrder = null;
                var fields = new List<QvxField>();
                var rows = new List<QvxDataRow>();

                if (qlikApp == null)
                {
                    var parameter = ConnectorParameter.Create(parmStr);
                    qlikApp = AppInstance.GetQlikInstance(parameter, script.AppId);
                }

                if (qlikApp == null)
                    return null;

                var masterObject = qlikApp.FirstSession.CurrentApp.GetMasterObjectAsync(script.ObjectId).Result;
                if (masterObject != null)
                {
                    var genericObject = qlikApp.FirstSession.CurrentApp.CreateGenericSessionObjectAsync(masterObject.Properties).Result;
                    pager = genericObject.GetAllHyperCubePagers().FirstOrDefault() ?? null;
                    var tableLayout = genericObject.GetLayout().As<TableLayout>();
                    var hyperCube = tableLayout.HyperCube;
                    width = hyperCube.ColumnWidths.Count();
                    columnOrder = hyperCube.ColumnOrder;
                    fields.AddRange(GetHyperCubeFields(hyperCube.DimensionInfo, hyperCube.MeasureInfo, script));
                }
                var table = qlikApp.FirstSession.CurrentApp.GetObjectAsync<Table>(script.ObjectId).Result;
                if (table != null)
                {
                    width = table.ColumnWidths.Count();
                    pager = table.HyperCubePager;
                    columnOrder = table.ColumnOrder;
                    fields.AddRange(GetHyperCubeFields(table.DimensionInfo, table.MeasureInfo, script));
                }

                var preview = new PreviewResponse()
                {
                     MaxCount = 15,
                };

                if (script != null)
                {
                    var initalPage = new NxPage { Top = 0, Left = 0, Width = width, Height = preview.MaxCount };
                    var allPages = new List<IEnumerable<NxDataPage>>();
                    allPages.Add(pager.GetData(new List<NxPage>() { initalPage }));
                    if (script.Full)
                    {
                        initalPage = new NxPage { Top = 0, Left = 0, Width = width, Height = 1000 };
                        allPages = pager.IteratePages(new[] { initalPage }, Pager.Next).ToList();
                        preview.MaxCount = 0;
                    }
                    if (allPages == null)
                        throw new Exception($"no dimension in table {script.ObjectId} exits.");
                    foreach (var page in allPages)
                    {
                        var allMatrix = page?.SelectMany(p => p.Matrix);
                        foreach (var matrix in allMatrix)
                        {
                            var row = new QvxDataRow();
                            var hrow = new PreviewRow();
                            var drow = new PreviewRow();
                            foreach (var order in columnOrder)
                            {
                                if (order < fields.Count)
                                {
                                    var field = fields[order];
                                    row[field] = matrix[order].Text;
                                    if (!preview.qPreview.Any(s => s.qValues.Contains(field.FieldName)))
                                        hrow.qValues.Add(field.FieldName);
                                    if (preview.qPreview.Count <= preview.MaxCount)
                                        drow.qValues.Add(matrix[order].Text);
                                }
                            }
                            rows.Add(row);
                            if (hrow.qValues.Count > 0)
                                preview.qPreview.Add(hrow);
                            if (drow.qValues.Count > 0)
                                preview.qPreview.Add(drow);
                        }
                    }
                }

                resultTable.Fields = fields.ToArray();
                resultTable.GetRows = () => { return rows; };
                return new TableHelper(resultTable, preview);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "can´t read table infos.");
                return null;
            }
        }
        #endregion

        #region helper classes
        public class TableHelper
        {
            public TableHelper(QvxTable table, PreviewResponse preview)
            {
                QvxTable = table;
                Preview = preview;
            }

            public QvxTable QvxTable { get; private set; }
            public PreviewResponse Preview { get; private set; }
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
            public int MaxCount { get; set; } = 15;
            public PreviewResponse() : base()
            {
                this.qPreview = new List<PreviewRow>();
            }
        }
        #endregion
    }
}