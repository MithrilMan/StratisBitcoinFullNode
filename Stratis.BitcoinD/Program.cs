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
         NodeSettings nodeArgs = NodeSettings.GetArgs(args);

         //Console.WriteLine($"you set ConfigurationFile={nodeArgs.ConfigurationFile}");
         //Console.WriteLine($"you set DataDir={nodeArgs.DataDir}");

         FullNode node = new FullNode(nodeArgs);
         CancellationTokenSource cts = new CancellationTokenSource();
         new Thread(() => {
            Console.WriteLine("Press one key to stop");
            Console.ReadLine();
            node.Dispose();
         }) {
            IsBackground = true //so the process terminate
         }.Start();

         node.Start();
         node.WaitDisposed();
         node.Dispose();

         Console.ReadLine();
      }
   }
}
