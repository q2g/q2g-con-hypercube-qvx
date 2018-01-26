#region License
/*
Copyright (c) 2017 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace q2gconhypercubeqvx.QlikApplication
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
