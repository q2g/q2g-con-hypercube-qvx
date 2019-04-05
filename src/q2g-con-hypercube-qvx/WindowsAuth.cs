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
    using Newtonsoft.Json;
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.DirectoryServices.AccountManagement;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    #endregion

    public class WinAuth
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        public static bool Success;
        #endregion

        #region private methods
        private static bool ValidateWinCredentialsInternal(string username, string password, ContextType type)
        {
            try
            {
                using (PrincipalContext context = new PrincipalContext(type))
                {
                    return context.ValidateCredentials(username, password);
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool ValidateWinCredentials(string username, string password)
        {
            try
            {
                if (Success)
                    return true;

                bool result = false;
                result = ValidateWinCredentialsInternal(username, password, ContextType.Machine);
                if(!result)
                    result = ValidateWinCredentialsInternal(username, password, ContextType.Domain);
                return result;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}