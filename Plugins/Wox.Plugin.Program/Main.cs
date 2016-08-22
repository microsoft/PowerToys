using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Program.Programs;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox.Plugin.Program
{
    public class Main : ISettingProvider, IPlugin, IPluginI18n, IContextMenu, ISavable
    {
        private static Win32[] _win32s = { };
        private static UWP.Application[] _uwps = { };

        private static PluginInitContext _context;

        private static ProgramIndexCache _cache;
        private static BinaryStorage<ProgramIndexCache> _cacheStorage;
        private static Settings _settings;
        private readonly PluginJsonStorage<Settings> _settingsStorage;

        public Main()
        {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();

            Stopwatch.Debug("Preload programs", () =>
            {
                _cacheStorage = new BinaryStorage<ProgramIndexCache>();
                _cache = _cacheStorage.Load();
                _win32s = _cache.Programs;
            });
            Log.Info($"Preload {_win32s.Length} programs from cache");
            Stopwatch.Debug("Program Index", IndexPrograms);
        }

        public void Save()
        {
            _settingsStorage.Save();
            _cache.Programs = _win32s;
            _cacheStorage.Save();
        }

        public List<Result> Query(Query query)
        {
            var results1 = _win32s.AsParallel()
                                  .Select(p => p.Result(query.Search, _context.API))
                                  .Where(r => r.Score > 0);
            var results2 = _uwps.AsParallel()
                                .Select(p => p.Result(query.Search, _context.API))
                                .Where(r => r.Score > 0);
            var result = results1.Concat(results2).ToList();
            return result;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public static void IndexPrograms()
        {
            _win32s = Win32.All(_settings);
            _uwps = UWP.All();
        }

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(_context, _settings);
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wox_plugin_program_plugin_description");
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var program = selectedResult.ContextData as IProgram;
            if (program != null)
            {
                var menus = program.ContextMenus(_context.API);
                return menus;
            }
            else
            {
                return new List<Result>();
            }
        }

        public static bool StartProcess(ProcessStartInfo info)
        {
            bool hide;
            try
            {
                Process.Start(info);
                hide = true;
            }
            catch (Exception)
            {
                var name = "Plugin: Program";
                var message = $"Can't start: {info.FileName}";
                _context.API.ShowMsg(name, message, string.Empty);
                hide = false;
            }
            return hide;
        }
    }
}