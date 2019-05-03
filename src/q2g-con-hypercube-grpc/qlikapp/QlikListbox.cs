namespace SSEDemo.Connection
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NLog;
    using Qlik.EngineAPI;
    #endregion

    public class QlikSessionObject
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        private IGenericObject GenericObject { get; set; }
        private int CurrentIndex { get; set; }
        private IDoc SenseApp { get; set; }
        private string FilterText { get; }
        private int Cardinal { get; set; }
        #endregion

        #region Constructor
        public QlikSessionObject(string filtertext, IDoc senseApp)
        {
            SenseApp = senseApp;
            FilterText = filtertext;
            GenericObject = GetGenericObject();
            Cardinal = GetListObject()?.qDimensionInfo?.qCardinal ?? 0;
            CurrentIndex = -1;
        }
        #endregion

        #region Private Methods
        private IGenericObject GetGenericObject()
        {
            try
            {
                var request = JObject.FromObject(new
                {
                    qProp = new
                    {
                        qInfo = new
                        {
                            qType = "ListObject"
                        },
                        qListObjectDef = new
                        {
                            qInitialDataFetch = new List<NxPage>
                        {
                        new NxPage() { qTop = 0, qHeight = 0, qLeft = 0, qWidth = 0 }
                        },
                            qDef = new
                            {
                                qFieldDefs = new List<string>
                            {
                                FilterText
                            },
                                qFieldLabels = new List<string>
                            {
                                $"Label: {FilterText}"
                            },
                                qSortCriterias = new List<SortCriteria>
                            {
                                new SortCriteria() { qSortByState = 1 }
                            }
                            },
                            qShowAlternatives = false,
                        }
                    }
                });

                return SenseApp.CreateSessionObjectAsync(request).Result;
            }
            catch (Exception ex)
            {
                throw new Exception("Can´t create a session object", ex);
            }
        }

        private ListObject GetListObject()
        {
            return GenericObject.GetLayoutAsync<JObject>()
            .ContinueWith<ListObject>((res) =>
            {
                try
                {
                    var listLayout = res.Result as dynamic;
                    return listLayout.qListObject.ToObject<ListObject>();
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"The method \"{nameof(GetListObject)}\" has an error.");
                    return null;
                }
            }).Result;
        }

        private bool SelectValuesInternal(List<int> indecs)
        {
            BeginSelections();
            var selectRes = SelectListObjectValues(indecs);
            if (!selectRes)
            {
                EndSelections(false);
                return false;
            }
            return EndSelections(true);
        }

        private NxCell GetFieldCellFromIndexAsync(int index)
        {
            var request = JObject.FromObject(new
            {
                qPath = "/qListObjectDef",
                qPages = new List<NxPage>
                {
                    new NxPage()
                    {
                            qTop = 0,
                            qLeft = 0,
                            qWidth = 1,
                            qHeight = Cardinal,
                    }
                 }
            });

            return GenericObject.GetListObjectDataAsync<JArray>(request)
            .ContinueWith<NxCell>((res2) =>
            {
                var genObjData = res2.Result;
                var dataPages = genObjData.ToObject<List<NxDataPage>>();
                var firstPage = dataPages.FirstOrDefault() ?? null;
                if (firstPage != null)
                {
                    var matrix = firstPage.qMatrix.ToList();
                    if (index < matrix.Count)
                        return matrix[index].FirstOrDefault() ?? null;
                }
                return null;
            }).Result;
        }
        #endregion

        #region Public Methods
        public void ResetIndex()
        {
            CurrentIndex = -1;
        }

        public GenericObjectLayout GetLayout()
        {
            try
            {
                return GenericObject.GetLayoutAsync().Result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(GetLayout)}\" was failed.");
                return null;
            }
        }

        public async Task AcceptListObjectSearchAsync(bool toggleMode)
        {
            try
            {
                await GenericObject.AcceptListObjectSearchAsync("/qListObjectDef", toggleMode);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(AcceptListObjectSearchAsync)}\" was failed.");
            }
        }

        public bool SearchListObjectFor(string match)
        {
            try
            {
                return GenericObject.SearchListObjectForAsync("/qListObjectDef", match).Result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(SearchListObjectFor)}\" was failed.");
                return false;
            }
        }

        public bool ClearSelections()
        {
            try
            {
                GenericObject.ClearSelectionsAsync("/qListObjectDef").Wait();
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(ClearSelections)}\" was failed.");
                return false;
            }
        }

        public bool BeginSelections()
        {
            try
            {
                GenericObject.BeginSelectionsAsync(new List<string> { "/qListObjectDef" });
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(BeginSelections)}\" was failed.");
                return false;
            }
        }

        public bool SelectListObjectValues(List<int> indecs, bool toggleMode = true)
        {
            try
            {
                return GenericObject.SelectListObjectValuesAsync("/qListObjectDef", indecs, toggleMode).Result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(SelectListObjectValues)}\" was failed.");
                return false;
            }
        }

        public bool EndSelections(bool accept)
        {
            try
            {
                GenericObject.EndSelectionsAsync(accept).Wait();
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(EndSelections)}\" was failed.");
                return false;
            }
        }

        public bool SelectPossible()
        {
            try
            {
                return GenericObject.SelectListObjectPossibleAsync("/qListObjectDef").Result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The method \"{nameof(SelectPossible)}\" was failed.");
                return false;
            }
        }

        public FlatSelection GetNextSelection()
        {
            try
            {
                ClearSelections();
                CurrentIndex++;
                var listObj = GetListObject();
                var count = listObj?.qDimensionInfo?.qCardinal ?? 0;
                if ((CurrentIndex >= count))
                {
                    CurrentIndex = -1;
                    return null;
                }

                var cell = GetFieldCellFromIndexAsync(CurrentIndex);
                if (cell != null)
                {
                    if (cell.qState == StateEnumType.EXCLUDED || cell.qState == StateEnumType.X)
                        return null;
                }
                else
                    throw new Exception($"No nxcell for selection {FilterText} with index {CurrentIndex} found.");

                var selectResult = SelectValuesInternal(new List<int> { cell.qElemNumber });
                if (!selectResult)
                    throw new Exception($"The selection {FilterText} for element number {cell?.qElemNumber} could not execute.");
                return new FlatSelection(FilterText, cell.qText, cell.qElemNumber, cell.qState);
            }
            catch (Exception ex)
            {
                throw new Exception("The next selection could not be set.", ex);
            }
        }
        #endregion
    }

    #region Helper Classes
    public class SelectionGroup
    {
        #region Properties & Varibales
        public List<FlatSelection> FlatSelections { get; }
        public Guid Id { get; }
        #endregion

        public SelectionGroup()
        {
            FlatSelections = new List<FlatSelection>();
            Id = Guid.NewGuid();
        }

        public string GetFlatValues()
        {
            var sb = new StringBuilder();
            foreach (var sel in FlatSelections)
                sb.Append($"{sel.Name}:{sel.Value}={sel.ElementNumber}/{sel.State.ToString()} \r\n");

            return sb.ToString().Trim();
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            foreach (var sel in FlatSelections)
                result.Append($"{sel.Value} ");
            return result.ToString().Trim();
        }
    }

    public class FlatSelection
    {
        #region Properties & Variables
        public string Name { get; }
        public string Value { get; set; }
        public string AlternativName { get; set; }
        public int ElementNumber { get; }
        public StateEnumType State { get; }
        #endregion

        public FlatSelection(string name, string value, int number, StateEnumType state)
        {
            Name = name;
            Value = value;
            ElementNumber = number;
            State = state;
        }

        public override string ToString()
        {
            return $"{Value}={ElementNumber}";
        }
    }
    #endregion
}
