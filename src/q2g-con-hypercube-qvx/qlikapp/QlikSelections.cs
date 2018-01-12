﻿namespace QlikTableConnector.QlikApplication
{
    #region Usings
    using Qlik.Engine;
    using Qlik.Sense.Client;
    using Qlik.Sense.Client.Visualizations;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using QlikTableConnector;
    #endregion

    public class QlikSelections
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
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
    }
}