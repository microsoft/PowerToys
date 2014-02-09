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
        public string Title { get; set; }
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

            return returnList.Select(c => new Result()
            {
                Title = c.Title,
                IcoPath = c.IcoPath,
                Score = c.Score,
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
                }
            }).ToList();
        }

        private bool MatchProgram(Program program, FuzzyMatcher matcher)
        {
            program.Score = matcher.Score(program.Title);
            if (program.Score > 0) return true;
            program.Score = matcher.Score(ChineseToPinYin.ToPinYin(program.Title).Replace(" ", ""));
            if (program.Score > 0) return true;

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
                        Score = 10,
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
                p.Score += 10;
            }
            if (p.Title.Contains("卸载") || p.Title.ToLower().Contains("uninstall"))
            {
                p.Score -= 5;
            }
        }

        private string getAppNameFromAppPath(string app)
        {
            string temp = app.Substring(app.LastIndexOf('\\') + 1);
            string name = temp.Substring(0, temp.LastIndexOf('.'));
            return name;
        }
    }
}
