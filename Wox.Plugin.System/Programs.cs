using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using Wox.Infrastructure;

namespace Wox.Plugin.System
{
    public class Program
    {
        private static readonly global::System.Text.RegularExpressions.Regex AbbrRegexp = new global::System.Text.RegularExpressions.Regex("[^A-Z0-9]", global::System.Text.RegularExpressions.RegexOptions.Compiled);
        private string m_Title;
        public string Title
        {
            get
            {
                return m_Title;
            }
            set
            {
                m_Title = value;
                string pinyin = ChineseToPinYin.ToPinYin(m_Title);
                PinyinTitle = pinyin.Replace(" ", "").ToLower();
                AbbrTitle = AbbrRegexp.Replace(global::System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(pinyin), "");
                if (AbbrTitle.Length < 2) AbbrTitle = null;
            }
        }
        public string PinyinTitle { get; private set; }
        public string AbbrTitle { get; private set; }
        public string IcoPath { get; set; }
        public string ExecutePath { get; set; }
        public string ExecuteName { get; set; }
        public int Score { get; set; }
        public IProgramSource Source { get; set; }
    }

    public class Programs : BaseSystemPlugin
    {
        List<Program> installedList = new List<Program>();
        List<IProgramSource> sources = new List<IProgramSource>();

        [DllImport("shell32.dll")]
        static extern bool SHGetSpecialFolderPath(IntPtr hwndOwner,[Out] StringBuilder lpszPath, int nFolder, bool fCreate);
        const int CSIDL_COMMON_STARTMENU = 0x16;  // \Windows\Start Menu\Programs
        const int CSIDL_COMMON_PROGRAMS = 0x17;

        protected override List<Result> QueryInternal(Query query)
        {
            if (string.IsNullOrEmpty(query.RawQuery) || query.RawQuery.EndsWith(" ") || query.RawQuery.Length <= 1) return new List<Result>();

            var fuzzyMather = FuzzyMatcher.Create(query.RawQuery);
            List<Program> returnList = installedList.Where(o => MatchProgram(o, fuzzyMather)).ToList();
            returnList.ForEach(ScoreFilter);
            //return ordered list instead of return the score, because programs scores will affect other
            //plugins, the weight of program should be less than the plugins when they showed at the same time.
            returnList = returnList.OrderByDescending(o => o.Score).ToList();

            return returnList.Select(c => new Result()
            {
                Title = c.Title,
                SubTitle = c.ExecutePath,
                IcoPath = c.IcoPath,
                Score = 0,
                Action = (context) =>
                {
                    if (string.IsNullOrEmpty(c.ExecutePath))
                    {
                        MessageBox.Show("couldn't start" + c.Title);
                    }
                    else
                    {
                        try
                        {
                            Process.Start(c.ExecutePath);
                        }
                        catch (Win32Exception)
                        {
                            //Do nothing.
                            //It may be caused if UAC blocks the program.
                        }
                        catch (Exception e)
                        {
                            throw e;
                        }
                    }
                    return true;
                }
            }).ToList();
        }

        private bool MatchProgram(Program program, FuzzyMatcher matcher)
        {
            if (program.AbbrTitle != null && (program.Score = matcher.Score(program.AbbrTitle)) > 0) return true;
            if ((program.Score = matcher.Score(program.Title)) > 0) return true;
            if ((program.Score = matcher.Score(program.PinyinTitle)) > 0) return true;
            if (program.ExecuteName != null && (program.Score = matcher.Score(program.ExecuteName)) > 0) return true;

            return false;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            sources.Add(new FileSystemProgramSource(Environment.GetFolderPath(Environment.SpecialFolder.Programs)));

            StringBuilder commonStartMenuPath = new StringBuilder(560);
            SHGetSpecialFolderPath(IntPtr.Zero, commonStartMenuPath, CSIDL_COMMON_PROGRAMS, false);
            sources.Add(new FileSystemProgramSource(commonStartMenuPath.ToString()));

            sources.Add(new AppPathsProgramSource());

            foreach (var source in sources)
            {
                var list = source.LoadPrograms();
                list.ForEach(o =>
                {
                    o.Source = source;
                });
                installedList.AddRange(list);
            }
        }

        private void ScoreFilter(Program p)
        {
            p.Score += p.Source.BonusPoints;

            if (p.Title.Contains("启动") || p.Title.ToLower().Contains("start"))
                p.Score += 10;

            if (p.Title.Contains("帮助") || p.Title.ToLower().Contains("help") || p.Title.Contains("文档") || p.Title.ToLower().Contains("documentation"))
                p.Score -= 10;

            if (p.Title.Contains("卸载") || p.Title.ToLower().Contains("uninstall"))
                p.Score -= 20;
        }
    }
}
