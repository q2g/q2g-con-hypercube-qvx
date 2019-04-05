namespace q2gconhypercubeqvx.Connection
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    #endregion

    public class ConnectorParameter
    {
        #region Properties
        public bool UseDesktop { get; set; }
        public string ConnectUri { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        #endregion

        private ConnectorParameter(bool useDesktop, string host, string userName, string password)
        {
            UseDesktop = useDesktop;
            ConnectUri = host;
            UserName = userName;
            Password = password;
        }

        public static ConnectorParameter Create(Dictionary<string, string> MParameters)
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
            return new ConnectorParameter(isDesktop.ToLowerInvariant() == "true", host, user, password);
        }
    }
}