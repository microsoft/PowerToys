// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Windows.Controls;
using Microsoft.PowerToys.Run.Plugin.OneNote.Components;
using Microsoft.PowerToys.Run.Plugin.OneNote.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Odotocodot.OneNote.Linq;
using Wox.Plugin;
using Timer = System.Timers.Timer;

namespace Microsoft.PowerToys.Run.Plugin.OneNote
{
    /// <summary>
    /// A power launcher plugin to search across time zones.
    /// </summary>
    public class Main : IPlugin, IDelayedExecutionPlugin, IPluginI18n, ISettingProvider, IContextMenu, IDisposable
    {
        private readonly Timer _comObjectTimeout = new();

        private readonly OneNoteSettings _settings = new();

        /// <summary>
        /// A value indicating if the OneNote interop library was able to successfully initialize.
        /// </summary>
        private bool _oneNoteInstalled;

        /// <summary>
        /// The initial context for this plugin (contains API and meta-data)
        /// </summary>
        private PluginInitContext? _context;

        private SearchManager? _searchManager;
        private IconProvider? _iconProvider;
        private ResultCreator? _resultCreator;

        private bool _disposed;

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

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => _settings.AdditionalOptions;

        /// <summary>
        /// Initialize the plugin with the given <see cref="PluginInitContext"/>
        /// </summary>
        /// <param name="context">The <see cref="PluginInitContext"/> for this plugin</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            try
            {
                OneNoteApplication.InitComObject();
                _oneNoteInstalled = OneNoteApplication.HasComObject;
                OneNoteApplication.ReleaseComObject();
            }
            catch (COMException)
            {
                // OneNote isn't installed, plugin won't do anything.
                _oneNoteInstalled = false;
            }

            _comObjectTimeout.Elapsed += ComObjectTimer_Elapsed;
            _comObjectTimeout.AutoReset = false;
            _comObjectTimeout.Enabled = false;

            _iconProvider = new IconProvider(_context, _settings);
            _resultCreator = new ResultCreator(_context, _settings, _iconProvider);
            _searchManager = new SearchManager(_context, _settings, _iconProvider, _resultCreator);
        }

        /// <summary>
        /// Return a filtered list, based on the given query
        /// </summary>
        /// <param name="query">The query to filter the list</param>
        /// <returns>A filtered list, can be empty when nothing was found</returns>
        public List<Result> Query(Query query)
        {
            if (_resultCreator is null)
            {
                return [];
            }

            if (!_oneNoteInstalled)
            {
                return _resultCreator.OneNoteNotInstalled();
            }

            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return _resultCreator.EmptyQuery(query);
            }

            // If a COM Object has been acquired results return faster
            if (OneNoteApplication.HasComObject)
            {
                ResetTimeout();
                return _searchManager is null ? [] : _searchManager.Query(query);
            }

            return [];
        }

        /// <summary>
        /// Return a filtered list, based on the given query
        /// </summary>
        /// <param name="query">The query to filter the list</param>
        /// <param name="delayedExecution">False if this is the first pass through plugins, true otherwise. Slow plugins should run delayed.</param>
        /// <returns>A filtered list, can be empty when nothing was found</returns>
        public List<Result> Query(Query query, bool delayedExecution)
        {
            if (_resultCreator is null)
            {
                return [];
            }

            if (!_oneNoteInstalled)
            {
                return _resultCreator.OneNoteNotInstalled();
            }

            if (string.IsNullOrWhiteSpace(query.Search))
            {
                return _resultCreator.EmptyQuery(query);
            }

            if (OneNoteApplication.HasComObject)
            {
                return [];
            }

            ResetTimeout();
            return _searchManager is null ? [] : _searchManager.Query(query);
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

        private void ComObjectTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            OneNoteApplication.ReleaseComObject();
        }

        private void ResetTimeout()
        {
            _comObjectTimeout.Interval = _settings.ComObjectTimeout;
            _comObjectTimeout.Enabled = true;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return _resultCreator is null ? [] : _resultCreator.LoadContextMenu(selectedResult);
        }

        public Control CreateSettingPanel() => throw new NotImplementedException();

        public void UpdateSettings(PowerLauncherPluginSettings settings) => _settings.UpdateSettings(settings);

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _iconProvider?.Cleanup();
                    _comObjectTimeout?.Dispose();
                    OneNoteApplication.ReleaseComObject();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
