using System;
using System.Text.RegularExpressions;
using System.Threading;
using Wox.Infrastructure;

namespace Wox.Plugin.Program
{
    [Serializable]
    public class Program
    {
        private static readonly Regex AbbrRegexp = new Regex("[^A-Z0-9]", RegexOptions.Compiled);
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
                string pinyin = m_Title.Unidecode();
                PinyinTitle = pinyin;
                AbbrTitle = AbbrRegexp.Replace(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(pinyin), "");
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
}