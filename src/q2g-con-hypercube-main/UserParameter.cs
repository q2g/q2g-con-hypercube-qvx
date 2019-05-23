namespace q2gconhypercubemain
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

        public static UserParameter Create(Dictionary<string, string> MParameters)
        {
            string host = "";
            string isDesktop = "true";
            string user = "";
            string password = "";
            MParameters?.TryGetValue("host", out host);
            MParameters?.TryGetValue("isDesktop", out isDesktop);
            MParameters?.TryGetValue("UserId", out user);
            MParameters?.TryGetValue("Password", out password);
            host = host ?? "";
            isDesktop = isDesktop ?? "true";
            user = user ?? "";
            password = password ?? "";

            if (host == "localhost" && isDesktop == "true")
                host = "ws://localhost:4848";

            return new UserParameter()
            {
                UseDesktop = Boolean.Parse(isDesktop.ToLowerInvariant()),
                ConnectUri = host,
                Password = password,
                UserName = user
            };
        }
    }
}
