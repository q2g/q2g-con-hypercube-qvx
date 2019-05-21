namespace q2gconhypercubemain
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NLog;
    using Qlik.EngineAPI;
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

        private List<ResultHeader> GetHyperCubeFields(List<NxDimensionInfo> dimensions, List<NxMeasureInfo> measures, ScriptCode script = null)
        {
            var fields = new List<ResultHeader>();

            try
            {
                foreach (var dimInfo in dimensions)
                {
                    if (IsUsedField(dimInfo.qFallbackTitle, script))
                    {
                        fields.Add(new ResultHeader() { Name = dimInfo.qFallbackTitle, Type = DataType.TEXT });
                    }
                }

                foreach (var measureInfo in measures)
                {
                    if (IsUsedField(measureInfo.qFallbackTitle, script))
                    {
                        fields.Add(new ResultHeader() { Name = measureInfo.qFallbackTitle, Type = DataType.TEXT });
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
                var resultTable = new ResultTable()
                {
                    Name = tableName,
                };

                var fields = new List<ResultHeader>();
                var rows = new List<ResultRow>();
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
                        Parallel.For(0, counter, options, i =>
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
                            foreach (var order in columnOrder)
                            {
                                if (order < fields.Count)
                                {
                                    var row = new ResultRow();
                                    var hrow = new PreviewRow();
                                    var drow = new PreviewRow();

                                    var field = fields[order];
                                    row.Value = matrix[order].qText;
                                    row.Num = matrix[order]?.qNum ?? Double.NaN;
                                    row.Header = field.Name;

                                    if (order == 0)
                                        row.IsFirstRow = true;

                                    if (order == fields.Count - 1)
                                        row.IsLastRow = true;

                                    rows.Add(row);

                                    if (!preview.qPreview.Any(s => s.qValues.Contains(field.Name)))
                                        hrow.qValues.Add(field.Name);
                                    if (preview.qPreview.Count <= preview.MaxCount)
                                        drow.qValues.Add(matrix[order].qText);

                                    if (hrow.qValues.Count > 0)
                                        preview.qPreview.Add(hrow);
                                    if (drow.qValues.Count > 0)
                                        preview.qPreview.Add(drow);
                                }
                            }
                        }
                    }
                }

                resultTable.Headers.AddRange(fields);
                resultTable.Rows.AddRange(rows);
                logger.Debug($"return table {resultTable.Name}");
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
            public TableHelper(ResultTable table, IPreviewResponse preview)
            {
                QvxTable = table;
                Preview = preview;
            }

            public ResultTable QvxTable { get; private set; }
            public IPreviewResponse Preview { get; private set; }
        }

        public class PreviewRow
        {
            public List<string> qValues { get; set; }
            public PreviewRow()
            {
                qValues = new List<string>();
            }
        }

        public interface IPreviewResponse
        {
            List<PreviewRow> qPreview { get; set; }
            int MaxCount { get; set; }
        }

        public class PreviewResponse : IPreviewResponse
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