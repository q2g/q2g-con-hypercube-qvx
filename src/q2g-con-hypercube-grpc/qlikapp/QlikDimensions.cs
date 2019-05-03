namespace SSEDemo.Connection
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NLog;
    using Qlik.EngineAPI;
    #endregion

    public class QlikDimensions
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        private List<DimensionDataHelper> Dimensions { get; set; }
        private IDoc SenseApp { get; set; }
        #endregion

        #region Constructor
        public QlikDimensions(IDoc senseApp)
        {
            SenseApp = senseApp;
            Dimensions = GetDimensionListAsync().Result;
        }
        #endregion

        #region Private Methods
        private async Task<List<DimensionDataHelper>> GetDimensionListAsync()
        {
            try
            {
                var request = JObject.FromObject(new
                {
                    qProp = new
                    {
                        qInfo = new
                        {
                            qType = "DimensionList"
                        },
                        qDimensionListDef = new
                        {
                            qType = "dimension",
                            qData = new
                            {
                                grouping = "/qDim"
                            }
                        }
                    }
                });

                return await SenseApp.CreateSessionObjectAsync(request)
                .ContinueWith((res) =>
                {
                    var layout = res.Result.GetLayoutAsync<JObject>();
                    SenseApp.DestroySessionObjectAsync(res.Result.qGenericId);
                    return layout;
                })
                .Unwrap()
                .ContinueWith<List<DimensionDataHelper>>((res2) =>
                {
                    var ret = res2.Result as dynamic;
                    var dimList = ret.qDimensionList;
                    var result = new List<DimensionDataHelper>();
                    foreach (var qitem in dimList.qItems)
                    {
                        var defs = qitem.qData.grouping.qFieldDefs as JToken;
                        var grouping = qitem.qData.grouping.qGrouping.ToObject<NxGrpType>();
                        result.Add(new DimensionDataHelper()
                        {
                            Id = qitem.qInfo.qId,
                            Title = qitem.qMeta.title,
                            Grouping = grouping,
                            FieldDefs = defs.ToObject<List<string>>(),
                        });
                    }
                    return result;
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Can´t initialize dimensions.");
                return null;
            }
        }

        private List<QlikSessionObject> GetFieldDefs(string filterText)
        {
            try
            {
                var results = new List<QlikSessionObject>();
                foreach (var dim in Dimensions)
                {
                    if (dim.Grouping == NxGrpType.GRP_NX_HIEARCHY ||
                        dim.Grouping == NxGrpType.GRP_NX_NONE)
                    {
                        if (dim.Title == filterText)
                        {
                            foreach (var fieldDef in dim.FieldDefs)
                                results.Add(new QlikSessionObject(fieldDef, SenseApp));
                        }
                    }
                }
                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetFieldDefs)}\" has an error.");
                return null;
            }
        }
        #endregion

        #region Public Methods
        public QlikSessionObject GetSelections(string filterText)
        {
            try
            {
                var listbox = GetFieldDefs(filterText).FirstOrDefault() ?? null;
                if (listbox != null)
                {
                    logger.Info($"The master element \"{filterText}\" was found.");
                    return listbox;
                }
                else
                {
                    logger.Info($"The filter text \"{filterText}\" is not a master element.");
                    return new QlikSessionObject(filterText, SenseApp);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("The selection could not be created.", ex);
            }
        }

        public List<QlikSessionObject> GetListboxList(List<string> filterTexts)
        {
            try
            {
                var results = new List<QlikSessionObject>();
                foreach (var text in filterTexts)
                {
                    var listboxes = GetFieldDefs(text);
                    if (listboxes.Count > 0)
                    {
                        results.AddRange(listboxes);
                        logger.Info($"The master element \"{text}\" was found.");
                    }
                    else
                    {
                        results.Add(new QlikSessionObject(text, SenseApp));
                        logger.Info($"The filter text \"{text}\" is not a master element.");
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                throw new Exception("The selection could not be created.", ex);
            }
        }
        #endregion
    }

    public class DimensionDataHelper
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public List<string> FieldDefs { get; set; }
        public NxGrpType Grouping { get; set; }
    }
}