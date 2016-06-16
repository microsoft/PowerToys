using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using YAMP;

namespace Wox.Plugin.Caculator
{
    public class Main : IPlugin, IPluginI18n
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
        private static ParseContext yampContext;
        private PluginInitContext context { get; set; }

        static Main()
        {
            yampContext = Parser.PrimaryContext;
            Parser.InteractiveMode = false;
            Parser.UseScripting = false;
        }

        public List<Result> Query(Query query)
        {
            if (query.Search.Length <= 2          // don't affect when user only input "e" or "i" keyword
                || !regValidExpressChar.IsMatch(query.Search)
                || !IsBracketComplete(query.Search)) return new List<Result>();

            try
            {
                var result = yampContext.Run(query.Search);
                if (result.Output != null && !string.IsNullOrEmpty(result.Result))
                {
                    return new List<Result>
                    { new Result
                    { 
                        Title = result.Result, 
                        IcoPath = "Images/calculator.png", 
                        Score = 300,
                        SubTitle = "Copy this number to the clipboard", 
                        Action = c =>
                        {
                            try
                            {
                                Clipboard.SetText(result.Result);
                                return true;
                            }
                            catch (ExternalException e)
                            {
                                MessageBox.Show("Copy failed, please try later");
                                return false;
                            }
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

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_caculator_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_caculator_plugin_description");
        }
    }
}
