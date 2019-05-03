namespace q2gconhypercubegrpc.Connection
{
    #region Usings
    using System;
    using NLog;
    #endregion

    #region enums
    public enum QlikCredentialType
    {
        NONE,
        WINDOWSAUTH,
        CERTIFICATE,
        SESSION,
        JWT,
        HEADER
    }
    #endregion

    public class ConnectionConfig
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Variables && Properties
        private Uri privateURI;
        public Uri ServerUri
        {
            get
            {
                try
                {
                    var newUri = new UriBuilder(privateURI);
                    switch (newUri.Scheme.ToLowerInvariant())
                    {
                        case "ws":
                            newUri.Scheme = "http";
                            break;
                        case "wss":
                            newUri.Scheme = "https";
                            break;
                    }
                    return newUri.Uri;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"The server uri {privateURI} is invalid.");
                    return null;
                }
            }
            set
            {
                if (value != privateURI)
                {
                    privateURI = value;
                    var uri = privateURI.OriginalString.TrimEnd('/');
                    privateURI = new Uri(uri);
                }
            }
        }
        public string App { get; set; }
        public ConnCredentials Credentials { get; set; }
        #endregion

        public override string ToString()
        {
            return $"{ServerUri}";
        }
    }

    public class ConnCredentials
    {
        #region Properties
        public QlikCredentialType Type { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Cert { get; set; }
        public string PrivateKey { get; set; }
        #endregion
    }
}