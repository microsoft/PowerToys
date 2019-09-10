using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Program.Programs;
using Wox.Plugin.Program.Views;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox.Plugin.Program
{
    public class Main : ISettingProvider, IPlugin, IPluginI18n, IContextMenu, ISavable
    {
        private static readonly object IndexLock = new object();
        internal static Win32[] _win32s { get; set; }
        internal static UWP.Application[] _uwps { get; set; }
        internal static Settings _settings { get; set; }

        private static PluginInitContext _context;

        private static BinaryStorage<Win32[]> _win32Storage;
        private static BinaryStorage<UWP.Application[]> _uwpStorage;        
        private readonly PluginJsonStorage<Settings> _settingsStorage;

        public Main()
        {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();

            Stopwatch.Normal("|Wox.Plugin.Program.Main|Preload programs cost", () =>
            {
                _win32Storage = new BinaryStorage<Win32[]>("Win32");
                _win32s = _win32Storage.TryLoad(new Win32[] { });
                _uwpStorage = new BinaryStorage<UWP.Application[]>("UWP");
                _uwps = _uwpStorage.TryLoad(new UWP.Application[] { });
            });
            Log.Info($"|Wox.Plugin.Program.Main|Number of preload win32 programs <{_win32s.Length}>");
            Log.Info($"|Wox.Plugin.Program.Main|Number of preload uwps <{_uwps.Length}>");
                        
            var a = Task.Run(() =>
            {
                if (!_win32s.Any())
                    Stopwatch.Normal("|Wox.Plugin.Program.Main|Win32Program index cost", IndexWin32Programs);
            });

            var b = Task.Run(() =>
            {
                if (!_uwps.Any())
                    Stopwatch.Normal("|Wox.Plugin.Program.Main|Win32Program index cost", IndexUWPPrograms);
            });

            Task.WaitAll(a, b);            
        }

        public void Save()
        {
            _settingsStorage.Save();
            _win32Storage.Save(_win32s);
            _uwpStorage.Save(_uwps);
        }

        public List<Result> Query(Query query)
        {
            lock (IndexLock)
            {
                var results1 = _win32s.AsParallel()
                    .Where(p => p.Enabled)
                    .Select(p => p.Result(query.Search, _context.API));

                var results2 = _uwps.AsParallel()
                    .Where(p => p.Enabled)
                    .Select(p => p.Result(query.Search, _context.API));

                var result = results1.Concat(results2).Where(r => r.Score > 0).ToList();
                return result;
            }
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public static void IndexWin32Programs()
        {
            lock (IndexLock)
            {
                _win32s = Win32.All(_settings);
            }
        }

        public static void IndexUWPPrograms()
        {
            var windows10 = new Version(10, 0);
            var support = Environment.OSVersion.Version.Major >= windows10.Major;

            lock (IndexLock)
            {
                _uwps = support ? UWP.All() : new UWP.Application[] { };

                //_uwps = UWP.RetainApplications(allUWPs, _settings.ProgramSources, _settings.EnableProgramSourceOnly);
            }
        }

        public static void IndexPrograms()
        {
            var t1 = Task.Run(() => { IndexWin32Programs(); });

            var t2 = Task.Run(() => { IndexUWPPrograms(); });

            Task.WaitAll(t1, t2);
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