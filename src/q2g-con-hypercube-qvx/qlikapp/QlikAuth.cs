namespace QlikTableConnector.QlikApplication
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    #endregion

    public class QlikAuth
    {
        #region Properties & Variables
        public string DefaultFolder => @"C:\ProgramData\Qlik\Sense\Repository\Exported Certificates\.Local Certificates";
        #endregion

        #region Private Methods
        protected X509Certificate2 GetClientCertificate(string fullpath = null, string password = null)
        {
            if (String.IsNullOrEmpty(fullpath))
                fullpath = DefaultFolder;

            if (String.IsNullOrEmpty(password))
                password = Guid.NewGuid().ToString();

            X509Certificate2 cert = null;
            if (fullpath.ToLowerInvariant().EndsWith(".pfx") && File.Exists(fullpath))
            {
                cert = new X509Certificate2(fullpath, password);
            }
            else if(fullpath.ToLowerInvariant().EndsWith(".pem") && File.Exists(fullpath))
            {
                if (String.IsNullOrEmpty(password))
                    password = Guid.NewGuid().ToString();

                var certificate = new QlikClientCertificate(fullpath, password);
                cert = certificate.GetCertificateFromPEM();
            }
            else
            {
                var clientCertPath = Path.Combine(fullpath, "client.pem");
                var clientKeyPath = Path.Combine(fullpath, "client_key.pem");
                var certificate = new QlikClientCertificate(clientCertPath, clientKeyPath, password);
                cert = certificate.GetCertificateFromPEM();
            }

            return cert;
        }

        protected X509Certificate2 GetRootCertificate(string fullpath = null)
        {
            if (File.Exists(fullpath))
            {
                return new X509Certificate2(fullpath);
            }
            else
            {
                return new X509Certificate2(Path.Combine(DefaultFolder, "root.pem"));
            }
        }
        #endregion
    }
}
