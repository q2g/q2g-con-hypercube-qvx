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
    using Newtonsoft.Json;
    using NLog;
    using System;
    using System.IO;
    #endregion

    #region Enumerations
    public enum QlikCredentialType
    {
        WINDOWSAUTH,
        CERTIFICATE,
        SESSION
    }
    #endregion

    #region Interfaces
    public interface IQlikCredentials
    {
        QlikCredentialType Type { get; }
    }
    #endregion

    public class CertificateAuth : IQlikCredentials
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        [JsonProperty(nameof(CertificatePath))]
        public string CertificatePath { get; private set; }

        [JsonProperty(nameof(UserId))]
        public string UserId { get; private set; }

        [JsonProperty(nameof(UserDirectory))]
        public string UserDirectory { get; private set; }

        [JsonIgnore]
        public string Password { get; private set; }

        [JsonIgnore]
        public QlikCredentialType Type { get; } = QlikCredentialType.CERTIFICATE;
        #endregion

        #region Constructur & Init
        public CertificateAuth(string userId, string userDirectory, string certificateFolder = null, string workingDir = null, string password = null)
        {
            CertificatePath = certificateFolder;
            UserId = userId;
            UserDirectory = userDirectory;
            Read(workingDir);
        }

        private void Read(string workingDir)
        {
            try
            {
                if (File.Exists(CertificatePath) || Directory.Exists(CertificatePath) || String.IsNullOrEmpty(CertificatePath))
                    return;
                else if (File.Exists(Path.Combine(workingDir, CertificatePath)))
                    CertificatePath = Path.Combine(workingDir, CertificatePath);
                else if (Directory.Exists(Path.Combine(workingDir, CertificatePath)))
                    CertificatePath = Path.Combine(workingDir, CertificatePath);
                else
                    throw new Exception($"The certificate {CertificatePath} was not found.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The certificate {CertificatePath} can not be load.");
            }
        }
        #endregion
    }

    public class WindowsAuth : IQlikCredentials
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties & Variables
        [JsonProperty(nameof(Login))]
        public string Login { get; private set; }
        [JsonProperty(nameof(Password))]
        public string Password { get; private set; }
        public QlikCredentialType Type { get; } = QlikCredentialType.WINDOWSAUTH;
        #endregion

        #region Constructor
        public WindowsAuth(string login, string password)
        {
            Login = login;
            Password = password;
        }
        #endregion
    }

    public class SessionAuth : IQlikCredentials
    {
        #region Properties & Variables
        public QlikCredentialType Type { get; } = QlikCredentialType.SESSION;
        [JsonProperty(nameof(SessionId))]
        public string SessionId { get; private set; }
        [JsonProperty(nameof(CookieName))]
        public string CookieName { get; private set; }
        #endregion

        #region Constructor
        public SessionAuth(string sessionId, string cookieName = "X-Qlik-Session")
        {
            SessionId = sessionId;
            CookieName = cookieName;
        }
        #endregion
    }
}