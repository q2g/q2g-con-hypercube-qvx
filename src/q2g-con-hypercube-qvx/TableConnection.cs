#region License
/*
Copyright (c) 2018 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace q2gconhypercubeqvx
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using NLog;
    using q2gconhypercubeqvx.Connection;
    using QlikView.Qvx.QvxLibrary;
    #endregion

    public class TableConnection : QvxConnection
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Variables
        private TableFunc tableFunctions;
        #endregion

        #region Init
        public override void Init()
        {
            tableFunctions = new TableFunc();
        }
        #endregion

        #region private methods
        private bool IsUsedField(string field, ScriptCode code)
        {
            if (code.Fields.Count == 0)
                return true;

            return code.Fields.IndexOf(field) > -1;
        }

        private QvxTable GetData(ScriptCode script, ConnectorParameter parameter)
        {
            q2gconhypercubeqvx.Connection.Connection connection = null;

            try
            {
                var config = QlikApp.CreateConfig(parameter, script.AppId);
                var qlikApp = new QlikApp(parameter);
                connection = qlikApp.CreateNewConnection(config);
                if(!connection.Connect())
                    return new QvxTable();

                foreach (var filter in script.Filter)
                {
                    logger.Debug($"Filter: {filter}");
                    foreach (var value in filter.Values)
                    {
                        var selection = new QlikSelections(connection.CurrentApp);
                        var result = selection.SelectValue(filter.Name, value);
                        if (result == false)
                        {
                            logger.Error($"The Dimension \"{filter.Name}\" could not found.");
                            return null;
                        }
                    }
                }

                var resultTable = tableFunctions.GetTableInfosFromApp($"Table_{script.AppId}_{script.ObjectId}", script, connection.CurrentApp);
                return resultTable.QvxTable;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The table script can not be executed.");
                return new QvxTable();
            }
            finally
            {
                connection?.Close();
            }
        }
        #endregion

        #region public methods
        public override QvxDataTable ExtractQuery(string query, List<QvxTable> tables)
        {
            try
            {
                logger.Debug($"Parse query {query}");
                var script = ScriptCode.Parse(query);
                if (script == null)
                    throw new Exception("The sql script is not valid.");

                var parameter = ConnectorParameter.Create(MParameters);
                var qvxTable = GetData(script, parameter);
                var result = new QvxDataTable(qvxTable);
                result.Select(qvxTable.Fields);
                logger.Debug($"Send result table {qvxTable.TableName}");
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The query could not be executed.");
                LogManager.Flush();
                return new QvxDataTable(new QvxTable() { TableName = "Error" });
            }
        }
        #endregion
    }
}