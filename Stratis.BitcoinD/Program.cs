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
using System.IO;

namespace Stratis.BitcoinD {
   public class Program {


      public static void Main(string[] args) {
         NodeSettings nodeSettings;
         try {
            nodeSettings = NodeSettings.Load(args);

            //nodeSettings null means commandline help has been issued, no need to proceed
            if (nodeSettings == null) {
               return;
            }
         }
         catch (CommandParsingException ex) {
            ex.Command.ShowHelp();
            return;
         }
         catch (Exception ex) {
            Console.WriteLine(ex.Message);
            return;
         }

         //#if DEBUG
         //         string fileName = System.IO.Path.Combine(nodeArgs.DataDir, "debug.log");
         //         try {
         //            // Attempt to open output file.
         //            FileStream fs = new FileStream(fileName, FileMode.Create);
         //            var writer = new StreamWriter(fs);
         //            // Redirect standard output from the console to the output file.
         //            Console.SetOut(writer);
         //            Console.WriteLine("test");
         //         }
         //         catch (IOException e) {
         //            Console.WriteLine($"Error redirecting output to file {fileName}");
         //            return;
         //         }
         //#endif

         FullNode node = new FullNode(nodeSettings);
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
