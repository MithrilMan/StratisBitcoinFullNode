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
         return NodeSettings.GetArgs(new string[0]);
      }


      public static NodeSettings GetArgs(string[] args) {
         string configurationFile = args.Where(a => a.StartsWith("-conf=")).Select(a => a.Substring("-conf=".Length).Replace("\"", "")).FirstOrDefault();
         string datadir = args.Where(a => a.StartsWith("-datadir=")).Select(a => a.Substring("-datadir=".Length).Replace("\"", "")).FirstOrDefault();
         if (datadir != null && configurationFile != null) {
            if (!Directory.Exists(datadir)) {
               throw new ConfigurationException($"Data directory '{datadir}' does not exists");
            }

            var isRelativePath = Path.GetFullPath(configurationFile).Length > configurationFile.Length;
            if (isRelativePath) {
               configurationFile = Path.Combine(datadir, configurationFile);
            }
         }

         CommandLineArguments parsedArguments = new CommandLineArguments();
         parsedArguments.Load(args);

#if DEBUG
         if (parsedArguments[CommandLineArguments.Option.DebugSettings].HasValue()) {
            Logs.Configuration.LogInformation($"Arguments passed by command line: {string.Join(" ", args)}");
         }
#endif

         if (configurationFile != null) {
            if (!File.Exists(configurationFile)) {
               throw new ConfigurationException("Configuration file does not exists");
            }

            var fileArgs = ReadConfigurationFile(configurationFile);


#if DEBUG
            if (parsedArguments[CommandLineArguments.Option.DebugSettings].HasValue()) {
               Logs.Configuration.LogInformation($"Arguments read from file: {string.Join(System.Environment.NewLine, fileArgs.Select(r => $"{r.Key}{r.Value}"))}");
            }
#endif

            //merge file options to command line arguments
            foreach (var arg in fileArgs) {
               parsedArguments.AddOptionValue(arg.Key, arg.Value);
            }
         }

         NodeSettings settings = parsedArguments.GetSettings();

         //ensure to load the default configuration file if -conf not used
         if (settings.ConfigurationFile == null) {
            settings.ConfigurationFile = settings.GetDefaultConfigurationFile(settings.DataDir);
         }

         settings.Validate();

         var folder = new DataFolder(settings.DataDir);
         if (!Directory.Exists(folder.CoinViewPath)) {
            Directory.CreateDirectory(folder.CoinViewPath);
         }
         return settings;
      }



      private void Validate() {
         if (this.Testnet && this.RegTest) {
            throw new ConfigurationException("Invalid combination of -regtest and -testnet");
         }

         Logs.Configuration.LogInformation($"Data directory set to {this.DataDir}");
         Logs.Configuration.LogInformation($"Configuration file set to {this.ConfigurationFile}");
      }

      private static List<KeyValuePair<string, string>> ReadConfigurationFile(string configurationFile) {
         List<KeyValuePair<string, string>> detectedOptions = new List<KeyValuePair<string, string>>();

         var lines = File
        .ReadAllText(configurationFile)
        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

         int lineCount = -1;
         foreach (var l in lines) {
            lineCount++;
            var line = l.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) {
               continue;
            }
            var split = line.Split('=');
            if (split.Length == 0) {
               continue;
            }
            if (split.Length == 1) {
               throw new FormatException("Line " + lineCount + ": No value are set");
            }

            var key = split[0];
            if (!key.StartsWith("-")) {
               key = '-' + key;
            }
            var value = String.Join("=", split.Skip(1).ToArray());

            detectedOptions.Add(new KeyValuePair<string, string>(key, value));
         }

         return detectedOptions;
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

      private string GetDefaultConfigurationFile(string datadir) {
         var config = Path.Combine(datadir, "bitcoin.conf");
         Logs.Configuration.LogInformation("Configuration file set to " + config);
         if (!File.Exists(config)) {
            Logs.Configuration.LogInformation("Creating configuration file");

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("####RPC Settings####");
            builder.AppendLine("#Activate RPC Server (default: 0)");
            builder.AppendLine("#server=0");
            builder.AppendLine("#Where the RPC Server binds (default: 127.0.0.1 and ::1)");
            builder.AppendLine("#rpcbind=127.0.0.1");
            builder.AppendLine("#Ip address allowed to connect to RPC (default all: 0.0.0.0 and ::)");
            builder.AppendLine("#rpcallowedip=127.0.0.1");
            File.WriteAllText(config, builder.ToString());
         }
         return config;
      }

      public Network GetNetwork() {
         return Testnet ? Network.TestNet :
            RegTest ? Network.RegTest :
            Network.Main;
      }

      
      #endregion
   }
}
