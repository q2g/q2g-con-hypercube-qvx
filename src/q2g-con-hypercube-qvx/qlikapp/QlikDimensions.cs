namespace QlikTableConnector.QlikApplication
{
    #region Usings
    using Qlik.Engine;
    using Qlik.Sense.Client;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using QlikTableConnector;
    #endregion

    public class QlikDimensions
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
        #endregion

        public QlikDimensions(IApp senseApp)
        {
            SenseApp = senseApp;
            Values = SenseApp.GetDimensionList() as Qlik.Sense.Client.DimensionList;
        }

        private List<QlikListbox> GetFieldDefs(string text)
        {
            var results = new List<QlikListbox>();
            foreach (var item in Values.Items)
            {
                var dimID = SenseApp.GetDimension(item.Info.Id);
                var title = dimID.MetaAttributes.Title;
                var dim = dimID.Dim;

                if (dim.Grouping == NxGrpType.GRP_NX_HIEARCHY ||
                    dim.Grouping == NxGrpType.GRP_NX_NONE)
                {
                    if (text == title)
                    {
                        var fieldDefs = dim.FieldDefs;
                        foreach (var fieldDef in fieldDefs)
                        {
                            results.Add(new QlikListbox(fieldDef, SenseApp));
                        }
                    }
                }
            }

            return results;
        }

        public List<QlikListbox> GetSelections(List<string> filterTexts)
        {
            try
            {
                var results = new List<QlikListbox>();
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
                        results.Add(new QlikListbox(text, SenseApp));
                        logger.Info($"The filter text \"{text}\" is not a master element.");
                    } 
                }

                return results;
            }
            catch(Exception ex)
            {
                throw new Exception("The selection could not be created.", ex);
            }
        }

        private Qlik.Sense.Client.DimensionList Values { get; set; }
        private IApp SenseApp { get; set; }
    }
}
