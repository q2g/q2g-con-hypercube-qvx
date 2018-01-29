#region License
/*
Copyright (c) 2017 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace q2gconhypercubeqvx.QlikApplication
{
    using NLog;
    #region Usings
    using Qlik.Engine;
    using Qlik.Sense.Client;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    #endregion

    public class QlikDimensions
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Variables
        private Qlik.Sense.Client.DimensionList Values { get; set; }
        private IApp SenseApp { get; set; }
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
                    }  
                    else
                    {
                        results.Add(new QlikListbox(text, SenseApp));
                    } 
                }

                return results;
            }
            catch(Exception ex)
            {
                throw new Exception("The selection could not be created.", ex);
            }
        }
    }
}