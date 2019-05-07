namespace q2gconhypercubegrpc
{
    #region Usings
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    #endregion

    public class UserParameter
    {
        #region Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        public string ConnectUri { get; set; } = "ws://localhost:4848";
        public string UserName { get; set; } = String.Empty;
        public string Password { get; set; } = String.Empty;
        public bool UseDesktop { get; set; } = true;
        #endregion

        public static UserParameter Create(string connectionString)
        {
            try
            {
                var result = new UserParameter();
                var split = connectionString.Split(';');
                foreach (var item in split)
                {
                    if (item.ToLowerInvariant().StartsWith("userid"))
                        result.UserName = item.Split('=').ElementAtOrDefault(1) ?? String.Empty;
                    if (item.ToLowerInvariant().StartsWith("password"))
                        result.Password = item.Split('=').ElementAtOrDefault(1) ?? String.Empty;
                    if (item.ToLowerInvariant().StartsWith("url"))
                        result.ConnectUri = item.Split('=').ElementAtOrDefault(1) ?? String.Empty;
                    if (item.ToLowerInvariant().StartsWith("isdesktop"))
                        result.UseDesktop = Convert.ToBoolean(item.Split('=').ElementAtOrDefault(1).ToLowerInvariant());
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not parse connection string.");
                return null;
            }
        }
    }
}
