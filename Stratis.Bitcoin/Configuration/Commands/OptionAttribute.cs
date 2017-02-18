using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Configuration.Commands {

   /// <summary>
   /// attribute used to easily define option/configuration arguments
   /// 
   /// actually not used
   /// </summary>
   [System.AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
   sealed class OptionAttribute : Attribute {
      public string Template { get; }
      public string Description { get; }
      public CommandOptionType OptionType { get; }

      // This is a positional argument
      public OptionAttribute(string template, CommandOptionType optionType, string descriptionFormat, params object[] args) {
         if (string.IsNullOrWhiteSpace(template)) {
            throw new ArgumentNullException(nameof(template), "Template cannot be null or empty");
         }
         this.Template = template;

         if (args != null) {
            this.Description = String.Format(descriptionFormat, args);
         }
         else {
            this.Description = descriptionFormat ?? String.Empty;
         }

         this.OptionType = optionType;
      }
   }
}
