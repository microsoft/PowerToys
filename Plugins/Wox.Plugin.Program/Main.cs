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
                                   .Where(p => Score(p, query.Search) > 0)
                                   .Select(ResultFromWin32);
            var results2 = _uwps.AsParallel()
                                .Where(app => Score(app, query.Search) > 0)
                                .Select(ResultFromUWP);
            var result = results1.Concat(results2).ToList();
            return result;
        }

        public Result ResultFromWin32(Win32 program)
        {
            var result = new Result
            {
                SubTitle = program.FullPath,
                IcoPath = program.IcoPath,
                Score = program.Score,
                ContextData = program,
                Action = e =>
                {
                    var info = new ProcessStartInfo
                    {
                        FileName = program.FullPath,
                        WorkingDirectory = program.ParentDirectory
                    };
                    var hide = StartProcess(info);
                    return hide;
                }
            };

            if (program.Description.Length >= program.Name.Length &&
                program.Description.Substring(0, program.Name.Length) == program.Name)
            {
                result.Title = program.Description;
            }
            else if (!string.IsNullOrEmpty(program.Description))
            {
                result.Title = $"{program.Name}: {program.Description}";
            }
            else
            {
                result.Title = program.Name;
            }

            return result;
        }
        public Result ResultFromUWP(UWP.Application app)
        {
            var result = new Result
            {
                SubTitle = $"{app.Location}",
                Icon = app.Logo,
                Score = app.Score,
                ContextData = app,
                Action = e =>
                {
                    app.Launch();
                    return true;
                }
            };

            if (app.Description.Length >= app.DisplayName.Length &&
                app.Description.Substring(0, app.DisplayName.Length) == app.DisplayName)
            {
                result.Title = app.Description;
            }
            else if (!string.IsNullOrEmpty(app.Description))
            {
                result.Title = $"{app.DisplayName}: {app.Description}";
            }
            else
            {
                result.Title = app.DisplayName;
            }
            return result;
        }


        private int Score(Win32 program, string query)
        {
            var score1 = StringMatcher.Score(program.Name, query);
            var score2 = StringMatcher.ScoreForPinyin(program.Name, query);
            var score3 = StringMatcher.Score(program.Description, query);
            var score4 = StringMatcher.ScoreForPinyin(program.Description, query);
            var score5 = StringMatcher.Score(program.ExecutableName, query);
            var score = new[] { score1, score2, score3, score4, score5 }.Max();
            program.Score = score;
            return score;
        }

        private int Score(UWP.Application app, string query)
        {
            var score1 = StringMatcher.Score(app.DisplayName, query);
            var score2 = StringMatcher.ScoreForPinyin(app.DisplayName, query);
            var score3 = StringMatcher.Score(app.Description, query);
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
                                FileName = p.FullPath,
                                WorkingDirectory = p.ParentDirectory,
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
                            var hide = StartProcess(new ProcessStartInfo(p.ParentDirectory));
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