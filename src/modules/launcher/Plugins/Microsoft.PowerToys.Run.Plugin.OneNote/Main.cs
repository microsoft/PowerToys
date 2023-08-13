// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LazyCache;
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
    public class Main : IPlugin, IDelayedExecutionPlugin, IPluginI18n
    {
        /// <summary>
        /// A value indicating if the OneNote interop library was able to successfully initialize.
        /// </summary>
        private bool _oneNoteInstalled;

        /// <summary>
        /// LazyCache CachingService instance to speed up repeated queries.
        /// </summary>
        private CachingService? _cache;

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
        /// Gets the plugin ID for validation
        /// </summary>
        public static string PluginID => "0778F0C264114FEC8A3DF59447CF0A74";

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            try
            {
                _ = OneNoteProvider.PageItems.Any();
                _oneNoteInstalled = true;

                _cache = new CachingService();
                _cache.DefaultCachePolicy.DefaultCacheDurationSeconds = (int)TimeSpan.FromDays(1).TotalSeconds;
            }
            catch (COMException)
            {
                // OneNote isn't installed, plugin won't do anything.
                _oneNoteInstalled = false;
            }

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
            if (!_oneNoteInstalled || query is null || string.IsNullOrWhiteSpace(query.Search) || _cache is null)
            {
                return new List<Result>(0);
            }

            // If there's cached results for this query, return immediately, otherwise wait for delayedExecution.
            var results = _cache.Get<List<Result>>(query.Search);
            return results ?? Query(query, false);
        }

        /// <summary>
        /// Return a filtered list, based on the given query
        /// </summary>
        /// <param name="query">The query to filter the list</param>
        /// <param name="delayedExecution">False if this is the first pass through plugins, true otherwise. Slow plugins should run delayed.</param>
        /// <returns>A filtered list, can be empty when nothing was found</returns>
        public List<Result> Query(Query query, bool delayedExecution)
        {
            if (!delayedExecution || !_oneNoteInstalled || query is null || string.IsNullOrWhiteSpace(query.Search) || _cache is null)
            {
                return new List<Result>(0);
            }

            // Get results from cache if they already exist for this query, otherwise query OneNote. Results will be cached for 1 day.
            var results = _cache.GetOrAdd(query.Search, () =>
            {
                var pages = OneNoteProvider.FindPages(query.Search);

                return pages.Select(p => new Result
                {
                    IcoPath = _iconPath,
                    Title = p.Name,
                    QueryTextDisplay = p.Name,
                    SubTitle = @$"{p.Notebook.Name}\{p.Section.Name}",
                    Action = (_) => OpenPageInOneNote(p),
                    ContextData = p,
                    ToolTipData = new ToolTipData(Name, @$"{p.Notebook.Name}\{p.Section.Name}\{p.Name}"),
                }).ToList();
            });

            return results;
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

        private bool OpenPageInOneNote(IOneNoteExtPage page)
        {
            try
            {
                page.OpenInOneNote();
                ShowOneNote();
                return true;
            }
            catch (COMException)
            {
                // The page, section or even notebook may no longer exist, ignore and do nothing.
                return false;
            }
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
