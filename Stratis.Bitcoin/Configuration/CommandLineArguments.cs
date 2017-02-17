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

namespace Stratis.Bitcoin.Configuration {
   public class CommandLineArguments {
      const bool DEFAULT_DEBUG_SETTINGS = false;
      const int DEFAULT_MAX_TIP_AGE = 24 * 60 * 60;


      internal enum Option {
         Conf,

         DataDir,
         Testnet,
         Regtest,
         Prune,
         RequireStandard,
         MaxTipAge,
         Server,

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

#if DEBUG
         //Debug
         DebugSettings
#endif
      }

      private Dictionary<Option, CommandOption> _options = new Dictionary<Option, CommandOption>();
      private CommandLineApplication _app;
      public CommandLineArguments() {
         _app = new CommandLineApplication(throwOnUnexpectedArg: true) {
            Name = "BitcoinD",
            Description = "BitcoindD stratis implementation",
            FullName = "BitcoinD full node implementation"
         };

         _app.HelpOption("-? | -h | --help");

         CreateOptions();
      }


      /// <summary>
      /// initialize available command line options
      /// </summary>
      private void CreateOptions() {
         _options[Option.DataDir] = new CommandOption("-datadir", CommandOptionType.SingleValue) {
            Description = "-datadir=<dir> Specify data directory"
         };

         _options[Option.Conf] = new CommandOption("-conf", CommandOptionType.SingleValue) {
            Description = "-conf=<file> Specify configuration file (default: bitcoin.conf)"
         };

         _options[Option.Testnet] = new CommandOption("-testnet", CommandOptionType.NoValue) {
            Description = "-conf=<file> Specify configuration file (default: bitcoin.conf)"
         };

         _options[Option.Regtest] = new CommandOption("-regtest", CommandOptionType.NoValue) {
            Description = "-conf=<file> Specify configuration file (default: bitcoin.conf)"
         };

         _options[Option.Prune] = new CommandOption("-prune", CommandOptionType.SingleValue) {
            Description = "-prune=<n> Reduce storage requirements by pruning (deleting) old blocks. This mode is incompatible with -txindex and -rescan. Warning: Reverting this setting requires re-downloading the entire blockchain. (default: 0 = disable pruning blocks, >550 = target size in MiB to use for block files)"
         };

         _options[Option.RequireStandard] = new CommandOption("-acceptnonstdtxn", CommandOptionType.NoValue) {
            Description = "-acceptnonstdtxn Accept \"non-standard\" transactions for relay and blocks",
         };

         _options[Option.MaxTipAge] = new CommandOption("-maxtipage", CommandOptionType.SingleValue) {
            Description = "-maxtipage=<n> Maximum tip age in seconds to consider node in initial block download",
         };

         _options[Option.Server] = new CommandOption("-server", CommandOptionType.SingleValue) {
            Description = "-server=<0/1> Accept command line and JSON-RPC commands",
         };

         _options[Option.RpcUser] = new CommandOption("-rpcuser", CommandOptionType.SingleValue) {
            Description = "-rpcuser=<user> Username for JSON-RPC connections",
         };

         _options[Option.RpcPassword] = new CommandOption("-rpcpassword", CommandOptionType.SingleValue) {
            Description = "-rpcpassword=<pw> Password for JSON-RPC connections",
         };

         _options[Option.RpcPort] = new CommandOption("-rpcport", CommandOptionType.SingleValue) {
            Description = $"-rpcport=<port> Listen for JSON-RPC connections on <port>, default: -",
         };

         _options[Option.RpcBind] = new CommandOption("-rpcbind", CommandOptionType.MultipleValue) {
            Description = $"-rpcbind=<addr> Bind to given address to listen for JSON-RPC connections. Use [host]:port notation for IPv6. This option can be specified multiple times (default: bind to all interfaces)",
         };

         _options[Option.RpcAllowIp] = new CommandOption("-rpcallowip", CommandOptionType.MultipleValue) {
            Description = $"-rpcallowip=<ip> Allow JSON-RPC connections from specified source. Valid for <ip> are a single IP (e.g. 1.2.3.4), a network/netmask (e.g. 1.2.3.4/255.255.255.0) or a network/CIDR (e.g. 1.2.3.4/24). This option can be specified multiple times",
         };

         _options[Option.Connect] = new CommandOption("-connect", CommandOptionType.MultipleValue) {
            Description = $"-connect=<ip> Connect only to the specified node(s); -noconnect or -connect=0 alone to disable automatic connections",
         };

         _options[Option.AddNode] = new CommandOption("-addnode", CommandOptionType.MultipleValue) {
            Description = $"-addnode=<ip> Add a node to connect to and attempt to keep the connection open",
         };

         _options[Option.Port] = new CommandOption("-port", CommandOptionType.SingleValue) {
            Description = $"-port=<port> Listen for connections on <port> (default: {Network.Main.DefaultPort} or testnet: {Network.TestNet.DefaultPort})",
         };

         _options[Option.Listen] = new CommandOption("-listen", CommandOptionType.SingleValue) {
            Description = $"-listen Accept connections from outside (default: 1 if no -proxy or -connect/-noconnect)",
         };

         _options[Option.WhiteBind] = new CommandOption("-whitebind", CommandOptionType.SingleValue) {
            Description = $"-whitebind=<addr> Bind to given address and whitelist peers connecting to it. Use [host]:port notation for IPv6",
         };

         _options[Option.ExternalIp] = new CommandOption("-externalip", CommandOptionType.SingleValue) {
            Description = $"-externalip=<ip> Specify your own public address",
         };


         _options[Option.MaxMemPool] = new CommandOption("-maxmempool", CommandOptionType.SingleValue) {
            Description = $"-maxmempool=<n> Keep the transaction memory pool below <n> megabytes (default: {MempoolValidator.DefaultMaxMempoolSize})",
         };

         _options[Option.MemPoolExpiry] = new CommandOption("-mempoolexpiry", CommandOptionType.SingleValue) {
            Description = $"-mempoolexpiry=<n> Do not keep transactions in the mempool longer than <n> hours (default: {MempoolValidator.DefaultMempoolExpiry})",
         };

         _options[Option.RelayPriority] = new CommandOption("-relaypriority", CommandOptionType.SingleValue) {
            Description = $"-relaypriority=<0/1> Require high priority for relaying free or low-fee transactions (default: {(MempoolValidator.DefaultRelayPriority ? "1" : "0")})",
         };

         _options[Option.LimitFreeRelay] = new CommandOption("-limitfreerelay", CommandOptionType.SingleValue) {
            Description = $"-limitfreerelay=<n> Rate-limit free transactions to <n>*1000 bytes per minute (default: {MempoolValidator.DefaultLimitFreeRelay})",
         };

         _options[Option.LimitAncestorCount] = new CommandOption("-limitancestorcount", CommandOptionType.SingleValue) {
            Description = $"-limitancestorcount=<n> Do not accept transactions if number of in-mempool ancestors is <n> or more (default {MempoolValidator.DefaultAncestorLimit})",
         };

         _options[Option.LimitAncestorSize] = new CommandOption("-limitancestorsize", CommandOptionType.SingleValue) {
            Description = $"-limitancestorsize=<n> Do not accept transactions whose size with all in-mempool ancestors exceeds <n> kilobytes (default: {MempoolValidator.DefaultAncestorSizeLimit})",
         };

         _options[Option.LimitDescendantCount] = new CommandOption("-limitdescendantcount", CommandOptionType.SingleValue) {
            Description = $"-limitdescendantcount=<n> Do not accept transactions whose size with all in-mempool ancestors exceeds <n> kilobytes (default: {MempoolValidator.DefaultDescendantLimit})",
         };

         _options[Option.LimitDescendantSize] = new CommandOption("-limitdescendantsize", CommandOptionType.SingleValue) {
            Description = $"-limitdescendantsize=<n> Do not accept transactions if any ancestor would have more than <n> kilobytes of in-mempool descendants (default: {MempoolValidator.DefaultDescendantSizeLimit})",
         };

         _options[Option.MemPoolReplacement] = new CommandOption("-mempoolreplacement", CommandOptionType.SingleValue) {
            Description = $"-mempoolreplacement=<0/1> Enable transaction replacement in the memory pool (default: {(MempoolValidator.DefaultEnableReplacement ? "1" : "0")})",
         };

         _options[Option.MaxOrphanTx] = new CommandOption("-maxorphantx", CommandOptionType.SingleValue) {
            Description = $"-maxorphantx=<n> Keep at most <n> unconnectable transactions in memory (default: {MempoolOrphans.DEFAULT_MAX_ORPHAN_TRANSACTIONS})",
         };

         _options[Option.BlocksOnly] = new CommandOption("-blocksonly", CommandOptionType.SingleValue) {
            Description = $"-blocksonly=<0/1> Whether to operate in a blocks only mode (default: {MemPoolSettings.DEFAULT_BLOCKSONLY})",
         };

         _options[Option.WhitelistRelay] = new CommandOption("-blocksonly", CommandOptionType.SingleValue) {
            Description = $"-whitelistrelay=<0/1> Accept relayed transactions received from whitelisted peers even when not relaying transactions (default: {MemPoolSettings.DEFAULT_WHITELISTRELAY})",
         };



         #region DEBUG options
         _options[Option.DebugSettings] = new CommandOption("-debugsettings", CommandOptionType.NoValue) {
            Description = $"-debugsettings Dump debug settings information (default: {(DEFAULT_DEBUG_SETTINGS ? "1" : "0")})",
         };
         #endregion

         _app.Options.AddRange(_options.Values);
      }

      internal CommandOption this[Option index] {
         get { return _options[index]; }
      }

      internal void Load(string[] args) {
         _app.Execute(args);
      }

      internal NodeSettings GetSettings() {
         var nodeSettings = new NodeSettings() {
            ConfigurationFile = _options[Option.Conf].Value(),
            RegTest = _options[Option.Regtest].HasValue(),
            Testnet = _options[Option.Testnet].HasValue(),

            //Cache = new CacheSettings() {
            //     MaxItems = ?
            // }
         };

         var network = nodeSettings.GetNetwork();

         nodeSettings.DataDir = GetOrDefault(Option.DataDir, GetDefaultDataDir("stratisbitcoin", network));
         nodeSettings.Prune = GetOrDefault(Option.Prune, false);


         nodeSettings.RequireStandard = GetOrDefault(Option.RequireStandard, !(nodeSettings.RegTest || nodeSettings.Testnet));
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
      internal void AddOptionValue(string option, string value) {
         var x = new NodeSettings();
         var opt = _options.Values.FirstOrDefault(o => o.LongName == option);
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
            Debug.Write($"option {option} not valid. Ignored");
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
   }
}
