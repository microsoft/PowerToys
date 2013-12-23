using System;
using System.Collections;
using System.Collections.Generic;

namespace WinAlfred.Plugin
{
    public class Result
    {
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string IcoPath { get; set; }
        public Action Action { get; set; }
        public int Score { get; set; }
        public List<Result> ContextResults { get; set; }

        /// <summary>
        /// you don't need to set this property if you are developing a plugin
        /// </summary>
        public string PluginDirectory { get; set; }
    }
}