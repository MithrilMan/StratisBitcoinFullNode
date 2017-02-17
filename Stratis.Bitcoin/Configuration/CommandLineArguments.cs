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

namespace Stratis.Bitcoin.Configuration {
   public class CommandLineArguments {
      const int DEFAULT_MAX_TIP_AGE = 24 * 60 * 60;

      internal enum Option {
         DataDir,
         Conf,
         Testnet,
         Regtest,
         Prune,
         RequireStandard,
         MaxTipAge,
         Server,
         RpcUser,
         RpcPassword,
         RpcPort,
         RpcBind,
         RpcAllowIp,
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

         _options[Option.Conf] = new CommandOption("-testnet", CommandOptionType.NoValue) {
            Description = "-conf=<file> Specify configuration file (default: bitcoin.conf)"
         };

         _options[Option.Conf] = new CommandOption("-regtest", CommandOptionType.NoValue) {
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
            Description = $"-rpcallowip=<ip> Allow JSON-RPC connections from specified source. Valid for <ip> are a single IP (e.g. 1.2.3.4), a network/netmask (e.g. 1.2.3.4/255.255.255.0) or a network/CIDR (e.g. 1.2.3.4/24). This option can be specified multiple times"",
         };




         _app.Options.AddRange(_options.Values);
      }

      internal CommandOption this[Option index] {
         get { return _options[Option.DataDir]; }
      }

      internal void Load(string[] args) {
         _app.Execute(args);
      }

      internal NodeSettings GetSettings() {
         var nodeSettings = new NodeSettings() {
            ConfigurationFile = _options[Option.Conf].Value(),
            DataDir = _options[Option.DataDir].Value(),
            RegTest = _options[Option.Regtest].HasValue(),
            Testnet = _options[Option.Testnet].HasValue(),

            Prune = GetOrDefault<int>(Option.Prune, 0) != 0,

            //Cache = new CacheSettings() {
            //     MaxItems = ?
            // }
         };

         var network = nodeSettings.GetNetwork();

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
                  .Select(p => NodeSettings.ConvertToEndpoint(p, defaultRpcPort))
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


         //nodeArgs.ConnectionManager.AddNode

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
               throw new ConfigurationException($"Duplicate value for option {option}");
            }
            else {
               opt.Values.Add(value);
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
   }
}
