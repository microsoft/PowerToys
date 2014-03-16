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
                PinyinTitle = ChineseToPinYin.ToPinYin(m_Title).Replace(" ", "").ToLower();
            }
        }
        public string PinyinTitle { get; private set; }
        public string IcoPath { get; set; }
        public string ExecutePath { get; set; }
        public int Score { get; set; }

    }

    public class Programs : BaseSystemPlugin
    {
        private List<string> indexDirectory = new List<string>();
        private List<string> indexPostfix = new List<string> { "lnk", "exe" };

        List<Program> installedList = new List<Program>();

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
            if ((program.Score = matcher.Score(program.Title)) > 0) return true;
            if ((program.Score = matcher.Score(program.PinyinTitle)) > 0) return true;

            return false;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            indexDirectory.Add(Environment.GetFolderPath(Environment.SpecialFolder.Programs));

            StringBuilder commonStartMenuPath = new StringBuilder(560);
            SHGetSpecialFolderPath(IntPtr.Zero, commonStartMenuPath, CSIDL_COMMON_PROGRAMS, false);
            indexDirectory.Add(commonStartMenuPath.ToString());

            GetAppFromStartMenu();
        }

        private void GetAppFromStartMenu()
        {
            foreach (string directory in indexDirectory)
            {
                GetAppFromDirectory(directory);
            }
        }

        private void GetAppFromDirectory(string path)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                if (indexPostfix.Any(o => file.EndsWith("." + o)))
                {
                    Program p = new Program()
                    {
                        Title = getAppNameFromAppPath(file),
                        IcoPath = file,
                        ExecutePath = file
                    };
                    installedList.Add(p);
                }
            }

            foreach (var subDirectory in Directory.GetDirectories(path))
            {
                GetAppFromDirectory(subDirectory);
            }
        }

        private void ScoreFilter(Program p)
        {
            if (p.Title.Contains("启动") || p.Title.ToLower().Contains("start"))
            {
                p.Score += 1;
            }
            if (p.Title.Contains("卸载") || p.Title.ToLower().Contains("uninstall"))
            {
                p.Score -= 1;
            }
        }

        private string getAppNameFromAppPath(string app)
        {
            return global::System.IO.Path.GetFileNameWithoutExtension(app);
        }
    }
}
