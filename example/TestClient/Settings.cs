using System;
using System.Collections.Generic;
using System.Text;

namespace TestClient
{
    public class Settings
    {
        public Uri ServerUri { get; set; }
        public string App { get; set; }
        public List<string> Scripts { get; set; }
        public List<QlikConnection> Connections { get; set; }
    }

    public class QlikConnection
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string ConnectionString { get; set; }
    }
}
