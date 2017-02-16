using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;

namespace Stratis.BitcoinD.Options {
   public class DataDir : CommandOption {

      public DataDir() : base("-datadir", CommandOptionType.SingleValue) {
         this.Description = "-datadir=<dir> Specify data directory";
      }
   }
}
