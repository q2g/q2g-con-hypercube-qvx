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
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NLog;
    using Qlik.EngineAPI;
    using QlikView.Qvx.QvxLibrary;
    #endregion

    public class TableFunc
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
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

        private List<QvxField> GetHyperCubeFields(List<NxDimensionInfo> dimensions, List<NxMeasureInfo> measures, ScriptCode script = null)
        {
            var fields = new List<QvxField>();

            try
            {
                foreach (var dimInfo in dimensions)
                {
                    if (IsUsedField(dimInfo.qFallbackTitle, script))
                    {
                        fields.Add(new QvxField(dimInfo.qFallbackTitle, QvxFieldType.QVX_TEXT,
                                            QvxNullRepresentation.QVX_NULL_FLAG_SUPPRESS_DATA,
                                            QlikView.Qvx.QvxLibrary.FieldAttrType.ASCII));
                    }
                }

                foreach (var measureInfo in measures)
                {
                    if (IsUsedField(measureInfo.qFallbackTitle, script))
                    {
                        fields.Add(new QvxField(measureInfo.qFallbackTitle, QvxFieldType.QVX_TEXT,
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
        public TableHelper GetTableInfosFromApp(string tableName, ScriptCode script, IDoc app)
        {
            try
            {
                var resultTable = new QvxTable()
                {
                    TableName = tableName,
                };

                var fields = new List<QvxField>();
                var rows = new List<QvxDataRow>();
                var size = new Size();

                if (app == null)
                    throw new Exception("No App session.");

                var tableObject = app.GetObjectAsync(script.ObjectId).Result;
                if (tableObject == null)
                    throw new Exception("No Table object found.");
                logger.Debug($"TableObject objectId: {script.ObjectId} - objectType {tableObject.qGenericType}");

                dynamic hyperCubeLayout = tableObject.GetLayoutAsync<JObject>().Result;
                HyperCube hyperCube = hyperCubeLayout.qHyperCube.ToObject<HyperCube>();
                var columnOrder = hyperCube.qColumnOrder.ToList();
                size = hyperCube.qSize;
                fields.AddRange(GetHyperCubeFields(hyperCube.qDimensionInfo, hyperCube.qMeasureInfo, script));

                if (columnOrder == null || columnOrder.Count == 0)
                {
                    columnOrder = new List<int>();
                    for (int i = 0; i < fields.Count; i++)
                        columnOrder.Add(i);
                }

                var preview = new PreviewResponse()
                {
                    MaxCount = 15,
                };

                if (script != null)
                {
                    var allPages = new List<IEnumerable<NxDataPage>>();
                    if (script.Full)
                    {
                        //DataLoad
                        preview.MaxCount = 0;
                        var pageHeight = Math.Min(size.qcy * size.qcx, 5000) / size.qcx;
                        logger.Debug($"read data - column count: {size.qcx}");
                        var counter = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(size.qcy) / Convert.ToDouble(pageHeight)));
                        allPages = new List<IEnumerable<NxDataPage>>(counter);
                        var options = new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount };
                        Parallel.For(0, counter, options, i  =>
                        {
                            var initalPage = new NxPage { qTop = 0, qLeft = 0, qWidth = size.qcx, qHeight = pageHeight };
                            initalPage.qTop = i * pageHeight;
                            var pages = tableObject.GetHyperCubeDataAsync("/qHyperCubeDef", new List<NxPage>() { initalPage }).Result;
                            allPages.Add(pages);
                        });
                    }
                    else
                    {
                        //Preview
                        var initalPage = new NxPage { qTop = 0, qLeft = 0, qWidth = size.qcx, qHeight = preview.MaxCount };
                        var pages = tableObject.GetHyperCubeDataAsync("/qHyperCubeDef", new List<NxPage>() { initalPage }).Result;
                        allPages.Add(pages);
                    }
                    if (allPages == null)
                        throw new Exception($"no dimension in table {script.ObjectId} exits.");
                    logger.Debug($"read pages - count {allPages.Count}");
                    foreach (var page in allPages)
                    {
                        var allMatrix = page?.SelectMany(p => p.qMatrix);
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
                                    row[field] = matrix[order].qText;
                                    if (!preview.qPreview.Any(s => s.qValues.Contains(field.FieldName)))
                                        hrow.qValues.Add(field.FieldName);
                                    if (preview.qPreview.Count <= preview.MaxCount)
                                        drow.qValues.Add(matrix[order].qText);
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
                logger.Debug($"return table {resultTable.TableName}");
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