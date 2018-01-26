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
        public string ObjectId { get; private set; }
        public List<string> Fields { get; private set; }
        public List<SQLFilter> Filter { get; private set; }
        public bool Full { get; private set; }
        #endregion

        #region Constructor & Load
        private ScriptCode(string script)
        {
            Script = script;
            Fields = new List<string>();
            Filter = new List<SQLFilter>();
            Full = true;
        }

        private ScriptCode(string appId, string objectId)
        {
            AppId = appId;
            ObjectId = objectId;
            Full = false;
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

        public static ScriptCode Create(string appId, string objectId)
        {
            try
            {
                return new ScriptCode(appId, objectId);
            }
            catch (Exception ex)
            {
                throw new Exception("The script parameter are not valid.", ex);
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

        private string[] SplitFirst(string value)
        {
            var results = new List<string>();
            value = value.Trim();
            var index = value.IndexOf("=");
            if (index > -1)
            {
                results.Add(value.Substring(0, index).Trim('\"'));
                results.Add(value.Substring(index + 1, value.Length - index - 1).Trim('\"'));
            }
            else
                results.Add(value);
            return results.ToArray();
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
                    ObjectId = match.Groups[3].Value.Trim();
                }

                match = GetMatch("where (.*)$");
                if (match != null)
                {
                    var whereValue = match.Groups[1].Value;
                    if (whereValue.ToLowerInvariant().Contains(" or "))
                        throw new Exception("The \"OR\" operator is not allowed in a where statement.");

                    var statements = whereValue.Split(new string[] { "and", "AND" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var statement in statements)
                    {
                        var split = SplitFirst(statement);
                        if (Filter.Exists(s => s.Name == split[0]))
                            throw new Exception($"The same dimension \"{split[0]}\" is not allowed in a where statement.");
                        var sqlFilter = new SQLFilter(split[0]);
                        var values = split[1].Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var value in values)
                        {
                            if (value.StartsWith("="))
                                sqlFilter.IsFormula = true;

                            sqlFilter.Values.Add(value.Trim());
                        }
                            
                        Filter.Add(sqlFilter);
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

    #region helper classes
    public class SQLFilter
    {
        public SQLFilter(string name)
        {
            Name = name;
            Values = new List<string>();
        }

        public string Name { get; private set; }
        public List<string> Values { get; private set; }
        public bool IsFormula { get; set; }
    }
    #endregion
}