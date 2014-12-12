using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Wox.Plugin
{

    public class Result
    {

        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string IcoPath { get; set; }

        public string FullIcoPath
        {
            get
            {
                if (string.IsNullOrEmpty(IcoPath)) return string.Empty;
                if (IcoPath.StartsWith("data:"))
                {
                    return IcoPath;
                }

                return Path.Combine(PluginDirectory, IcoPath);
            }
        }

        /// <summary>
        /// return true to hide wox after select result
        /// </summary>
        public Func<ActionContext, bool> Action { get; set; }

        public int Score { get; set; }

        /// <summary>
        /// Auto add scores for MRU items
        /// </summary>
        public bool AutoAjustScore { get; set; }

        //todo: this should be controlled by system, not visible to users
        /// <summary>
        /// Only resulsts that originQuery match with curren query will be displayed in the panel
        /// </summary>
        public Query OriginQuery { get; set; }

        /// <summary>
        /// Don't set this property if you are developing a plugin
        /// </summary>
        public string PluginDirectory { get; set; }

        public new bool Equals(object obj)
        {
            if (obj == null || !(obj is Result)) return false;

            Result r = (Result)obj;
            return r.Title == Title && r.SubTitle == SubTitle;
        }

        public override string ToString()
        {
            return Title + SubTitle;
        }

        public Result()
        {

        }

        public Result(string Title = null, string IcoPath = null, string SubTitle = null)
        {
            this.Title = Title;
            this.IcoPath = IcoPath;
            this.SubTitle = SubTitle;
        }

        public List<Result> ContextMenu { get; set; } 
    }
}