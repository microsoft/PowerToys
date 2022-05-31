// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.OneNote.Properties;
using ScipBe.Common.Office.OneNote;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.OneNote
{
    /// <summary>
    /// A power launcher plugin to search across time zones.
    /// </summary>
    public class Main : IPlugin, IPluginI18n
    {
        /// <summary>
        /// A value indicating if the OneNote interop library was able to successfully initialize.
        /// </summary>
        private readonly bool _oneNoteInstalled;

        /// <summary>
        /// The initial context for this plugin (contains API and meta-data)
        /// </summary>
        private PluginInitContext? _context;

        /// <summary>
        /// The path to the icon for each result
        /// </summary>
        private string _iconPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="Main"/> class.
        /// </summary>
        public Main()
        {
            UpdateIconPath(Theme.Light);
            try
            {
                _ = OneNoteProvider.PageItems.Any();
                _oneNoteInstalled = true;
            }
            catch (COMException)
            {
                // OneNote isn't installed, plugin won't do anything.
                _oneNoteInstalled = false;
            }
        }

        /// <summary>
        /// Gets the localized name.
        /// </summary>
        public string Name => Resources.PluginTitle;

        /// <summary>
        /// Gets the localized description.
        /// </summary>
        public string Description => Resources.PluginDescription;

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        /// <summary>
        /// Return a filtered list, based on the given query
        /// </summary>
        /// <param name="query">The query to filter the list</param>
        /// <returns>A filtered list, can be empty when nothing was found</returns>
        public List<Result> Query(Query query)
        {
            if (!_oneNoteInstalled || query is null || string.IsNullOrWhiteSpace(query.Search))
            {
                return new List<Result>(0);
            }

            var pages = OneNoteProvider.FindPages(query.Search);

            return pages.Select(p => new Result
            {
                IcoPath = _iconPath,
                Title = p.Name,
                QueryTextDisplay = p.Name,
                SubTitle = @$"{p.Notebook.Name}\{p.Section.Name}",
                Action = (_) =>
                {
                    p.OpenInOneNote();
                    ShowOneNote();
                    return true;
                },
                ContextData = p,
                ToolTipData = new ToolTipData(Name, @$"{p.Notebook.Name}\{p.Section.Name}\{p.Name}"),
            }).ToList();
        }

        /// <summary>
        /// Return the translated plugin title.
        /// </summary>
        /// <returns>A translated plugin title.</returns>
        public string GetTranslatedPluginTitle() => Resources.PluginTitle;

        /// <summary>
        /// Return the translated plugin description.
        /// </summary>
        /// <returns>A translated plugin description.</returns>
        public string GetTranslatedPluginDescription() => Resources.PluginDescription;

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        [MemberNotNull(nameof(_iconPath))]
        private void UpdateIconPath(Theme theme)
        {
            _iconPath = theme == Theme.Light || theme == Theme.HighContrastWhite ? "Images/oneNote.light.png" : "Images/oneNote.dark.png";
        }

        /// <summary>
        /// Brings OneNote to the foreground and restores it if minimized.
        /// </summary>
        private void ShowOneNote()
        {
            using var process = Process.GetProcessesByName("onenote").FirstOrDefault();
            if (process?.MainWindowHandle != null)
            {
                HWND handle = (HWND)process.MainWindowHandle;
                if (PInvoke.IsIconic(handle))
                {
                    PInvoke.ShowWindow(handle, SHOW_WINDOW_CMD.SW_RESTORE);
                }

                PInvoke.SetForegroundWindow(handle);
            }
        }
    }
}
