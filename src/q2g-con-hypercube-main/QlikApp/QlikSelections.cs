namespace q2gconhypercubemain
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NLog;
    using System.Threading.Tasks;
    using Qlik.EngineAPI;
    using Newtonsoft.Json.Linq;
    #endregion

    public class QlikSelections
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        private IDoc SenseApp { get; set; }
        private QlikDimensions Dimensions { get; set; }
        #endregion

        public QlikSelections(IDoc senseApp)
        {
            SenseApp = senseApp;
            Dimensions = new QlikDimensions(senseApp);
        }

        public async Task<SelectionObject> GetCurrentSelectionAsync()
        {
            try
            {
                var request = JObject.FromObject(new
                {
                    qProp = new
                    {
                        qInfo = new
                        {
                            qType = "CurrentSelection"
                        },
                        qSelectionObjectDef = new { }
                    }
                });

                return await SenseApp.CreateSessionObjectAsync(request)
                .ContinueWith((res) =>
                {
                    return res.Result.GetLayoutAsync<JObject>();
                })
                .Unwrap()
                .ContinueWith<SelectionObject>((res2) =>
                {
                    var ret = res2.Result as dynamic;
                    var jsonObj = ret.qSelectionObject as JObject;
                    var selectionObj = jsonObj.ToObject<SelectionObject>();
                    return selectionObj;
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The filter selection could not be determined.");
                return null;
            }
        }

        public void SelectAllValues(string filterText)
        {
            SelectAllValues(new List<string>() { filterText });
        }

        public void SelectAllValues(List<string> filterTexts)
        {
            try
            {
                var listBoxes = Dimensions.GetListboxList(filterTexts);
                listBoxes?.ForEach(l => l.SelectPossible());
            }
            catch (Exception ex)
            {
                throw new Exception($"The selections could not be executed.", ex);
            }
        }

        public void ClearSelections(List<string> filterText)
        {
            var listBoxes = Dimensions.GetListboxList(filterText);
            listBoxes?.ForEach(l => l.ClearSelections());
        }

        public bool SelectValue(string filterText, string match)
        {
            try
            {
                var listBox = Dimensions.GetSelections(filterText);
                var searchResult = listBox.SearchListObjectFor(match);
                if (!searchResult)
                    return false;
                listBox.GetLayout();
                listBox.AcceptListObjectSearchAsync(true).Wait();
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The filter {filterText} coult not set with match {match}.");
                return false;
            }
        }

        public async Task ClearAllSelectionsAsync()
        {
            try
            {
                await SenseApp.AbortModalAsync(false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The Qlik selections could not be abort.");
            }

            try
            {
                await SenseApp.ClearAllAsync(true);
            }
            catch (Exception ex)
            {
                throw new Exception("The Qlik selections could not be cleared.", ex);
            }
        }
    }
}