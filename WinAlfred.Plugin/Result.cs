using System;
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
    }
}