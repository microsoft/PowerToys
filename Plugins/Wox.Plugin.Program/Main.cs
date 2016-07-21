using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Program.ProgramSources;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox.Plugin.Program
{
    public class Main : ISettingProvider, IPlugin, IPluginI18n, IContextMenu, ISavable
    {
        private static List<Program> _programs = new List<Program>();

        private PluginInitContext _context;

        private static ProgramIndexCache _cache;
        private static BinaryStorage<ProgramIndexCache> _cacheStorage;
        private static Settings _settings;
        private readonly PluginJsonStorage<Settings> _settingsStorage;

        public Main()
        {
            _settingsStorage = new PluginJsonStorage<Settings>();
            _settings = _settingsStorage.Load();
            _cacheStorage = new BinaryStorage<ProgramIndexCache>();
            _cache = _cacheStorage.Load();
        }

        public void Save()
        {
            _settingsStorage.Save();
            _cacheStorage.Save();
        }

        public List<Result> Query(Query query)
        {
            var results = _programs.AsParallel()
                                   .Where(p => Score(p, query.Search) > 0)
                                   .Select(ScoreFilter)
                                   .OrderByDescending(p => p.Score)
                                   .Select(p => new Result
                                   {
                                       Title = p.Title,
                                       SubTitle = p.Path,
                                       IcoPath = p.IcoPath,
                                       Score = p.Score,
                                       ContextData = p,
                                       Action = e =>
                                       {
                                           var info = new ProcessStartInfo
                                           {
                                               FileName = p.Path,
                                               WorkingDirectory = p.Directory
                                           };
                                           var hide = StartProcess(info);
                                           return hide;
                                       }
                                   }).ToList();
            return results;
        }

        private int Score(Program program, string query)
        {
            var score1 = StringMatcher.Score(program.Title, query);
            var score2 = StringMatcher.ScoreForPinyin(program.Title, query);
            var score3 = StringMatcher.Score(program.ExecutableName, query);
            var score = new[] { score1, score2, score3 }.Max();
            program.Score = score;
            return score;
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            Stopwatch.Debug("Preload programs", () =>
            {
                _programs = _cache.Programs;
            });
            Log.Info($"Preload {_programs.Count} programs from cache");
            Stopwatch.Debug("Program Index", IndexPrograms);
        }

        public static void IndexPrograms()
        {
            var sources = DefaultProgramSources();
            if (_settings.ProgramSources != null &&
                _settings.ProgramSources.Count(o => o.Enabled) > 0)
            {
                sources.AddRange(_settings.ProgramSources);
            }

            _programs = sources.AsParallel()
                                .SelectMany(s => s.LoadPrograms())
                                // filter duplicate program
                                .GroupBy(x => new { ExecutePath = x.Path, ExecuteName = x.ExecutableName })
                                .Select(g => g.First())
                                .ToList();

            _cache.Programs = _programs;
        }

        /// <summary>
        /// Load program sources that wox always provide
        /// </summary>
        private static List<ProgramSource> DefaultProgramSources()
        {
            var list = new List<ProgramSource>
            {
                new CommonStartMenuProgramSource
                {
                    BonusPoints = 0,
                    Enabled = _settings.EnableStartMenuSource,
                },
                new UserStartMenuProgramSource
                {
                    BonusPoints = 0,
                    Enabled = _settings.EnableStartMenuSource,
                },
                new AppPathsProgramSource
                {
                    BonusPoints = -10,
                    Enabled = _settings.EnableRegistrySource,
                }
            };
            return list;
        }

        private Program ScoreFilter(Program p)
        {
            p.Score += p.Source.BonusPoints;

            if (p.Title.Contains("启动") || p.Title.ToLower().Contains("start"))
                p.Score += 10;

            if (p.Title.Contains("帮助") || p.Title.ToLower().Contains("help") || p.Title.Contains("文档") || p.Title.ToLower().Contains("documentation"))
                p.Score -= 10;

            if (p.Title.Contains("卸载") || p.Title.ToLower().Contains("uninstall"))
                p.Score -= 20;
            return p;
        }

        #region ISettingProvider Members

        public Control CreateSettingPanel()
        {
            return new ProgramSetting(_context, _settings);
        }

        #endregion

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
            Program p = selectedResult.ContextData as Program;
            List<Result> contextMenus = new List<Result>
            {
                new Result
                {
                    Title = _context.API.GetTranslation("wox_plugin_program_run_as_administrator"),
                    Action = _ =>
                    {
                        var info = new ProcessStartInfo
                        {
                            FileName = p.Path,
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