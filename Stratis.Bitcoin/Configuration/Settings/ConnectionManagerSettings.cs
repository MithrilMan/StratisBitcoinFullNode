using System.Collections.Generic;
using System.Net;

namespace Stratis.Bitcoin.Configuration {
   public class ConnectionManagerSettings {
      public ConnectionManagerSettings() { }

      public List<IPEndPoint> Connect { get; set; } = new List<IPEndPoint>();

      public List<IPEndPoint> AddNode { get; set; } = new List<IPEndPoint>();

      public List<NodeServerEndpoint> Listen { get; set; } = new List<NodeServerEndpoint>();

      public IPEndPoint ExternalEndpoint { get; internal set; }

      /// <summary>
      /// specify if the node listen to outbound connections
      /// </summary>
      public bool IsListening { get; set; }

      /// <summary>
      /// Port used to listen
      /// </summary>
      public int Port { get; internal set; }
   }
}