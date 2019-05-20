namespace TestClient
{
    #region Usings
    using enigma;
    using ImpromptuInterface;
    using Newtonsoft.Json;
    using Qlik.EngineAPI;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Net.WebSockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    #endregion

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting...");
            Console.WriteLine("Read Settings...");

            var content = File.ReadAllText("settings.json");
            var settings = JsonConvert.DeserializeObject<Settings>(content);

            Session session = null;
            IDoc app = null;
            try
            {
                var config = new EnigmaConfigurations()
                {
                    Url = settings.ServerUri.AbsoluteUri,
                    CreateSocket = async (url) =>
                    {
                        var ws = new ClientWebSocket();
                        await ws.ConnectAsync(new Uri(url), CancellationToken.None);
                        return ws;
                    }
                };

                session = Enigma.Create(config);
                var globalTask = session.OpenAsync();
                globalTask.Wait();
                IGlobal global = Impromptu.ActLike<IGlobal>(globalTask.Result);
                var appName = settings.App;
                Console.WriteLine($"Connect to App {appName}...");
                app = global.OpenDocAsync(appName).Result;

                Console.WriteLine($"Create Connections...");
                foreach (var qconn in settings.Connections)
                {
                    try
                    {
                        var connConfig = new Connection()
                        {
                            qType = qconn.Type,
                            qName = qconn.Name,
                            qConnectionString = qconn.ConnectionString
                        };
                        Console.WriteLine($"Create connection...");
                        var test = app.CreateConnectionAsync(connConfig).Result;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"No Connection created: {ex.ToString()}");
                    }
                }

                foreach (var scriptPath in settings.Scripts)
                {
                    var scriptContent = File.ReadAllText(scriptPath);
                    Console.WriteLine($"Reload Script...");
                    app.SetScriptAsync(scriptContent).Wait();
                    app.DoReloadAsync().Wait();
                }

                Console.WriteLine($"Finish.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error:\n{ex.ToString()}");
                Console.ReadLine();
            }
        }
    }
}