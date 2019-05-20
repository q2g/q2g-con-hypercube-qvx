namespace q2gconhypercubemain
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Text;
    #endregion

    public enum DataType
    {
        TEXT,
        NUMBER,
        DATE
    }


    public class ResultTable
    {
        public string Name { get; set; }
        public List<ResultHeader> Headers { get; set; } = new List<ResultHeader>();
        public List<ResultRow> Rows { get; set; } = new List<ResultRow>();
    }

    public class ResultRow
    {
        public string Value { get; set; }
        public double Num { get; set; }
        public string Header { get; set; }
    }

    public class ResultHeader
    {
        public string Name { get; set; }
        public DataType Type { get; set; }
    }
}
