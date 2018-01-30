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
    using Qlik.Sense.Client.Visualizations;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    #endregion

    public class QlikSelections
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        private IApp SenseApp { get; set; }
        private QlikDimensions Dimensions { get; set; }
        #endregion

        public QlikSelections(IApp senseApp)
        {
            SenseApp = senseApp;
            Dimensions = new QlikDimensions(senseApp);
        }

        #region private methods
        private bool RecursiveSelection(List<QlikListbox> listboxes, int start, SelectionGroup group, List<SelectionGroup> groups)
        {
            if (start == listboxes.Count)
            {
                groups.Add(group);
                return false;
            }

            for (int i = start; i < listboxes.Count; i++)
            {
                var flatSelection = listboxes[i].GetNextSelection();
                if (flatSelection == null)
                    return false;

                group.FlatSelections.Add(flatSelection);
                if (RecursiveSelection(listboxes, i + 1, group, groups) == false)
                {
                    i--;
                    var newgroup = new SelectionGroup();
                    var lastidx = group.FlatSelections.Count - start;
                    newgroup.FlatSelections.AddRange(group.FlatSelections.Take(group.FlatSelections.Count - lastidx));
                    group = newgroup;
                }
            }

            return true;
        }
        #endregion

        #region public methods
        public bool SelectValue(string filterText, string match)
        {
            var listBox = Dimensions.GetSelections(new List<string>() { filterText }).FirstOrDefault() ?? null;
            return listBox?.SelectValue(match) ?? false;
        }

        public void SelectAllValues(string filterText)
        {
            SelectAllValues(new List<string>() { filterText });
        }

        public void SelectAllValues(List<string> filterTexts)
        {
            try
            {
                var listBoxes = Dimensions.GetSelections(filterTexts);
                listBoxes?.ForEach(l => l.SelectAll());
            }
            catch (Exception ex)
            {
                throw new Exception($"The selections could not be executed.", ex);
            }
        }

        public void SelectValues(List<FlatSelection> selections)
        {
            foreach (var flatSel in selections)
                SelectValues(flatSel.Name, new List<int>() { flatSel.ElementNumber });
        }

        public void ClearSelections(List<string> filterText)
        {
            var listBoxes = Dimensions.GetSelections(filterText);
            listBoxes?.ForEach(l => l.ClearSelections());
        }

        public void ClearSelections(List<FlatSelection> selections)
        {
            foreach (var flatSel in selections)
            {
                var listBoxes = Dimensions.GetSelections(new List<string>() { flatSel.Name });
                listBoxes?.ForEach(l => l.ClearSelections());
            }
        }

        public void SelectValues(string filterText, List<string> values)
        {
            SelectValues(new List<string>() { filterText }, values);
        }

        public void SelectValues(List<string> filterTexts, List<string> values)
        {
            try
            {
                var listBoxes = Dimensions.GetSelections(filterTexts);
                listBoxes?.ForEach(l => l.SelectValues(values));
            }
            catch (Exception ex)
            {
                throw new Exception($"The selection could not be executed.", ex);
            }
        }

        public void SelectValues(string filterText, List<int> fieldIndecs)
        {
            SelectValues(new List<string>() { filterText }, fieldIndecs);
        }

        public void SelectValues(List<string> filterTexts, List<int> fieldIndecs)
        {
            try
            {
                var listBoxes = Dimensions.GetSelections(filterTexts);
                listBoxes?.ForEach(l => l.SelectValues(fieldIndecs));
            }
            catch (Exception ex)
            {
                throw new Exception($"The selection could not be executed.", ex);
            }
        }

        public void ClearAllSelections()
        {
            try
            {
                SenseApp.AbortModal(false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The Qlik selections could not be abort.");
                SenseApp.ClearAll(true);
            }

            try
            { 
                SenseApp.ClearAll(true);
            }
            catch (Exception ex)
            {
                throw new Exception("The Qlik selections could not be cleared.", ex);
            }
        }

        public List<SelectionGroup> DynamicSelections(List<string> filterTexts)
        {
            var groups = new List<SelectionGroup>();
            var listBoxes = Dimensions.GetSelections(filterTexts);
            foreach (var listbox in listBoxes)
            {
                var newgroup = new SelectionGroup();
                RecursiveSelection(listBoxes, 0, newgroup, groups);
            }

            return groups;
        }
        #endregion
    }
}