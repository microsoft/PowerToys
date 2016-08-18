using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace Wox.Plugin
{

    public class Result
    {

        private string _pluginDirectory;
        private string _icoPath;
        public string Title { get; set; }
        public string SubTitle { get; set; }

        public string IcoPath
        {
            get { return _icoPath; }
            set
            {
                if (!string.IsNullOrEmpty(PluginDirectory) && !Path.IsPathRooted(value))
                {
                    _icoPath = Path.Combine(value, IcoPath);
                }
                else
                {
                    _icoPath = value;
                }
            }
        }

        public delegate ImageSource IconDelegate();

        public IconDelegate Icon;


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
        public string PluginDirectory
        {
            get { return _pluginDirectory; }
            set
            {
                _pluginDirectory = value;
                if (!string.IsNullOrEmpty(IcoPath) && !Path.IsPathRooted(IcoPath))
                {
                    IcoPath = Path.Combine(value, IcoPath);
                }
            }
        }

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
                           (SubTitle?.GetHashCode() ?? 0);
            return hashcode;
        }

        public override string ToString()
        {
            return Title + SubTitle;
        }

        [Obsolete("Use IContextMenu instead")]
        /// <summary>
        /// Context menus associate with this result
        /// </summary>
        public List<Result> ContextMenu { get; set; }

        [Obsolete("Use Object initializers instead")]
        public Result(string Title, string IcoPath, string SubTitle = null)
        {
            this.Title = Title;
            this.IcoPath = IcoPath;
            this.SubTitle = SubTitle;
        }

        public Result() { }

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