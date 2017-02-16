using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;

namespace Stratis.BitcoinD.Options {
   public class Conf : CommandOption {

      public Conf() : base("-conf", CommandOptionType.SingleValue) {
         this.Description = "-conf=<file> Specify configuration file (default: bitcoin.conf)";
      }
   }
}
