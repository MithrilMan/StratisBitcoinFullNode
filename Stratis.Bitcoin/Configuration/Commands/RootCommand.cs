using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.MemoryPool;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging.Console;

namespace Stratis.Bitcoin.Configuration.Commands {
   /// <summary>
   /// handle the configuration of the root command (e.g. Stratis.BitcoinD)
   /// </summary>
   public class RootCommand {
      const bool DEFAULT_DEBUG_SETTINGS = false;
      const int DEFAULT_MAX_TIP_AGE = 24 * 60 * 60;

      /// <summary>
      /// enum that expose the available options.
      /// the option name, by convention, is the lowercase Enum name with the - prefix.
      /// e.g. Option.Conf option name is -conf
      /// </summary>
      internal enum Option {
         DataDir,
         Conf,

         Testnet,
         Regtest,
         Prune,
         AcceptNonStdTxn,
         MaxTipAge,
         Server,
         PrintToConsole,

         //RPCSettings
         RpcUser,
         RpcPassword,
         RpcPort,
         RpcBind,
         RpcAllowIp,

         //ConnectionManagerSettings
         Connect,
         AddNode,
         Port,
         Listen,
         WhiteBind,
         ExternalIp,

         //MemPoolSettings
         MaxMemPool,
         MemPoolExpiry,
         RelayPriority,
         LimitFreeRelay,
         LimitAncestorCount,
         LimitAncestorSize,
         LimitDescendantCount,
         LimitDescendantSize,
         MemPoolReplacement,
         MaxOrphanTx,
         BlocksOnly,
         WhitelistRelay,
      }

      private Dictionary<Option, CommandOption> _options = new Dictionary<Option, CommandOption>();
      private CommandLineApplication _app;

      /// <summary>
      /// the populated Node Settings (CommandLine + Configuration file)
      /// </summary>
      public NodeSettings NodeSettings { get; private set; }



      public RootCommand(CommandLineApplication app) {
         _app = app;

         CreateOptions(app);

         _app.OnExecute(() => {
            Logs.Configure(new FuncLoggerFactory(n => new ConsoleLogger(n, (a, b) => true, false)));

            var settings = new NodeSettings();

            #region preload some settings to load the configuration file
            settings.RegTest = _options[Option.Regtest].HasValue();
            settings.Testnet = _options[Option.Testnet].HasValue();

            if (settings.Testnet && settings.RegTest) {
               throw new ConfigurationException("Invalid combination of -regtest and -testnet");
            }

            settings.DataDir = GetOrDefault(Option.DataDir, GetDefaultDataDir("stratisbitcoin", settings.GetNetwork()));
            settings.ConfigurationFile = GetOrDefault(Option.Conf, GetDefaultConfigurationFile(settings.DataDir));
            #endregion

            if (settings.DataDir != null && settings.ConfigurationFile != null) {
               if (!Directory.Exists(settings.DataDir)) {
                  throw new ConfigurationException($"Data directory '{settings.DataDir}' does not exists");
               }

               var isRelativePath = Path.GetFullPath(settings.ConfigurationFile).Length > settings.ConfigurationFile.Length;
               if (isRelativePath) {
                  settings.ConfigurationFile = Path.Combine(settings.DataDir, settings.ConfigurationFile);
               }
            }

            //merge commandline and configuration file options
            UpdateFromConfigurationFile(settings.ConfigurationFile);

            //populate the final node settings (-conf and -datadir don't update, they are considered only as commandline arguments
            this.NodeSettings = PopulateSettings(settings, settings.GetNetwork());

            Logs.Configuration.LogInformation($"Data directory set to {settings.DataDir}");
            Logs.Configuration.LogInformation($"Configuration file set to {settings.ConfigurationFile}");


            var folder = new DataFolder(settings.DataDir);
            if (!Directory.Exists(folder.CoinViewPath)) {
               Directory.CreateDirectory(folder.CoinViewPath);
            }


            //setup the correct log provider, reading the -printtoconsole option
            if (GetOrDefault<bool>(Option.PrintToConsole, false) == false) {
               string fileName = Path.Combine(settings.DataDir, "debug.log");
               try {
                  // Attempt to open output file.
                  FileStream fs = new FileStream(fileName, FileMode.Create);
                  var writer = new StreamWriter(fs);
                  writer.AutoFlush = true;
                  Console.WriteLine($"Redirecting logs to file {fileName}");
                  Console.SetOut(writer);
               }
               catch (IOException e) {
                  Console.WriteLine($"Error redirecting output to file {fileName}");
               }
            }


            Logs.Configuration.LogInformation($"Final Node Settings: {settings.ToJsonString()}");

            return 0;
         });
      }


      internal CommandOption this[Option index] {
         get { return _options[index]; }
      }


      /// <summary>
      /// initialize available command line options
      /// </summary>
      private void CreateOptions(CommandLineApplication app) {

         registerOption(Option.DataDir, CommandOptionType.SingleValue, "-datadir=<dir> Specify data directory");
         registerOption(Option.Conf, CommandOptionType.SingleValue, "=<file> Specify configuration file (default: bitcoin.conf)");
         registerOption(Option.Testnet, CommandOptionType.SingleValue, "Use the test chain");
         registerOption(Option.Regtest, CommandOptionType.SingleValue, "Run a regression test network");
         registerOption(Option.Prune, CommandOptionType.SingleValue, "=<n> Reduce storage requirements by pruning (deleting) old blocks. This mode is incompatible with -txindex and -rescan. Warning: Reverting this setting requires re-downloading the entire blockchain. (default: 0 = disable pruning blocks, >550 = target size in MiB to use for block files)");
         registerOption(Option.AcceptNonStdTxn, CommandOptionType.SingleValue, "-acceptnonstdtxn Accept \"non-standard\" transactions for relay and blocks");
         registerOption(Option.MaxTipAge, CommandOptionType.SingleValue, "=<n> Maximum tip age in seconds to consider node in initial block download");
         registerOption(Option.Server, CommandOptionType.SingleValue, "Accept command line and JSON-RPC commands");
         registerOption(Option.PrintToConsole, CommandOptionType.SingleValue, "Send trace/debug info to console instead of debug.log file");
         registerOption(Option.RpcUser, CommandOptionType.SingleValue, "-rpcuser=<user> Username for JSON-RPC connections");
         registerOption(Option.RpcPassword, CommandOptionType.SingleValue, "-rpcpassword=<pw> Password for JSON-RPC connections");
         registerOption(Option.RpcPort, CommandOptionType.SingleValue, $"-rpcport=<port> Listen for JSON-RPC connections on <port>, default: -");
         registerOption(Option.RpcBind, CommandOptionType.MultipleValue, $"-rpcbind=<addr> Bind to given address to listen for JSON-RPC connections. Use [host]:port notation for IPv6. This option can be specified multiple times (default: bind to all interfaces)");
         registerOption(Option.RpcAllowIp, CommandOptionType.MultipleValue, $"-rpcallowip=<ip> Allow JSON-RPC connections from specified source. Valid for <ip> are a single IP (e.g. 1.2.3.4), a network/netmask (e.g. 1.2.3.4/255.255.255.0) or a network/CIDR (e.g. 1.2.3.4/24). This option can be specified multiple times");
         registerOption(Option.Connect, CommandOptionType.MultipleValue, $"-connect=<ip> Connect only to the specified node(s); -noconnect or -connect=0 alone to disable automatic connections");
         registerOption(Option.AddNode, CommandOptionType.MultipleValue, $"-addnode=<ip> Add a node to connect to and attempt to keep the connection open");
         registerOption(Option.Port, CommandOptionType.SingleValue, $"-port=<port> Listen for connections on <port> (default: {Network.Main.DefaultPort} or testnet: {Network.TestNet.DefaultPort})");
         registerOption(Option.Listen, CommandOptionType.SingleValue, $"-listen Accept connections from outside (default: 1 if no -proxy or -connect/-noconnect)");
         registerOption(Option.WhiteBind, CommandOptionType.SingleValue, $"-whitebind=<addr> Bind to given address and whitelist peers connecting to it. Use [host]:port notation for IPv6");
         registerOption(Option.ExternalIp, CommandOptionType.SingleValue, $"-externalip=<ip> Specify your own public address");
         registerOption(Option.MaxMemPool, CommandOptionType.SingleValue, $"-maxmempool=<n> Keep the transaction memory pool below <n> megabytes (default: {MempoolValidator.DefaultMaxMempoolSize})");
         registerOption(Option.MemPoolExpiry, CommandOptionType.SingleValue, $"-mempoolexpiry=<n> Do not keep transactions in the mempool longer than <n> hours (default: {MempoolValidator.DefaultMempoolExpiry})");
         registerOption(Option.RelayPriority, CommandOptionType.SingleValue, $"-relaypriority Require high priority for relaying free or low-fee transactions (default: {(MempoolValidator.DefaultRelayPriority ? "1" : "0")})");
         registerOption(Option.LimitFreeRelay, CommandOptionType.SingleValue, $"-limitfreerelay=<n> Rate-limit free transactions to <n>*1000 bytes per minute (default: {MempoolValidator.DefaultLimitFreeRelay})");
         registerOption(Option.LimitAncestorCount, CommandOptionType.SingleValue, $"-limitancestorcount=<n> Do not accept transactions if number of in-mempool ancestors is <n> or more (default {MempoolValidator.DefaultAncestorLimit})");
         registerOption(Option.LimitAncestorSize, CommandOptionType.SingleValue, $"-limitancestorsize=<n> Do not accept transactions whose size with all in-mempool ancestors exceeds <n> kilobytes (default: {MempoolValidator.DefaultAncestorSizeLimit})");
         registerOption(Option.LimitDescendantCount, CommandOptionType.SingleValue, $"-limitdescendantcount=<n> Do not accept transactions whose size with all in-mempool ancestors exceeds <n> kilobytes (default: {MempoolValidator.DefaultDescendantLimit})");
         registerOption(Option.LimitDescendantSize, CommandOptionType.SingleValue, $"-limitdescendantsize=<n> Do not accept transactions if any ancestor would have more than <n> kilobytes of in-mempool descendants (default: {MempoolValidator.DefaultDescendantSizeLimit})");
         registerOption(Option.MemPoolReplacement, CommandOptionType.SingleValue, $"-mempoolreplacement Enable transaction replacement in the memory pool (default: {(MempoolValidator.DefaultEnableReplacement ? "1" : "0")})");
         registerOption(Option.MaxOrphanTx, CommandOptionType.SingleValue, $"-maxorphantx=<n> Keep at most <n> unconnectable transactions in memory (default: {MempoolOrphans.DEFAULT_MAX_ORPHAN_TRANSACTIONS})");
         registerOption(Option.BlocksOnly, CommandOptionType.SingleValue, $"-blocksonly Whether to operate in a blocks only mode (default: {MemPoolSettings.DEFAULT_BLOCKSONLY})");
         registerOption(Option.WhitelistRelay, CommandOptionType.SingleValue, $"-whitelistrelay Accept relayed transactions received from whitelisted peers even when not relaying transactions (default: {MemPoolSettings.DEFAULT_WHITELISTRELAY})");
      }

      private int UpdateFromConfigurationFile(string configurationFilePath) {
         if (!File.Exists(configurationFilePath)) {
            throw new ConfigurationException("Configuration file does not exists");
         }

         var fileArgs = ReadConfigurationFile(configurationFilePath);

         Logs.Configuration.LogInformation(
            "Arguments read from file: {0}{1}",
            System.Environment.NewLine,
            string.Join(System.Environment.NewLine, fileArgs.Select(r => String.Format("{0}{1}", r.Key, r.Value != null ? "=" + r.Value : "")))
            );

         //merge file options to command line arguments
         foreach (var arg in fileArgs) {
            AddOptionValue(arg.Key, arg.Value);
         }

         return 0;
      }

      private NodeSettings PopulateSettings(NodeSettings nodeSettings, Network network) {
         //Cache = new CacheSettings() {
         //     MaxItems = ?
         // }

         nodeSettings.Prune = GetOrDefault(Option.Prune, false);

         nodeSettings.RequireStandard = GetOrDefault(Option.AcceptNonStdTxn, !(nodeSettings.RegTest || nodeSettings.Testnet));
         nodeSettings.MaxTipAge = GetOrDefault(Option.MaxTipAge, DEFAULT_MAX_TIP_AGE);

         #region RPC Settings
         if (GetOrDefault<bool>(Option.Server, false)) {
            var defaultRpcPort = network.RPCPort;
            nodeSettings.RPC = new Configuration.RPCSettings() {
               RpcUser = GetOrDefault<string>(Option.RpcUser, null),
               RpcPassword = GetOrDefault<string>(Option.RpcPassword, null),
               RPCPort = GetOrDefault<int>(Option.RpcPort, defaultRpcPort)
            };

            if (nodeSettings.RPC.RpcPassword == null && nodeSettings.RPC.RpcUser != null) {
               throw new ConfigurationException("rpcpassword should be provided");
            }
            if (nodeSettings.RPC.RpcUser == null && nodeSettings.RPC.RpcPassword != null) {
               throw new ConfigurationException("rpcuser should be provided");
            }

            try {
               nodeSettings.RPC.Bind = GetAll(Option.RpcBind)
                  .Select(p => ConvertToEndpoint(p, defaultRpcPort))
                  .ToList();
            }
            catch (FormatException) {
               throw new ConfigurationException("Invalid rpcbind value");
            }

            try {
               nodeSettings.RPC.AllowIp = GetAll(Option.RpcAllowIp)
                  .Select(p => IPAddress.Parse(p))
                  .ToList();
            }
            catch (FormatException) {
               throw new ConfigurationException("Invalid rpcallowip value");
            }

            if (nodeSettings.RPC.AllowIp.Count == 0) {
               if (nodeSettings.RPC.Bind.Count > 0) {
                  Logs.Configuration.LogWarning("WARNING: option -rpcbind was ignored because -rpcallowip was not specified, refusing to allow everyone to connect");
               }

               nodeSettings.RPC.Bind.Clear();
               nodeSettings.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("::1"), defaultRpcPort));
               nodeSettings.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("127.0.0.1"), defaultRpcPort));

            }

            if (nodeSettings.RPC.Bind.Count == 0) {
               nodeSettings.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("::"), defaultRpcPort));
               nodeSettings.RPC.Bind.Add(new IPEndPoint(IPAddress.Parse("0.0.0.0"), defaultRpcPort));
            }
         }
         else {
            nodeSettings.RPC = null;
         }

         #endregion

         #region ConnectionManager
         try {
            nodeSettings.ConnectionManager.Connect.AddRange(
               GetAll(Option.Connect).Select(c => ConvertToEndpoint(c, network.DefaultPort))
            );
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid connect parameter");
         }

         try {
            nodeSettings.ConnectionManager.AddNode.AddRange(
               GetAll(Option.AddNode).Select(c => ConvertToEndpoint(c, network.DefaultPort))
            );
         }
         catch (FormatException) {
            throw new ConfigurationException("Invalid addnode parameter");
         }


         //listen enabled? default value is 1 if no -connect used, otherwise 0
         nodeSettings.ConnectionManager.IsListening = GetOrDefault<int>(Option.Listen, nodeSettings.ConnectionManager.Connect.Count > 0 ? 0 : 1) != 0;

         nodeSettings.ConnectionManager.Port = GetOrDefault<int>(Option.Port, network.DefaultPort);

         //if listening, listen to 0.0.0.0:port
         if (nodeSettings.ConnectionManager.IsListening) {
            if (nodeSettings.ConnectionManager.Listen.Count == 0) {
               nodeSettings.ConnectionManager.Listen.Add(
                  new NodeServerEndpoint(new IPEndPoint(IPAddress.Parse("0.0.0.0"), nodeSettings.ConnectionManager.Port), false)
               );
            }
         }


         string whitebind = GetOrDefault<string>(Option.WhiteBind, null);
         if (whitebind != null) {
            try {
               nodeSettings.ConnectionManager.Listen.Add(new NodeServerEndpoint(ConvertToEndpoint(whitebind, nodeSettings.ConnectionManager.Port), true));
            }
            catch (FormatException) {
               throw new ConfigurationException("Invalid whitebind parameter");
            }
         }


         var externalIp = GetOrDefault<string>(Option.ExternalIp, null);
         if (externalIp != null) {
            try {
               nodeSettings.ConnectionManager.ExternalEndpoint = ConvertToEndpoint(externalIp, nodeSettings.ConnectionManager.Port);
            }
            catch (FormatException) {
               throw new ConfigurationException("Invalid externalip parameter");
            }
         }

         if (nodeSettings.ConnectionManager.ExternalEndpoint == null) {
            nodeSettings.ConnectionManager.ExternalEndpoint = new IPEndPoint(IPAddress.Loopback, nodeSettings.ConnectionManager.Port);
         }
         #endregion


         #region MemPool settings
         nodeSettings.MemPool.MaxMempool = GetOrDefault(Option.MaxMemPool, MempoolValidator.DefaultMaxMempoolSize);
         nodeSettings.MemPool.MemPoolExpiry = GetOrDefault(Option.MemPoolExpiry, MempoolValidator.DefaultMempoolExpiry);
         nodeSettings.MemPool.RelayPriority = GetOrDefault(Option.RelayPriority, MempoolValidator.DefaultRelayPriority);
         nodeSettings.MemPool.LimitFreeRelay = GetOrDefault(Option.LimitFreeRelay, MempoolValidator.DefaultLimitFreeRelay);
         nodeSettings.MemPool.LimitAncestorCount = GetOrDefault(Option.LimitAncestorCount, MempoolValidator.DefaultAncestorLimit);
         nodeSettings.MemPool.LimitAncestorSize = GetOrDefault(Option.LimitAncestorSize, MempoolValidator.DefaultAncestorSizeLimit);
         nodeSettings.MemPool.LimitDescendantCount = GetOrDefault(Option.LimitDescendantCount, MempoolValidator.DefaultDescendantLimit);
         nodeSettings.MemPool.LimitDescendantSize = GetOrDefault(Option.LimitDescendantSize, MempoolValidator.DefaultDescendantSizeLimit);
         nodeSettings.MemPool.EnableReplacement = GetOrDefault(Option.MemPoolReplacement, MempoolValidator.DefaultEnableReplacement);
         nodeSettings.MemPool.MaxOrphanTx = GetOrDefault(Option.MaxOrphanTx, MempoolOrphans.DEFAULT_MAX_ORPHAN_TRANSACTIONS);
         nodeSettings.MemPool.RelayTxes = GetOrDefault(Option.BlocksOnly, MemPoolSettings.DEFAULT_BLOCKSONLY) == false;
         nodeSettings.MemPool.Whitelistrelay = GetOrDefault(Option.WhitelistRelay, MemPoolSettings.DEFAULT_WHITELISTRELAY);
         #endregion

         return nodeSettings;
      }

      /// <summary>
      /// tries to add an option value
      /// </summary>
      /// <param name="option"></param>
      /// <param name="value"></param>
      private void AddOptionValue(string option, string value) {
         if (option.StartsWith("-")) {
            option = option.Substring(1); //remove - from the option name
         }

         var x = new NodeSettings();
         var opt = _options.Values.FirstOrDefault(o => o.ShortName == option);
         if (opt != null) {
            if (opt.HasValue() && opt.OptionType == CommandOptionType.SingleValue) {
               opt.Values.Clear();
               opt.Values.Add(value);
               Logs.Configuration.LogInformation($"Replaced value {value} to {option}");
               //throw new ConfigurationException($"Duplicate value for option {option}");
            }
            else {
               opt.Values.Add(value);

               Logs.Configuration.LogInformation($"Added value {value} to {option}");
            }
         }
         else {
            Logs.Configuration.LogWarning($"option {option} not valid. Ignored");
         }
      }



      #region Helpers
      /// <summary>
      /// helper to register a new option
      /// </summary>
      /// <param name="app"></param>
      /// <param name="option"></param>
      /// <param name="optionType"></param>
      /// <param name="description"></param>
      private void registerOption(Option option, CommandOptionType optionType, string description) {
         if (!_options.ContainsKey(option)) {
            _options[option] = _app.Option(
               "-" + option.ToString().ToLowerInvariant(),
               description,
               optionType
            );
         }
         else {
            throw new ArgumentException($"Option {option} already registered");
         }
      }


      private T GetOrDefault<T>(Option option, T defaultValue) {

         var commandOption = this[option];

         if (!commandOption.HasValue()) {
            return defaultValue;
         }
         else {
            try {
               return ConvertValue<T>(commandOption.Value());
            }
            catch (FormatException) { throw new ConfigurationException($"Option {commandOption.LongName} should be of type " + typeof(T).Name); }
         }
      }


      private string[] GetAll(Option option) {
         var commandOption = this[option];
         return commandOption?.Values?.ToArray();
      }


      private T ConvertValue<T>(string str) {
         if (typeof(T) == typeof(bool)) {
            var trueValues = new[] { "1", "true" };
            var falseValues = new[] { "0", "false" };
            if (trueValues.Contains(str, StringComparer.OrdinalIgnoreCase)) {
               return (T)(object)true;
            }
            if (falseValues.Contains(str, StringComparer.OrdinalIgnoreCase)) {
               return (T)(object)false;
            }
            throw new FormatException();
         }
         else if (typeof(T) == typeof(string)) {
            return (T)(object)str;
         }
         else if (typeof(T) == typeof(int)) {
            return (T)(object)int.Parse(str, CultureInfo.InvariantCulture);
         }
         else {
            throw new NotSupportedException("Configuration value does not support time " + typeof(T).Name);
         }
      }


      private static IPEndPoint ConvertToEndpoint(string str, int defaultPort) {
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


      private string GetDefaultConfigurationFile(string datadir) {
         var config = Path.Combine(datadir, "bitcoin.conf");
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
      #endregion
   }
}
