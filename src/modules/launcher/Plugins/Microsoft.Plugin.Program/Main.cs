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
        internal static Settings _settings { get; set; }

        private static bool IsStartupIndexProgramsRequired => _settings.LastIndexTime.AddDays(3) < DateTime.Today;

        private static PluginInitContext _context;

        private readonly PluginJsonStorage<Settings> _settingsStorage;
        private bool _disposed = false;
        private PackageRepository _packageRepository = new PackageRepository(new PackageCatalogWrapper(), new BinaryStorage<IList<UWP.Application>>("UWP"));
        private static Win32ProgramFileSystemWatchers _win32ProgramRepositoryHelper;
        private static Win32ProgramRepository _win32ProgramRepository;

        public Main()
        {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();
            // This helper class initializes the file system watchers based on the locations to watch
            _win32ProgramRepositoryHelper = new Win32ProgramFileSystemWatchers();

            // Initialize the Win32ProgramRepository with the settings object
            _win32ProgramRepository = new Win32ProgramRepository(_win32ProgramRepositoryHelper._fileSystemWatchers.Cast<IFileSystemWatcherWrapper>().ToList(), new BinaryStorage<IList<Programs.Win32>>("Win32"), _settings, _win32ProgramRepositoryHelper._pathsToWatch);

            Stopwatch.Normal("|Microsoft.Plugin.Program.Main|Preload programs cost", () =>
            {
                _win32ProgramRepository.Load();
                _packageRepository.Load();
            });
            Log.Info($"|Microsoft.Plugin.Program.Main|Number of preload win32 programs <{_win32ProgramRepository.Count()}>");

            var a = Task.Run(() =>
            {
                if (IsStartupIndexProgramsRequired || !_win32ProgramRepository.Any())
                    Stopwatch.Normal("|Microsoft.Plugin.Program.Main|Win32Program index cost", _win32ProgramRepository.IndexPrograms);
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
            _win32ProgramRepository.Save();
            _packageRepository.Save();
        }

        public List<Result> Query(Query query)
        {
            var results1 = _win32ProgramRepository.AsParallel()
                .Where(p => p.Enabled)
                .Select(p => p.Result(query.Search, _context.API));

            var results2 = _packageRepository.AsParallel()
                .Where(p => p.Enabled)
                .Select(p => p.Result(query.Search, _context.API));

            var result = results1.Concat(results2).Where(r => r != null && r.Score > 0);
            var maxScore = result.Max(x => x.Score);
            result = result.Where(x => x.Score > _settings.MinScoreThreshold * maxScore);

            return result.ToList();
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

        public void IndexPrograms()
        {
            var t1 = Task.Run(() => _win32ProgramRepository.IndexPrograms());
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
                    _win32ProgramRepositoryHelper.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}