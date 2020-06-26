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


namespace Microsoft.Plugin.Program
{
    public class Main : ISettingProvider, IPlugin, IPluginI18n, IContextMenu, ISavable, IReloadable
    {
        private static readonly object IndexLock = new object();
        internal static Programs.Win32[] _win32s { get; set; }
        internal static Programs.UWP.Application[] _uwps { get; set; }
        internal static Settings _settings { get; set; }

        FileSystemWatcher _watcher = null;
        System.Timers.Timer _timer = null;

        private static bool IsStartupIndexProgramsRequired => _settings.LastIndexTime.AddDays(3) < DateTime.Today;

        private static PluginInitContext _context;

        private static BinaryStorage<Programs.Win32[]> _win32Storage;
        private static BinaryStorage<Programs.UWP.Application[]> _uwpStorage;
        private readonly PluginJsonStorage<Settings> _settingsStorage;

        public Main()
        {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();

            Stopwatch.Normal("|Microsoft.Plugin.Program.Main|Preload programs cost", () =>
            {
                _win32Storage = new BinaryStorage<Programs.Win32[]>("Win32");
                _win32s = _win32Storage.TryLoad(new Programs.Win32[] { });
                _uwpStorage = new BinaryStorage<Programs.UWP.Application[]>("UWP");
                _uwps = _uwpStorage.TryLoad(new Programs.UWP.Application[] { });
            });
            Log.Info($"|Microsoft.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Length}>");
            Log.Info($"|Microsoft.Plugin.Program.Main|Number of preload uwps <{_uwps.Length}>");

            var a = Task.Run(() =>
            {
                if (IsStartupIndexProgramsRequired || !_win32s.Any())
                    Stopwatch.Normal("|Microsoft.Plugin.Program.Main|Win32Program index cost", IndexWin32Programs);
            });

            var b = Task.Run(() =>
            {
                if (IsStartupIndexProgramsRequired || !_uwps.Any())
                    Stopwatch.Normal("|Microsoft.Plugin.Program.Main|Win32Program index cost", IndexUWPPrograms);
            });

            Task.WaitAll(a, b);

            _settings.LastIndexTime = DateTime.Today;

            InitializeFileWatchers();
            InitializeTimer();
        }

        public void Save()
        {
            _settingsStorage.Save();
            _win32Storage.Save(_win32s);
            _uwpStorage.Save(_uwps);
        }

        public List<Result> Query(Query query)
        {
            Programs.Win32[] win32;
            Programs.UWP.Application[] uwps;

            lock (IndexLock)
            {
                // just take the reference inside the lock to eliminate query time issues.
                win32 = _win32s;
                uwps = _uwps;
            }

            var results1 = win32.AsParallel()
                .Where(p => p.Enabled)
                .Select(p => p.Result(query.Search, _context.API));

            var results2 = uwps.AsParallel()
                .Where(p => p.Enabled)
                .Select(p => p.Result(query.Search, _context.API));

            var result = results1.Concat(results2).Where(r => r != null && r.Score > 0).ToList();
            return result;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public static void IndexWin32Programs()
        {
            var win32S = Programs.Win32.All(_settings);
            lock (IndexLock)
            {
                _win32s = win32S;
            }
        }

        public static void IndexUWPPrograms()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;

            var applications = support ? Programs.UWP.All() : new Programs.UWP.Application[] { };
            lock (IndexLock)
            {
                _uwps = applications;
            }
        }

        public static void IndexPrograms()
        {
            var t1 = Task.Run(() => IndexWin32Programs());
            var t2 = Task.Run(() => IndexUWPPrograms());

            Task.WaitAll(t1, t2);

            _settings.LastIndexTime = DateTime.Today;
        }

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(_context, _settings, _win32s, _uwps);
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
        void InitializeFileWatchers()
        {
            // Create a new FileSystemWatcher and set its properties.
            _watcher = new FileSystemWatcher();
            var resolvedPath = Environment.ExpandEnvironmentVariables("%ProgramFiles%");
            _watcher.Path = resolvedPath;

            //Filter to create and deletes of 'microsoft.system.package.metadata' directories. 
            _watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;
            _watcher.IncludeSubdirectories = true;

            // Add event handlers.
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;

            // Begin watching.
            _watcher.EnableRaisingEvents = true;
        }

        void InitializeTimer()
        {
            //multiple file writes occur on install / uninstall.  Adding a delay before actually indexing.
            var delayInterval = 5000; 
            _timer = new System.Timers.Timer(delayInterval);
            _timer.Enabled = true;
            _timer.AutoReset = false;
            _timer.Elapsed += FileWatchElapsedTimer;
            _timer.Stop();
        }

        //When a watched directory changes then reset the timer.
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            Log.Debug($"|Microsoft.Plugin.Program.Main|Directory Changed: {e.FullPath} {e.ChangeType} - Resetting timer.");
            _timer.Stop();
            _timer.Start();
        }

        private void FileWatchElapsedTimer(object sender, ElapsedEventArgs e)
        {
            Task.Run(() =>
            {
                Log.Debug($"|Microsoft.Plugin.Program.Main| ReIndexing UWP Programs");
                IndexUWPPrograms();
                Log.Debug($"|Microsoft.Plugin.Program.Main| Done ReIndexing");
            });
        }
    }
}