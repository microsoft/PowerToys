using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WinAlfred.Plugin.System
{
    public class Program
    {
        public string Title { get; set; }
        public string IcoPath { get; set; }
        public string ExecutePath { get; set; }
    }

    public class Programs : ISystemPlugin
    {
        List<Program> installedList = new List<Program>();

        public List<Result> Query(Query query)
        {
            if (string.IsNullOrEmpty(query.RawQuery) || query.RawQuery.Length <= 1) return new List<Result>();

            return installedList.Where(o => o.Title.ToLower().Contains(query.RawQuery.ToLower())).Select(c => new Result()
            {
                Title = c.Title,
                IcoPath = c.IcoPath,
                Score = 10,
                Action = () =>
                {
                    if (string.IsNullOrEmpty(c.ExecutePath))
                    {
                        MessageBox.Show("couldn't start" + c.Title);
                    }
                    else
                    {
                        Process.Start(c.ExecutePath);
                    }
                }
            }).ToList();
        }

        public void Init(PluginInitContext context)
        {
            GetAppFromStartMenu();
        }

        private void GetAppFromStartMenu()
        {
            List<string> path =
                Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.Programs)).ToList();
            foreach (string s in path)
            {
                GetAppFromDirectory(s);
            }
        }

        private void GetAppFromDirectory(string path)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                if (file.EndsWith(".lnk") || file.EndsWith(".exe"))
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

        private string getAppNameFromAppPath(string app)
        {
            string temp = app.Substring(app.LastIndexOf('\\') + 1);
            string name = temp.Substring(0, temp.LastIndexOf('.'));
            return name;
        }

        public string Name
        {
            get
            {
                return "Programs";
            }
        }

        public string Description
        {
            get
            {
                return "get system programs";
            }
        }
    }
}