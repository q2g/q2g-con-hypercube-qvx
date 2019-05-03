namespace q2gconhypercubegrpc.Connection
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Cryptography;
    using System.IO;
    using Q2g.HelperPem;
    #endregion

    public class CookieConnectionOptions
    {
        #region Properties
        public string HeaderName { get; set; }
        public string HeaderValue { get; set; }
        public string CookieName { get; set; } = "X-Qlik-Session-ser";
        public bool UseCertificate { get; set; } = false;
        public string CertificatePath { get; set; }
        public string Password { get; set; }
        #endregion
    }
}