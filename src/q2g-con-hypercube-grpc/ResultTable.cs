namespace q2gconhypercubegrpc
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
        public string Name { get; set;}
        public List<ResultHeader> Headers { get; set; }
        public List<ResultRow> Rows { get; set; }
    }

    public class ResultRow
    {
        public object Value { get; set; }
    }

    public class ResultHeader
    {
        public string Name { get; set; }
        public DataType Type { get; set; }
    }
}
