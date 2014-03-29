using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using YAMP;

namespace Wox.Plugin.SystemPlugins
{
    public class Calculator : BaseSystemPlugin
    {
        private static Regex regValidExpressChar = new Regex(
                        @"^(" +
                        @"sin|cos|ceil|floor|exp|pi|max|min|det|arccos|abs|" +
                        @"eigval|eigvec|eig|sum|polar|plot|round|sort|real|zeta|" +
                        @"bin2dec|hex2dec|oct2dec|" +
                        @"==|~=|&&|\|\||" +
                        @"[ei]|[0-9]|[\+\-\*\/\^\., ""]|[\(\)\|\!\[\]]" +
                        @")+$", RegexOptions.Compiled);
        private static Regex regBrackets = new Regex(@"[\(\)\[\]]", RegexOptions.Compiled);
        private static ParseContext yampContext = null;
        private PluginInitContext context { get; set; }

        static Calculator()
        {
            yampContext = Parser.PrimaryContext;
            Parser.InteractiveMode = false;
            Parser.UseScripting = false;
        }

        protected override List<Result> QueryInternal(Query query)
        {
            if (string.IsNullOrEmpty(query.RawQuery)
                || query.RawQuery.Length <= 2          // don't affect when user only input "e" or "i" keyword
                || !regValidExpressChar.IsMatch(query.RawQuery) 
                || !IsBracketComplete(query.RawQuery)) return new List<Result>();

            try
            {
                var result = yampContext.Run(query.RawQuery);
                if (result.Output != null && !string.IsNullOrEmpty(result.Result))
                {
                    return new List<Result>() { new Result() { 
                        Title = result.Result, 
                        IcoPath = "Images/calculator.png", 
                        Score = 300,
                        SubTitle = "Copy this number to the clipboard", 
                        Action = (c) =>
                        {
                            Clipboard.SetText(result.Result);
                            return true;
                        }
                    } };
                }
            }
            catch
            {}

            return new List<Result>();
        }

        private bool IsBracketComplete(string query)
        {
            var matchs = regBrackets.Matches(query);
            var leftBracketCount = 0;
            foreach (Match match in matchs)
            {
                if (match.Value == "(" || match.Value == "[")
                {
                    leftBracketCount++;
                }
                else
                {
                    leftBracketCount--;
                }
            }

            return leftBracketCount == 0;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            this.context = context;
        }

        public override string Name
        {
            get { return "Calculator"; }
        }

        public override string IcoPath
        {
            get { return @"Images\calculator.png"; }
        }

        public override string Description
        {
            get { return base.Description; }
        }
    }
}
