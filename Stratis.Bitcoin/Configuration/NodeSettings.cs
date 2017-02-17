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

      public MempoolSettings Mempool { get; set; } = new MempoolSettings();

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
            var isRelativePath = Path.GetFullPath(configurationFile).Length > configurationFile.Length;
            if (isRelativePath) {
               configurationFile = Path.Combine(datadir, configurationFile);
            }
         }

         CommandLineArguments parsedArguments = new CommandLineArguments();
         parsedArguments.Load(args);

         if (configurationFile != null) {
            if (!File.Exists(configurationFile)) {
               throw new ConfigurationException("Configuration file does not exists");
            }

            var fileArgs = ReadConfigurationFile(configurationFile);

            //merge file options to command line arguments
            foreach (var arg in fileArgs) {
               parsedArguments.AddOptionValue(arg.Key, arg.Value);
            }
         }

         NodeSettings nodeArgs = parsedArguments.GetSettings();

         if (nodeArgs.ConfigurationFile == null) {
            nodeArgs.ConfigurationFile = nodeArgs.GetDefaultConfigurationFile();
         }

         if (nodeArgs.DataDir == null) {
            var network = nodeArgs.GetNetwork();
            nodeArgs.DataDir = GetDefaultDataDir("stratisbitcoin", network);
         }

         nodeArgs.Validate();

         try {
            nodeArgs.ConnectionManager.Connect.AddRange(config.GetAll("connect")
               .Select(c => ConvertToEndpoint(c, network.DefaultPort)));
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid connect parameter");
         }

         try {
            nodeArgs.ConnectionManager.AddNode.AddRange(config.GetAll("addnode")
                  .Select(c => ConvertToEndpoint(c, network.DefaultPort)));
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid addnode parameter");
         }

         var port = config.GetOrDefault<int>("port", network.DefaultPort);
         try {
            nodeArgs.ConnectionManager.Listen.AddRange(config.GetAll("listen")
                  .Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), false)));
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid listen parameter");
         }

         try {
            nodeArgs.ConnectionManager.Listen.AddRange(config.GetAll("whitebind")
                  .Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), true)));
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid listen parameter");
         }

         if (nodeArgs.ConnectionManager.Listen.Count == 0) {
            nodeArgs.ConnectionManager.Listen.Add(new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port), false));
         }

         var externalIp = config.GetOrDefault<string>("externalip", null);
         if (externalIp != null) {
            try {
               nodeArgs.ConnectionManager.ExternalEndpoint = ConvertToEndpoint(externalIp, port);
            }
            catch (FormatException) {
               throw new ConfigurationException("Invalid externalip parameter");
            }
         }

         if (nodeArgs.ConnectionManager.ExternalEndpoint == null) {
            nodeArgs.ConnectionManager.ExternalEndpoint = new IPEndPoint(IPAddress.Loopback, network.DefaultPort);
         }

         nodeArgs.Mempool.Load(config);

         var folder = new DataFolder(nodeArgs.DataDir);
         if (!Directory.Exists(folder.CoinViewPath))
            Directory.CreateDirectory(folder.CoinViewPath);
         return nodeArgs;
      }

      private void Validate() {
         if (this.Testnet && this.RegTest) {
            throw new ConfigurationException("Invalid combination of -regtest and -testnet");
         }

         Logs.Configuration.LogInformation($"Data directory set to {this.DataDir}");
         Logs.Configuration.LogInformation($"Configuration file set to {this.ConfigurationFile}");

         if (!Directory.Exists(this.DataDir)) {
            throw new ConfigurationException("Data directory does not exists");
         }
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

      [Obsolete]
      public static NodeSettings GetArgs_Old(string[] args) {
         NodeSettings nodeArgs = new NodeSettings();
         nodeArgs.ConfigurationFile = args.Where(a => a.StartsWith("-conf=")).Select(a => a.Substring("-conf=".Length).Replace("\"", "")).FirstOrDefault();
         nodeArgs.DataDir = args.Where(a => a.StartsWith("-datadir=")).Select(a => a.Substring("-datadir=".Length).Replace("\"", "")).FirstOrDefault();
         if (nodeArgs.DataDir != null && nodeArgs.ConfigurationFile != null) {
            var isRelativePath = Path.GetFullPath(nodeArgs.ConfigurationFile).Length > nodeArgs.ConfigurationFile.Length;
            if (isRelativePath) {
               nodeArgs.ConfigurationFile = Path.Combine(nodeArgs.DataDir, nodeArgs.ConfigurationFile);
            }
         }
         nodeArgs.Testnet = args.Contains("-testnet", StringComparer.CurrentCultureIgnoreCase);
         nodeArgs.RegTest = args.Contains("-regtest", StringComparer.CurrentCultureIgnoreCase);

         if (nodeArgs.ConfigurationFile != null) {
            AssetConfigFileExists(nodeArgs);
            var configTemp = TextFileConfiguration.Parse(File.ReadAllText(nodeArgs.ConfigurationFile));
            nodeArgs.Testnet = configTemp.GetOrDefault<bool>("testnet", false);
            nodeArgs.RegTest = configTemp.GetOrDefault<bool>("regtest", false);
         }

         if (nodeArgs.Testnet && nodeArgs.RegTest)
            throw new ConfigurationException("Invalid combination of -regtest and -testnet");

         var network = nodeArgs.GetNetwork();
         if (nodeArgs.DataDir == null) {
            nodeArgs.DataDir = GetDefaultDataDir("stratisbitcoin", network);
         }

         if (nodeArgs.ConfigurationFile == null) {
            nodeArgs.ConfigurationFile = nodeArgs.GetDefaultConfigurationFile();
         }

         Logs.Configuration.LogInformation("Data directory set to " + nodeArgs.DataDir);
         Logs.Configuration.LogInformation("Configuration file set to " + nodeArgs.ConfigurationFile);

         if (!Directory.Exists(nodeArgs.DataDir))
            throw new ConfigurationException("Data directory does not exists");

         var consoleConfig = new TextFileConfiguration(args);
         var config = TextFileConfiguration.Parse(File.ReadAllText(nodeArgs.ConfigurationFile));
         consoleConfig.MergeInto(config);

         nodeArgs.Prune = config.GetOrDefault("prune", 0) != 0;
         nodeArgs.RequireStandard = config.GetOrDefault("acceptnonstdtxn", !(nodeArgs.RegTest || nodeArgs.Testnet));
         nodeArgs.MaxTipAge = config.GetOrDefault("maxtipage", DEFAULT_MAX_TIP_AGE);

         nodeArgs.RPC = config.GetOrDefault<bool>("server", false) ? new RPCSettings() : null;
         if (nodeArgs.RPC != null) {
            nodeArgs.RPC.RpcUser = config.GetOrDefault<string>("rpcuser", null);
            nodeArgs.RPC.RpcPassword = config.GetOrDefault<string>("rpcpassword", null);
            if (nodeArgs.RPC.RpcPassword == null && nodeArgs.RPC.RpcUser != null)
               throw new ConfigurationException("rpcpassword should be provided");
            if (nodeArgs.RPC.RpcUser == null && nodeArgs.RPC.RpcPassword != null)
               throw new ConfigurationException("rpcuser should be provided");

            var defaultPort = config.GetOrDefault<int>("rpcport", network.RPCPort);
            nodeArgs.RPC.RPCPort = defaultPort;
            try {
               nodeArgs.RPC.Bind = config
                           .GetAll("rpcbind")
                           .Select(p => ConvertToEndpoint(p, defaultPort))
                           .ToList();
            }
            catch (FormatException) {
               throw new ConfigurationException("Invalid rpcbind value");
            }

            try {

               nodeArgs.RPC.AllowIp = config
                           .GetAll("rpcallowip")
                           .Select(p => IPAddress.Parse(p))
                           .ToList();
            }
            catch (FormatException) {
               throw new ConfigurationException("Invalid rpcallowip value");
            }

            if (nodeArgs.RPC.AllowIp.Count == 0) {
               nodeArgs.RPC.Bind.Clear();
               nodeArgs.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("::1"), defaultPort));
               nodeArgs.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("127.0.0.1"), defaultPort));
               if (config.Contains("rpcbind"))
                  Logs.Configuration.LogWarning("WARNING: option -rpcbind was ignored because -rpcallowip was not specified, refusing to allow everyone to connect");
            }

            if (nodeArgs.RPC.Bind.Count == 0) {
               nodeArgs.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("::"), defaultPort));
               nodeArgs.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("0.0.0.0"), defaultPort));
            }
         }

         try {
            nodeArgs.ConnectionManager.Connect.AddRange(config.GetAll("connect")
               .Select(c => ConvertToEndpoint(c, network.DefaultPort)));
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid connect parameter");
         }

         try {
            nodeArgs.ConnectionManager.AddNode.AddRange(config.GetAll("addnode")
                  .Select(c => ConvertToEndpoint(c, network.DefaultPort)));
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid addnode parameter");
         }

         var port = config.GetOrDefault<int>("port", network.DefaultPort);
         try {
            nodeArgs.ConnectionManager.Listen.AddRange(config.GetAll("listen")
                  .Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), false)));
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid listen parameter");
         }

         try {
            nodeArgs.ConnectionManager.Listen.AddRange(config.GetAll("whitebind")
                  .Select(c => new NodeServerEndpoint(ConvertToEndpoint(c, port), true)));
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid listen parameter");
         }

         if (nodeArgs.ConnectionManager.Listen.Count == 0) {
            nodeArgs.ConnectionManager.Listen.Add(new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port), false));
         }

         var externalIp = config.GetOrDefault<string>("externalip", null);
         if (externalIp != null) {
            try {
               nodeArgs.ConnectionManager.ExternalEndpoint = ConvertToEndpoint(externalIp, port);
            }
            catch (FormatException) {
               throw new ConfigurationException("Invalid externalip parameter");
            }
         }

         if (nodeArgs.ConnectionManager.ExternalEndpoint == null) {
            nodeArgs.ConnectionManager.ExternalEndpoint = new IPEndPoint(IPAddress.Loopback, network.DefaultPort);
         }

         nodeArgs.Mempool.Load(config);

         var folder = new DataFolder(nodeArgs.DataDir);
         if (!Directory.Exists(folder.CoinViewPath))
            Directory.CreateDirectory(folder.CoinViewPath);
         return nodeArgs;
      }


      #region Helpers
      private static void AssetConfigFileExists(NodeSettings nodeArgs) {
         if (!File.Exists(nodeArgs.ConfigurationFile))
            throw new ConfigurationException("Configuration file does not exists");
      }

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

      private string GetDefaultConfigurationFile() {
         var config = Path.Combine(DataDir, "bitcoin.conf");
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

      private static string GetDefaultDataDir(string appName, Network network) {
         string directory = null;
         var home = Environment.GetEnvironmentVariable("HOME");
         if (!string.IsNullOrEmpty(home)) {
            Logs.Configuration.LogInformation("Using HOME environment variable for initializing application data");
            directory = home;
            directory = Path.Combine(directory, "." + appName.ToLowerInvariant());
         }
         else {
            var localAppData = Environment.GetEnvironmentVariable("APPDATA");
            if (!string.IsNullOrEmpty(localAppData)) {
               Logs.Configuration.LogInformation("Using APPDATA environment variable for initializing application data");
               directory = localAppData;
               directory = Path.Combine(directory, appName);
            }
            else {
               throw new DirectoryNotFoundException("Could not find suitable datadir");
            }
         }
         if (!Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
         }
         directory = Path.Combine(directory, network.Name);
         if (!Directory.Exists(directory)) {
            Logs.Configuration.LogInformation("Creating data directory");
            Directory.CreateDirectory(directory);
         }
         return directory;
      }
      #endregion
   }
}
