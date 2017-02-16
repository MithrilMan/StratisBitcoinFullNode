using Microsoft.Extensions.Logging.Console;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;

namespace Stratis.BitcoinD {
   public class Program {


      public static void Main(string[] args) {
         Logs.Configure(new FuncLoggerFactory(n => new ConsoleLogger(n, (a, b) => true, false)));

         CommandLineApplication app = new CommandLineApplication(throwOnUnexpectedArg: true) {
            Name = "BitcoinD",
            Description = "BitcoindD stratis implementation",
            FullName = "BitcoinD full node implementation"
         };

         app.HelpOption("-? | -h | --help");

         Options.OptionsManager.Init(app);

         app.OnExecute(() => {
            var options = Options.OptionsManager.Instance;

            NodeArgs nodeArgs = new NodeArgs() {
               ConfigurationFile = options[Options.OptionsManager.Option.Conf].Value(),
               DataDir = options[Options.OptionsManager.Option.DataDir].Value()
            };

            Console.WriteLine($"you set ConfigurationFile={nodeArgs.ConfigurationFile}");
            Console.WriteLine($"you set DataDir={nodeArgs.DataDir}");

            //NodeArgs nodeArgs = NodeArgs.GetArgs(args);
            //FullNode node = new FullNode(nodeArgs);
            //CancellationTokenSource cts = new CancellationTokenSource();
            //new Thread(() => {
            //   Console.WriteLine("Press one key to stop");
            //   Console.ReadLine();
            //   node.Dispose();
            //}) {
            //   IsBackground = true //so the process terminate
            //}.Start();

            //node.Start();
            //node.WaitDisposed();
            //node.Dispose();
            return 0;
         });

         try {
            app.Execute(args);
         }
         catch (CommandParsingException e) {
            Console.WriteLine("invalid arguments");
            app.ShowHelp();
         }

         Console.ReadLine();
      }
   }
}
