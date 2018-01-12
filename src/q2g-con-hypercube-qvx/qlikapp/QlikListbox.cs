namespace QlikTableConnector.QlikApplication
{
    #region Usings
    using Qlik.Engine;
    using Qlik.Sense.Client.Visualizations;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using QlikTableConnector;
    #endregion

    public class QlikListbox
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
        #endregion

        #region Properties & Variables
        private Listbox Selection { get; set; }
        private int CurrentIndex { get; set; }
        private IApp SenseApp { get; set; }
        private string FilterText { get; }

        public Guid Id { get; }
        public string FirstName
        {
            get
            {
                var values = GetValues();
                if (values.Count > 0)
                    return values[0];

                return String.Empty;
            }
        }
        #endregion

        public QlikListbox(string filtertext, IApp senseApp)
        {
            SenseApp = senseApp;
            FilterText = filtertext;
            Selection = CreateSession();
            Selection.GetLayout();
            //Selection.Changed += Selection_Changed;
            CurrentIndex = -1;
            Id = Guid.NewGuid();
        }

        //private void Selection_Changed(object sender, EventArgs e)
        //{
        //    Listbox listbox = null;
        //    ListboxLayout listObj = null;
        //    var count = 0;

        //    try
        //    {
        //        listbox = sender as Listbox;
        //        listObj = listbox.GetLayout() as ListboxLayout;
        //        count = listObj?.ListObject?.DimensionInfo?.Cardinal ?? 0;
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error(ex);
        //    }
        //}

        private Listbox CreateSession()
        {
            var session = SenseApp.CreateGenericSessionObject(
            new ListboxProperties()
            {
                Info = new NxInfo() { Type = "listbox" },
                ListObjectDef = new ListboxListObjectDef
                {
                    InitialDataFetch = new NxPage[] { new NxPage() { Height = 0, Top = 0 } },
                    Def = new ListboxListObjectDimensionDef()
                    {
                        FieldDefs = new List<string>() { FilterText },
                        FieldLabels = new List<string>() { Guid.NewGuid().ToString() },
                        SortCriterias = new List<SortCriteria> { new SortCriteria() { SortByState = SortDirection.Ascending } },
                    },
                    ShowAlternatives = false,
                },
            });

            return session as Listbox;
        }

        private List<NxCell> GetFieldCells()
        {
            var listData = Selection.GetListObjectData("/qListObjectDef", new List<NxPage>() { new NxPage() { Left = 0, Top = 0, Height = Selection.DimensionInfo.Cardinal, Width = 1 } });
            var dataPage = listData.SingleOrDefault();
            if (dataPage != null)
            {
                var results = dataPage.Matrix.Select(i => i.First()).Where(s => s.State != StateEnumType.EXCLUDED).ToList();
                return results;
            }

            return new List<NxCell>();
        }

        private List<int> GetAllIndecs(IList<NxCell> cells)
        {
            var results = cells.ToList().Select(c => c.ElemNumber).ToList();
            return results;
        }

        private List<int> GetIndecs(IList<NxCell> cells, IList<string> values)
        {
            if (values == null)
            {
                logger.Debug("Selection: The filter values are null.");
                return null;
            }

            var count = cells.Where(c => values.Contains(c.Text)).Count();
            if (count == 0)
            {
                logger.Debug("Selection: The filter values not exists.");
                return null;
            }

            var results = cells.Where(c => values.Contains(c.Text)).Select(e => e.ElemNumber).ToList();
            return results;
        }

        private void SelectValuesInternal(List<int> indecs)
        {
            Selection.BeginSelections();
            Selection.SelectValues(indecs, true);
            Selection.EndSelections(true);
        }

        private NxCell GetFieldCellFromIndex(int index)
        {
            var dataPages = Selection.GetData(new NxPage[] { new NxPage() { Height = Selection.DimensionInfo.Cardinal, Top = 0 } });
            var firstPage = dataPages.FirstOrDefault() ?? null;
            if (firstPage != null)
            {
                var matrix = firstPage.Matrix.ToList();
                if (index < matrix.Count)
                {
                    return matrix[index].FirstOrDefault() ?? null;
                }
            }

            return null;
        }

        public void ClearSelections()
        {
            Selection.ClearSelections();
        }

        public void SelectValues(List<int> indecs)
        {
            SelectValuesInternal(indecs);
        }

        public void SelectValues(List<string> values)
        {
            var cells = GetFieldCells();
            var indecs = GetIndecs(cells, values);
            if (indecs != null)
                SelectValuesInternal(indecs);
        }

        public List<string> GetValues()
        {
            var cells = GetFieldCells();
            return cells.Select(s => s.Text).ToList();
        }

        public List<string> GetValuesSort()
        {
            var values = GetValues();
            values.Sort();
            return values;
        }

        public void SelectAll()
        {
            try
            {
                Selection.SelectPossible();
            }
            catch (Exception ex)
            {
                throw new Exception("The selection of all items could not work.", ex);
            }
        }

        public FlatSelection GetNextSelection()
        {
            try
            {
                Selection.ClearSelections();

                CurrentIndex++;
                var listObj = Selection.GetLayout() as ListboxLayout;
                var count = listObj?.ListObject?.DimensionInfo?.Cardinal ?? 0;
                if ((CurrentIndex >= count))
                {
                    CurrentIndex = -1;
                    return null;
                }

                var cell = GetFieldCellFromIndex(CurrentIndex);
                if (cell != null)
                {
                    if (cell.State == StateEnumType.EXCLUDED)
                        return null;
                }

                SelectValuesInternal(new List<int> { cell.ElemNumber });

                return new FlatSelection(FilterText, cell.Text, cell.ElemNumber, cell.State);
            }
            catch (Exception ex)
            {
                throw new Exception("The next selection could not be set.", ex);
            }
        }
    }

    public class SelectionGroup
    {
        public SelectionGroup()
        {
            FlatSelections = new List<FlatSelection>();
            Id = Guid.NewGuid();
        }

        public List<FlatSelection> FlatSelections { get; }
        public Guid Id { get; }

        public string GetFlatValues()
        {
            var sb = new StringBuilder();
            foreach (var sel in FlatSelections)
                sb.Append($"{sel.Name}:{sel.Value}={sel.ElementNumber}/{sel.State.ToString()} \r\n");
            
            return sb.ToString().Trim();
        }
    }

    public class FlatSelection
    {
        public FlatSelection(string name, string value, int number, StateEnumType state)
        {
            Name = name;
            Value = value;
            ElementNumber = number;
            State = state;
        }

        public string Name { get; }
        public string Value { get; }
        public int ElementNumber { get; }
        public StateEnumType State { get; }

        public override string ToString()
        {
            return $"{Value}={ElementNumber}";
        }
    }
}
