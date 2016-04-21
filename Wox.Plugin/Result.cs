using System;
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
        /// Only resulsts that originQuery match with curren query will be displayed in the panel
        /// </summary>
        internal Query OriginQuery { get; set; }

        /// <summary>
        /// Plugin directory
        /// </summary>
        public string PluginDirectory { get; internal set; }

        public override bool Equals(object obj)
        {
            Result r = obj as Result;
            if (r != null)
            {
                var equality = string.Equals(r.Title, Title) &&
                               string.Equals(r.SubTitle, SubTitle);
                return equality;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            var hashcode = (Title?.GetHashCode() ?? 0) ^
                           (SubTitle?.GetHashCode() ?? 0) ;
            return hashcode;
        }

        public override string ToString()
        {
            return Title + SubTitle;
        }

        public Result(string Title = null, string IcoPath = null, string SubTitle = null)
        {
            this.Title = Title;
            this.IcoPath = IcoPath;
            this.SubTitle = SubTitle;
        }

        [Obsolete("Use IContextMenu instead")]
        /// <summary>
        /// Context menus associate with this result
        /// </summary>
        public List<Result> ContextMenu { get; set; }

        /// <summary>
        /// Additional data associate with this result
        /// </summary>
        public object ContextData { get; set; }

        /// <summary>
        /// Plugin ID that generate this result
        /// </summary>
        public string PluginID { get; set; }
    }
}