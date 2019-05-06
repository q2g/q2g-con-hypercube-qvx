namespace TestClient
{
    #region Usings
    using enigma;
    using ImpromptuInterface;
    using Qlik.EngineAPI;
    using System;
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

            Session session = null;
            IDoc app = null;
            try
            {
                var config = new EnigmaConfigurations()
                {
                    Url = $"ws://172.30.1.125:9076/app/engineData",
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
                var appName = "/apps/Executive Dashboard Entwickler.qvf";
                Console.WriteLine($"Connect to App {appName}...");
                app = global.OpenDocAsync(appName).Result;

                var userid = "test";
                var password = "test";

                var connConfig = new Connection()
                {
                    qType = "testconnector",
                    qName = "testconn",
                    qConnectionString = $"CUSTOM CONNECT TO \"provider=testconnector;userid={userid};password={password};host=localhost;\""
                };
                Console.WriteLine($"Create connection2...");
                var connection = app.CreateConnectionAsync(connConfig).Result;
                Console.WriteLine($"Connection: {connection}");

                var sb = new StringBuilder();
                sb.Append("\nLIB CONNECT TO 'testconn';");
                sb.AppendLine("SQL SELECT \"Product\"");
                sb.AppendLine(",\"Inventory Turns\"");
                sb.AppendLine(",\"Inventory Amount\"");
                sb.AppendLine(",\"Margin % \"");
                sb.AppendLine("FROM [Executive Dashboard Entwickler.qvf].[JZMrdb];");
                
                Console.WriteLine($"Start Script...");
                app.SetScriptAsync(sb.ToString()).Wait();
                app.DoReloadAsync().Wait();

                Console.WriteLine($"Show Table...");
                var size = new Qlik.EngineAPI.Size();
                var tables = app.GetTablesAndKeysAsync(size, size, 0, false, false).Result;

                var table = tables.qtr.FirstOrDefault() ?? null;
                if(table != null)
                {
                    var tableSb = new StringBuilder();
                    tableSb.Append($"TableName: {table.qName}\n");
                    foreach (var field in table.qFields)
                        tableSb.AppendLine($"Field: {field.qName}");
                    tableSb.AppendLine($"Number of Rows: {table.qNoOfRows}");
                    Console.WriteLine($"Table:\n{tableSb.ToString()}");
                }
                else
                    Console.WriteLine($"No Table found...");

                Console.WriteLine($"Bereit...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error:\n{ex.ToString()}");
                Console.ReadLine();
            }
        }
    }
}