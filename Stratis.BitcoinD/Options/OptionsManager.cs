using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;

namespace Stratis.BitcoinD.Options {
   public class OptionsManager {
      public enum Option {
         DataDir,
         Conf
      }

      static OptionsManager _instance = new OptionsManager();
      public static OptionsManager Instance => _instance;

      private Dictionary<Option, CommandOption> _options = new Dictionary<Option, CommandOption>();

      private OptionsManager() {
         _options[Option.DataDir] = new DataDir();
         _options[Option.Conf] = new Conf();
      }


      public CommandOption this[Option index] {
         get { return _options[index]; }
      }

      internal static void Init(CommandLineApplication app) {
         var instance = _instance;
         app.Options.AddRange(_instance._options.Values);
      }
   }
}
