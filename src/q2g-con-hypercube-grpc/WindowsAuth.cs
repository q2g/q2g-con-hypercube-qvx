namespace SSEDemo
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
                if (!result)
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