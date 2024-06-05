// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.Plugin.Program.ProgramArgumentParser;
using Microsoft.Plugin.Program.Programs;
using Microsoft.Plugin.Program.Storage;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Common;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Microsoft.Plugin.Program
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISavable, IDisposable
    {
        // The order of this array is important! The Parsers will be checked in order (index 0 to index Length-1) and the first parser which is able to parse the Query will be used
        // NoArgumentsArgumentParser does always succeed and therefore should always be last/fallback
        private static readonly IProgramArgumentParser[] _programArgumentParsers = new IProgramArgumentParser[]
        {
            new DoubleDashProgramArgumentParser(),
            new InferredProgramArgumentParser(),
            new NoArgumentsArgumentParser(),
        };

        internal static ProgramPluginSettings Settings { get; set; }

        internal static readonly ShellLocalization ShellLocalizationHelper = new();

        public string Name => Properties.Resources.wox_plugin_program_plugin_name;

        public string Description => Properties.Resources.wox_plugin_program_plugin_description;

        public static string PluginID => "791FC278BA414111B8D1886DFE447410";

        private static PluginInitContext _context;
        private readonly PluginJsonStorage<ProgramPluginSettings> _settingsStorage;
        private bool _disposed;
        private PackageRepository _packageRepository;
        private static Win32ProgramFileSystemWatchers _win32ProgramRepositoryHelper;
        private static Win32ProgramRepository _win32ProgramRepository;

        public Main()
        {
            _settingsStorage = new PluginJsonStorage<ProgramPluginSettings>();
            Settings = _settingsStorage.Load();

            // This helper class initializes the file system watchers based on the locations to watch
            _win32ProgramRepositoryHelper = new Win32ProgramFileSystemWatchers();

            // Initialize the Win32ProgramRepository with the settings object
            _win32ProgramRepository = new Win32ProgramRepository(_win32ProgramRepositoryHelper.FileSystemWatchers.Cast<IFileSystemWatcherWrapper>().ToList(), Settings, _win32ProgramRepositoryHelper.PathsToWatch);
        }

        public void Save()
        {
            _settingsStorage.Save();
        }

        public List<Result> Query(Query query)
        {
            var sources = _programArgumentParsers
                .Where(programArgumentParser => programArgumentParser.Enabled);

            foreach (var programArgumentParser in sources)
            {
                if (!programArgumentParser.TryParse(query, out var program, out var programArguments))
                {
                    continue;
                }

                return Query(program, programArguments).ToList();
            }

            return new List<Result>(0);
        }

        private IEnumerable<Result> Query(string program, string programArguments)
        {
            var result = _win32ProgramRepository
                .Concat<IProgram>(_packageRepository)
                .AsParallel()
                .Where(p => p.Enabled)
                .Select(p => p.Result(program, programArguments, _context.API))
                .Where(r => r?.Score > 0)
                .ToArray();

            if (result.Length != 0)
            {
                var maxScore = result.Max(x => x.Score);
                return result
                    .Where(x => x.Score > Settings.MinScoreThreshold * maxScore);
            }

            return Enumerable.Empty<Result>();
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            _packageRepository = new PackageRepository(new PackageCatalogWrapper(), _context);

            var a = Task.Run(() =>
            {
                Stopwatch.Normal("Microsoft.Plugin.Program.Main - Win32Program index cost", _win32ProgramRepository.IndexPrograms);
            });

            var b = Task.Run(() =>
            {
                Stopwatch.Normal("Microsoft.Plugin.Program.Main - Package index cost", _packageRepository.IndexPrograms);
                UpdateUWPIconPath(_context.API.GetCurrentTheme());
            });

            Task.WaitAll(a, b);

            Settings.LastIndexTime = DateTime.Today;
        }

        public void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateUWPIconPath(newTheme);
        }

        public void UpdateUWPIconPath(Theme theme)
        {
            if (_packageRepository != null)
            {
                foreach (UWPApplication app in _packageRepository)
                {
                    app.UpdateLogoPath(theme);
                }
            }
        }

        public void IndexPrograms()
        {
            var t1 = Task.Run(() => _win32ProgramRepository.IndexPrograms());
            var t2 = Task.Run(() => _packageRepository.IndexPrograms());

            Task.WaitAll(t1, t2);

            Settings.LastIndexTime = DateTime.Today;
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.wox_plugin_program_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.wox_plugin_program_plugin_description;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            ArgumentNullException.ThrowIfNull(selectedResult);

            var menuOptions = new List<ContextMenuResult>();
            if (selectedResult.ContextData is IProgram program)
            {
                menuOptions = program.ContextMenus(selectedResult.ProgramArguments, _context.API);
            }

            return menuOptions;
        }

        public static void StartProcess(Func<ProcessStartInfo, Process> runProcess, ProcessStartInfo info)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(runProcess);

                ArgumentNullException.ThrowIfNull(info);

                runProcess(info);
            }
            catch (Exception ex)
            {
                Logger.ProgramLogger.Exception($"Unable to start ", ex, System.Reflection.MethodBase.GetCurrentMethod().DeclaringType, info?.FileName);
                var name = "Plugin: " + Properties.Resources.wox_plugin_program_plugin_name;
                var message = $"{Properties.Resources.powertoys_run_plugin_program_start_failed}: {info?.FileName}";
                _context.API.ShowMsg(name, message, string.Empty);
            }
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
                    if (_context != null && _context.API != null)
                    {
                        _context.API.ThemeChanged -= OnThemeChanged;
                    }

                    _win32ProgramRepositoryHelper?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
