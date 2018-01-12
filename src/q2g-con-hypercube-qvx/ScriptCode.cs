#region License
/*
Copyright (c) 2017 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace QlikTableConnector
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;
    #endregion

    public class ScriptCode
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
        #endregion

        #region Variables & Properties
        public string Script { get; private set; }
        public string AppId { get; private set; }
        public string TableId { get; private set; }
        public string MasterId { get; private set; }
        public List<string> Fields { get; private set; }
        public List<SQLFilter> Filter { get; private set; }
        #endregion

        #region Constructor & Load
        private ScriptCode(string script)
        {
            Script = script;
            Fields = new List<string>();
            Filter = new List<SQLFilter>();
        }
        #endregion

        #region Static Methods
        public static ScriptCode Parse(string script)
        {
            try
            {
                var resultScript = new ScriptCode(script);
                if (resultScript.Read())
                    return resultScript;

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception("The script is not valid.", ex);
            }
        }
        #endregion

        #region Methods
        private Match GetMatch(string regex)
        {
            var formatScript = Script.Replace("\r\n", " ").Replace("\"","").Trim();
            var match = Regex.Match(formatScript, regex, RegexOptions.IgnoreCase);
            if (match.Success)
                return match;
            return null;
        }

        private bool Read()
        {
            try
            {
                if (String.IsNullOrEmpty(Script))
                {
                    logger.Warn("The script is empty.");
                    return false;
                }

                var match = GetMatch("select (.*) from \\[(.+)\\]\\.\\[(.+)\\]");
                if (match != null)
                {
                    var fieldValues = match.Groups[1].Value.Trim().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    if (fieldValues.Length > 0 && fieldValues[0] != "*")
                    {
                        foreach (var fvalue in fieldValues)
                            Fields.Add(fvalue.Trim());
                    }

                    AppId = match.Groups[2].Value.Trim();
                    TableId = match.Groups[3].Value.Trim();
                }

                match = GetMatch("where (.*)$");
                if (match != null)
                {
                    var statements = match.Groups[1].Value.Split(new string[] { "and", "AND" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var statement in statements)
                    {
                        var split = statement.Trim().Split('=');
                        Filter.Add(new SQLFilter { Name = split[0].Trim(), Value = split[1].Trim() });
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The script could not be read.");
                return false;
            }
        }
        #endregion
    }

    public class SQLFilter
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}