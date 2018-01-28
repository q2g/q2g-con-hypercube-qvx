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
    using System;
    using System.Collections.Generic;
    using System.DirectoryServices.AccountManagement;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    #endregion

    public class AppInstance
    {
        #region Logger
        private static ConnectorLogger logger = ConnectorLogger.CreateLogger();
        #endregion

        #region Varibales
        private static List<SessionMemory> sessionMem = new List<SessionMemory>();
        private static QlikApp activeApp;
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

        private static bool ValidateWinCredentials(string username, string password)
        {
            try
            {
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

        private static SessionMemory GetVaildSession(string username)
        {
            var session = sessionMem.FirstOrDefault(s => s.UserName == username && s.Id != null) ?? null;
            var timeResult = DateTime.Now - session?.Stamp;
            if (timeResult?.TotalMinutes <= 30)
            {
                return session;
            }

            return null;
        }
        #endregion

        #region public methods
        public static QlikApp GetQlikInstance(ConnectorParameter parameter, string appId = null)
        {
            try
            {
                IQlikCredentials qlikAuth = null;
                var connectUri = "Qlik Sense Desktop";
                if (!parameter.UseDesktop)
                {
                    connectUri = $"https://{parameter.ConnectUri}";
                    var session = GetVaildSession(parameter.UserName);
                    if (ValidateWinCredentials(parameter.UserName, parameter.Password))
                    {
                        connectUri = $"wss://{parameter.ConnectUri}:4747";
                        qlikAuth = new CertificateAuth(parameter.UserName, Environment.UserDomainName);
                    }
                    else if (session != null)
                        qlikAuth = new SessionAuth(session.Id);
                    else
                        qlikAuth = new WindowsAuth(parameter.UserName, parameter.Password);
                }

                activeApp = new QlikApp(appId, connectUri, qlikAuth);
                if (appId != null && activeApp.Connect())
                {
                    var session = sessionMem.FirstOrDefault(s => s.UserName == parameter.UserName) ?? null;
                    if(session == null)
                    {
                        sessionMem.Add(new SessionMemory()
                        {
                            UserName = parameter.UserName,
                            Id = activeApp.FirstSession.SessionId,
                            Stamp = DateTime.Now,
                        });
                    }
                    else
                    {
                        session.Id = activeApp.FirstSession.SessionId;
                        session.Stamp = DateTime.Now;
                        var index = sessionMem.IndexOf(session);
                        sessionMem[index] = session;
                    }
                }
                else
                {
                    if (!String.IsNullOrEmpty(appId))
                        throw new Exception($"No connection with app {appId}");
                }

                return activeApp;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "app connection failed");
                return null;
            }
        }

        public static void LoadMemory()
        {
            var loadPath = String.Empty;
            try
            {
                loadPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "session.json");
                if (File.Exists(loadPath))
                {
                    var json = File.ReadAllText(loadPath);
                    sessionMem = JsonConvert.DeserializeObject<List<SessionMemory>>(json);
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(loadPath))
                {
                    try { File.Delete(loadPath); } catch { }
                }
                logger.Error(ex, "session memory could not load.");
            }
        }

        public static void SaveMemory()
        {
            try
            {
                var json = JsonConvert.SerializeObject(sessionMem);
                var savePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "session.json");
                File.WriteAllText(savePath, json);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "session memory could not save.");
            }
        }

        public static void Dispose()
        {
            activeApp?.Dispose();
        }
        #endregion
    }

    #region helper classes
    public class SessionMemory
    {
        public string UserName { get; set; }
        public string Id { get; set; }
        public DateTime Stamp { get; set; }
    }

    public class ConnectorParameter
    {
        private ConnectorParameter(bool useDesktop, string host, string userName, string password)
        {
            UseDesktop = useDesktop;
            ConnectUri = host;
            UserName = userName;
            Password = password;
        }

        public static ConnectorParameter Create(Dictionary<string, string> MParameters)
        {
            MParameters.TryGetValue("host", out string host);
            MParameters.TryGetValue("isDesktop", out string isDesktop);
            MParameters.TryGetValue("UserId", out string user);
            MParameters.TryGetValue("Password", out string password);
            return new ConnectorParameter(isDesktop.ToLowerInvariant() == "true",host, user, password);
        }
            
        public bool UseDesktop { get; set; }
        public string ConnectUri { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
    #endregion
}