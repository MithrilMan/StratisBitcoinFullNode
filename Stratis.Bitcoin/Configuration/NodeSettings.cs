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
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration.JsonConverters;

namespace Stratis.Bitcoin.Configuration {
   public class NodeSettings {
      const int DEFAULT_MAX_TIP_AGE = 24 * 60 * 60;

      public RPCSettings RPC { get; set; }

      public CacheSettings Cache { get; set; } = new CacheSettings();

      public ConnectionManagerSettings ConnectionManager { get; set; } = new ConnectionManagerSettings();

      public MemPoolSettings MemPool { get; set; } = new MemPoolSettings();

      public bool Testnet { get; set; }

      public string DataDir { get; set; }

      public bool RegTest { get; set; }

      public string ConfigurationFile { get; set; }

      public bool Prune { get; set; }

      public bool RequireStandard { get; set; }

      public int MaxTipAge { get; set; }

      public static NodeSettings Default() {
         return NodeSettings.Load(new string[0]);
      }


      public static NodeSettings Load(string[] args) {
         var app = new CommandLineApplication(throwOnUnexpectedArg: true) {
            Name = "BitcoinD",
            Description = "BitcoindD stratis implementation",
            FullName = "BitcoinD full node implementation"
         };

         app.HelpOption("-? | -h | --help");
         var rootCommand = new Commands.RootCommand(app);

         try {
            Logs.Configuration.LogInformation($"Arguments read from command line:{System.Environment.NewLine}{string.Join(", ", args)}");

            var result = app.Execute(args);


            //if some error happens, return a null NodeSettings
            if (result != 0) {
               return null;
            }
         }
         catch (CommandParsingException ex) {
            Console.WriteLine(ex.Message);
            app.ShowHelp();
            return null;
         }



         return rootCommand.NodeSettings;
      }


      internal string ToJsonString() {
         JsonSerializerSettings settings = new JsonSerializerSettings();
         settings.Converters.Add(new IPAddressConverter());
         settings.Converters.Add(new IPEndPointConverter());
         settings.Formatting = Formatting.Indented;

         return JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented, settings);
      }


      #region Helpers
      public static IPEndPoint ConvertToEndpoint(string str, int defaultPort) {
         var portOut = defaultPort;
         var hostOut = "";
         int colon = str.LastIndexOf(':');
         // if a : is found, and it either follows a [...], or no other : is in the string, treat it as port separator
         bool fHaveColon = colon != -1;
         bool fBracketed = fHaveColon && (str[0] == '[' && str[colon - 1] == ']'); // if there is a colon, and in[0]=='[', colon is not 0, so in[colon-1] is safe
         bool fMultiColon = fHaveColon && (str.LastIndexOf(':', colon - 1) != -1);
         if (fHaveColon && (colon == 0 || fBracketed || !fMultiColon)) {
            int n;
            if (int.TryParse(str.Substring(colon + 1), out n) && n > 0 && n < 0x10000) {
               str = str.Substring(0, colon);
               portOut = n;
            }
         }
         if (str.Length > 0 && str[0] == '[' && str[str.Length - 1] == ']')
            hostOut = str.Substring(1, str.Length - 2);
         else
            hostOut = str;
         return new IPEndPoint(IPAddress.Parse(str), portOut);
      }

      public Network GetNetwork() {
         return Testnet ? Network.TestNet :
            RegTest ? Network.RegTest :
            Network.Main;
      }


      #endregion
   }
}
