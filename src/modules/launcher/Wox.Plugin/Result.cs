// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;

namespace Wox.Plugin
{
    public class Result
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;

        private string _title;
        private ToolTipData _toolTipData;
        private string _pluginDirectory;
        private string _icoPath;

        public PluginMetadata Metadata { get; set; }

        public string Title
        {
            get
            {
                return _title;
            }

            set
            {
                ArgumentNullException.ThrowIfNull(value);

                // Using Ordinal since this is used internally
                _title = value.Replace("\n", " ", StringComparison.Ordinal);
            }
        }

        public bool FromHistory { get; set; }

        public string HistoryPluginID { get; set; }

        public string HistoryTitle { get; set; }

        public string SubTitle { get; set; }

        public string Glyph { get; set; }

        public string FontFamily { get; set; }

        public string ProgramArguments { get; set; }

        public Visibility ToolTipVisibility { get; set; } = Visibility.Collapsed;

        public ToolTipData ToolTipData
        {
            get
            {
                return _toolTipData;
            }

            set
            {
                _toolTipData = value;
                ToolTipVisibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Gets or sets the text that will get displayed in the Search text box, when this item is selected in the result list.
        /// </summary>
        public string QueryTextDisplay { get; set; }

        public string IcoPath
        {
            get
            {
                return _icoPath;
            }

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

        public IconDelegate Icon { get; set; }

        /// <summary>
        /// Gets or sets the result action.
        /// Return <c>true</c> to hide PowerToys Run after the result has been selected.
        /// </summary>
        [JsonIgnore]
        public Func<ActionContext, bool> Action { get; set; }

        public int Score { get; set; }

        public int SelectedCount { get; set; }

        public DateTime LastSelected { get; set; } = DateTime.MinValue;

        public Result(IList<int> titleHighlightData = null, IList<int> subTitleHighlightData = null)
        {
            TitleHighlightData = titleHighlightData;
            SubTitleHighlightData = subTitleHighlightData;
        }

        public IList<int> TitleHighlightData { get; set; }

        /// <summary>
        /// Gets or sets a list of indexes for the characters to be highlighted in SubTitle
        /// </summary>
        public IList<int> SubTitleHighlightData { get; set; }

        /// <summary>
        /// Gets or sets only results that originQuery match with current query will be displayed in the panel
        /// </summary>
        internal Query OriginQuery { get; set; }

        /// <summary>
        /// Gets or sets plugin directory
        /// </summary>
        public string PluginDirectory
        {
            get
            {
                return _pluginDirectory;
            }

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
            var r = obj as Result;

            // Using Ordinal since this is used internally
            var equality = string.Equals(r?.Title, Title, StringComparison.Ordinal) &&
                           string.Equals(r?.SubTitle, SubTitle, StringComparison.Ordinal) &&
                           string.Equals(r?.IcoPath, IcoPath, StringComparison.Ordinal) &&
                           TitleHighlightData == r.TitleHighlightData &&
                           SubTitleHighlightData == r.SubTitleHighlightData;

            return equality;
        }

        public override int GetHashCode()
        {
            // Using Ordinal since this is used internally
            var hashcode = (Title?.GetHashCode(StringComparison.Ordinal) ?? 0) ^
                           (SubTitle?.GetHashCode(StringComparison.Ordinal) ?? 0);
            return hashcode;
        }

        public override string ToString()
        {
            // Using CurrentCulture since this is user facing
            return string.Format(CultureInfo.CurrentCulture, "{0} : {1}", Title, SubTitle);
        }

        /// <summary>
        /// Gets or sets additional data associate with this result
        /// </summary>
        public object ContextData { get; set; }

        /// <summary>
        /// Gets plugin ID that generated this result
        /// </summary>
        public string PluginID { get; internal set; }
    }
}
