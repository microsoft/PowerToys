using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;

namespace Wox.Plugin.System
{
    public class CMD : BaseSystemPlugin
    {
        private Dictionary<string, int> cmdHistory = new Dictionary<string, int>();
        private string filePath = Directory.GetCurrentDirectory() + "\\CMDHistory.dat";
        private PluginInitContext context;

        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();
            if (query.RawQuery == ">")
            {
                IEnumerable<Result> history = cmdHistory.OrderByDescending(o => o.Value)
                 .Select(m => new Result
                 {
                     Title = m.Key,
                     SubTitle = "this command has been executed " + m.Value + " times",
                     IcoPath = "Images/cmd.png",
                     Action = (c) =>
                     {
                         ExecuteCmd(m.Key);
                         return true;
                     }
                 }).Take(5);

                results.AddRange(history);
            }

            if (query.RawQuery.StartsWith(">") && query.RawQuery.Length > 1)
            {
                string cmd = query.RawQuery.Substring(1);
                Result result = new Result
                {
                    Title = cmd,
                    Score = 5000,
                    SubTitle = "execute command through command shell",
                    IcoPath = "Images/cmd.png",
                    Action = (c) =>
                    {
                        ExecuteCmd(cmd);
                        return true;
                    }
                };
                results.Add(result);

                IEnumerable<Result> history = cmdHistory.Where(o => o.Key.Contains(cmd))
                    .OrderByDescending(o => o.Value)
                    .Select(m => new Result
                    {
                        Title = m.Key,
                        SubTitle = "this command has been executed " + m.Value + " times",
                        IcoPath = "Images/cmd.png",
                        Action = (c) =>
                        {
                            ExecuteCmd(m.Key);
                            return true;
                        }
                    }).Take(4);

                results.AddRange(history);
            }
            return results;
        }


        private void ExecuteCmd(string cmd)
        {
            try
            {
                WindowsShellRun.Start(cmd);
                AddCmdHistory(cmd);
            }
            catch (Exception e)
            {
                MessageBox.Show("Wox cound't execute this command. \n\n" + e.Message);
            }
        }

        protected override void InitInternal(PluginInitContext context)
        {
            this.context = context;
            LoadCmdHistory();
        }

        //todo:we need provide a common data persist interface for user?
        private void AddCmdHistory(string cmdName)
        {
            if (cmdHistory.ContainsKey(cmdName))
            {
                cmdHistory[cmdName] += 1;
            }
            else
            {
                cmdHistory.Add(cmdName, 1);
            }
            PersistCmdHistory();
        }

        public void LoadCmdHistory()
        {
            if (File.Exists(filePath))
            {
                FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                BinaryFormatter b = new BinaryFormatter();
                cmdHistory = (Dictionary<string, int>)b.Deserialize(fileStream);
                fileStream.Close();
            }

            if (cmdHistory.Count > 1000)
            {
                List<string> onlyOnceKeys = (from c in cmdHistory where c.Value == 1 select c.Key).ToList();
                foreach (string onlyOnceKey in onlyOnceKeys)
                {
                    cmdHistory.Remove(onlyOnceKey);
                }
            }
        }

        private void PersistCmdHistory()
        {
            FileStream fileStream = new FileStream(filePath, FileMode.Create);
            BinaryFormatter b = new BinaryFormatter();
            b.Serialize(fileStream, cmdHistory);
            fileStream.Close();
        }
    }
}
