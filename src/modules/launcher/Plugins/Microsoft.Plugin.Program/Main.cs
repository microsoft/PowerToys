using Microsoft.PowerToys.Settings.UI.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Controls;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Microsoft.Plugin.Program.Views;

using Stopwatch = Wox.Infrastructure.Stopwatch;
using Windows.ApplicationModel;
using Microsoft.Plugin.Program.Storage;
using Microsoft.Plugin.Program.Programs;

namespace Microsoft.Plugin.Program
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISavable, IReloadable, IDisposable
    {
        private static readonly object IndexLock = new object();
        internal static Programs.Win32[] _win32s { get; set; }
        internal static Settings _settings { get; set; }

        private static bool IsStartupIndexProgramsRequired => _settings.LastIndexTime.AddDays(3) < DateTime.Today;

        private static PluginInitContext _context;

        private static BinaryStorage<Programs.Win32[]> _win32Storage;
        private readonly PluginJsonStorage<Settings> _settingsStorage;
        private bool _disposed = false;
        private PackageRepository _packageRepository = new PackageRepository(new PackageCatalogWrapper(), new BinaryStorage<IList<UWP.Application>>("UWP"));

        public Main()
        {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();

            Stopwatch.Normal("|Microsoft.Plugin.Program.Main|Preload programs cost", () =>
            {
                _win32Storage = new BinaryStorage<Programs.Win32[]>("Win32");
                _win32s = _win32Storage.TryLoad(new Programs.Win32[] { });

                _packageRepository.Load();
            });
            Log.Info($"|Microsoft.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Length}>");

            var a = Task.Run(() =>
            {
                if (IsStartupIndexProgramsRequired || !_win32s.Any())
                    Stopwatch.Normal("|Microsoft.Plugin.Program.Main|Win32Program index cost", IndexWin32Programs);
            });

            var b = Task.Run(() =>
            {
                if (IsStartupIndexProgramsRequired || !_packageRepository.Any())
                    Stopwatch.Normal("|Microsoft.Plugin.Program.Main|Win32Program index cost", _packageRepository.IndexPrograms);
            });


            Task.WaitAll(a, b);

            _settings.LastIndexTime = DateTime.Today;
        }

        public void Save()
        {
            _settingsStorage.Save();
            _win32Storage.Save(_win32s);
            _packageRepository.Save();
        }

        public List<Result> Query(Query query)
        {
            Programs.Win32[] win32;

            lock (IndexLock)
            {
                // just take the reference inside the lock to eliminate query time issues.
                win32 = _win32s;
            }

            var results1 = win32.AsParallel()
                .Where(p => p.Enabled)
                .Select(p => p.Result(query.Search, _context.API));

            var results2 = _packageRepository.AsParallel()
                .Where(p => p.Enabled)
                .Select(p => p.Result(query.Search, _context.API));

            var result = results1.Concat(results2).Where(r => r != null && r.Score > 0).ToList();
            return result;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateUWPIconPath(_context.API.GetCurrentTheme());
        }

        public void OnThemeChanged(Theme _, Theme currentTheme)
        {
            UpdateUWPIconPath(currentTheme);
        }

        public void UpdateUWPIconPath(Theme theme)
        {
            foreach (UWP.Application app in _packageRepository)
            {
                app.UpdatePath(theme);
            }
        }

        public static void IndexWin32Programs()
        {
            var win32S = Programs.Win32.All(_settings);
            lock (IndexLock)
            {
                _win32s = win32S;
            }
        }



        public void IndexPrograms()
        {
            var t1 = Task.Run(() => IndexWin32Programs());
            var t2 = Task.Run(() => _packageRepository.IndexPrograms());

            Task.WaitAll(t1, t2);

            _settings.LastIndexTime = DateTime.Today;
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_description");
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var menuOptions = new List<ContextMenuResult>();
            var program = selectedResult.ContextData as Programs.IProgram;
            if (program != null)
            {
                menuOptions = program.ContextMenus(_context.API);
            }

            return menuOptions;
        }

        public static void StartProcess(Func<ProcessStartInfo, Process> runProcess, ProcessStartInfo info)
        {
            try
            {
                runProcess(info);
            }
            catch (Exception)
            {
                var name = "Plugin: Program";
                var message = $"Unable to start: {info.FileName}";
                _context.API.ShowMsg(name, message, string.Empty);
            }
        }

        public void ReloadData()
        {
            IndexPrograms();
        }

        public void UpdateSettings(PowerLauncherSettings settings)
        {
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
                    _disposed = true;
                }
            }
        }

    }
}