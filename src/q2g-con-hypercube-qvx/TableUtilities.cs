using q2gconhypercubemain;
using QlikView.Qvx.QvxLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace q2gconhypercubeqvx
{
    public class TableUtilities
    {
        public static QvxTable ConvertTable(ResultTable table)
        {
            var resultTable = new QvxTable()
            {
                TableName = table.Name
            };
            var fields = new List<QvxField>();
            foreach (var header in table.Headers)
                fields.Add(new QvxField(header.Name, QvxFieldType.QVX_TEXT, QvxNullRepresentation.QVX_NULL_FLAG_SUPPRESS_DATA, FieldAttrType.ASCII));
            var rows = new List<QvxDataRow>();
            foreach (var row in table.Rows)
            {
                var newRow = new QvxDataRow();
                var field = fields.FirstOrDefault(f => f.FieldName == row.Header);
                newRow[field] = row.Value;
                rows.Add(newRow);
            }
            resultTable.Fields = fields.ToArray();
            resultTable.GetRows = () => { return rows; };
            return resultTable;
        }
    }
}
