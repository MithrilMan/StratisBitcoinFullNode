using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool;

namespace Stratis.Bitcoin.Configuration {
   public class RPCSettings {
      public RPCSettings() {
         Bind = new List<IPEndPoint>();
         AllowIp = new List<IPAddress>();
      }

      public string RpcUser { get; set; }
      public string RpcPassword { get; set; }

      public int RPCPort { get; set; }
      public List<IPEndPoint> Bind { get; set; }

      public List<IPAddress> AllowIp {
         get; set;
      }

      public string[] GetUrls() {
         return Bind.Select(b => "http://" + b + "/").ToArray();
      }
   }
}
