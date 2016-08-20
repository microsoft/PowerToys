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
        private static List<Win32> _programs = new List<Win32>();
        private static List<UWP> _uwps = new List<UWP>();

        private PluginInitContext _context;

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
                _programs = _cache.Programs;
            });
            Log.Info($"Preload {_programs.Count} programs from cache");
            Stopwatch.Debug("Program Index", IndexPrograms);
        }

        public void Save()
        {
            _settingsStorage.Save();
            _cacheStorage.Save();
        }

        public List<Result> Query(Query query)
        {
            var results1 = _programs.AsParallel()
                .Where(p => Score(p, query.Search) > 0)
                .Select(ResultFromProgram);

            var results2 = _uwps.AsParallel()
                .Where(u => Score(u, query.Search) > 0)
                .Select(ResultFromUWPApp);
            var result = results1.Concat(results2).ToList();

            return result;
        }

        public Result ResultFromProgram(Win32 p)
        {
            var result = new Result
            {
                Title = p.Title,
                SubTitle = p.ExecutablePath,
                IcoPath = p.IcoPath,
                Score = p.Score,
                ContextData = p,
                Action = e =>
                {
                    var info = new ProcessStartInfo
                    {
                        FileName = p.ExecutablePath,
                        WorkingDirectory = p.Directory
                    };
                    var hide = StartProcess(info);
                    return hide;
                }
            };
            return result;
        }
        public Result ResultFromUWPApp(UWP uwp)
        {
            var app = uwp.Apps[0];
            var result = new Result
            {
                Title = app.DisplayName,
                SubTitle = $"Windows Store app: {app.Description}",
                Icon = app.Logo,
                Score = uwp.Score,
                ContextData = app,
                Action = e =>
                {
                    app.Launch();
                    return true;
                }
            };
            return result;
        }


        private int Score(Win32 program, string query)
        {
            var score1 = StringMatcher.Score(program.Title, query);
            var score2 = StringMatcher.ScoreForPinyin(program.Title, query);
            var score3 = StringMatcher.Score(program.ExecutableName, query);
            var score = new[] { score1, score2, score3 }.Max();
            program.Score = score;
            return score;
        }

        private int Score(UWP app, string query)
        {
            var score1 = StringMatcher.Score(app.Apps[0].DisplayName, query);
            var score2 = StringMatcher.ScoreForPinyin(app.Apps[0].DisplayName, query);
            var score3 = StringMatcher.Score(app.Apps[0].Description, query);
            var score = new[] { score1, score2, score3 }.Max();
            app.Score = score;
            return score;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
        }

        public static void IndexPrograms()
        {
            _cache.Programs = AllWin32Programs();
            _uwps = UWP.All();
        }

        private static List<Win32> AllWin32Programs()
        {
            var appPaths = AppPathsPrograms.All();
            var startMenu = StartMenu.All(_settings.ProgramSuffixes);
            var unregistered = UnregisteredPrograms.All(_settings.ProgramSources, _settings.ProgramSuffixes);
            var programs = appPaths.Concat(startMenu).Concat(unregistered);

            programs = programs.AsParallel()
                                // filter duplicate program
                               .GroupBy(x => new { ExecutePath = x.ExecutablePath, ExecuteName = x.ExecutableName })
                               .Select(g => g.First())
                               .Select(ScoreFilter);

            return programs.ToList();
        }

        private static Win32 ScoreFilter(Win32 p)
        {
            var start = new[] { "启动", "start" };
            var doc = new[] { "帮助", "help", "文档", "documentation" };
            var uninstall = new[] { "卸载", "uninstall" };

            var contained = start.Any(s => p.Title.ToLower().Contains(s));
            if (contained)
            {
                p.Score += 10;
            }
            contained = doc.Any(d => p.Title.ToLower().Contains(d));
            if (contained)
            {
                p.Score -= 10;
            }
            contained = uninstall.Any(u => p.Title.ToLower().Contains(u));
            if (contained)
            {
                p.Score -= 20;
            }

            return p;
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
            Win32 p = selectedResult.ContextData as Win32;
            if (p != null)
            {
                List<Result> contextMenus = new List<Result>
                {
                    new Result
                    {
                        Title = _context.API.GetTranslation("wox_plugin_program_run_as_administrator"),
                        Action = _ =>
                        {
                            var info = new ProcessStartInfo
                            {
                                FileName = p.ExecutablePath,
                                WorkingDirectory = p.Directory,
                                Verb = "runas"
                            };
                            var hide = StartProcess(info);
                            return hide;
                        },
                        IcoPath = "Images/cmd.png"
                    },
                    new Result
                    {
                        Title = _context.API.GetTranslation("wox_plugin_program_open_containing_folder"),
                        Action = _ =>
                        {
                            var hide = StartProcess(new ProcessStartInfo(p.Directory));
                            return hide;
                        },
                        IcoPath = "Images/folder.png"
                    }
                };
                return contextMenus;
            }
            else
            {
                return new List<Result>();
            }
        }

        private bool StartProcess(ProcessStartInfo info)
        {
            bool hide;
            try
            {
                Process.Start(info);
                hide = true;
            }
            catch (Win32Exception)
            {
                var name = $"Plugin: {_context.CurrentPluginMetadata.Name}";
                var message = "Can't open this file";
                _context.API.ShowMsg(name, message, string.Empty);
                hide = false;
            }
            return hide;
        }
    }
}