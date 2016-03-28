using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;
using Wox.Plugin.Program.ProgramSources;
using Stopwatch = Wox.Infrastructure.Stopwatch;

namespace Wox.Plugin.Program
{
    public class Programs : ISettingProvider, IPlugin, IPluginI18n, IContextMenu
    {
        private static object lockObject = new object();
        private static List<Program> programs = new List<Program>();
        private static List<IProgramSource> sources = new List<IProgramSource>();
        private static Dictionary<string, Type> SourceTypes = new Dictionary<string, Type>
        {
            {"FileSystemProgramSource", typeof(FileSystemProgramSource)},
            {"CommonStartMenuProgramSource", typeof(CommonStartMenuProgramSource)},
            {"UserStartMenuProgramSource", typeof(UserStartMenuProgramSource)},
            {"AppPathsProgramSource", typeof(AppPathsProgramSource)}
        };
        private PluginInitContext _context;
        private static ProgramCacheStorage _cache = ProgramCacheStorage.Instance;
        private static ProgramStorage _settings = ProgramStorage.Instance;

        public List<Result> Query(Query query)
        {

            var fuzzyMather = FuzzyMatcher.Create(query.Search);
            var results = programs.Where(p => MatchProgram(p, fuzzyMather)).
                                   Select(ScoreFilter).
                                   OrderByDescending(p => p.Score)
                                   .Select(c => new Result
                                   {
                                       Title = c.Title,
                                       SubTitle = c.ExecutePath,
                                       IcoPath = c.IcoPath,
                                       Score = c.Score,
                                       ContextData = c,
                                       Action = e =>
                                       {
                                           var hide = StartProcess(new ProcessStartInfo(c.ExecutePath));
                                           return hide;
                                       }
                                   }).ToList();
            return results;
        }

        private bool MatchProgram(Program program, FuzzyMatcher matcher)
        {
            var scores = new List<string> { program.Title, program.PinyinTitle, program.AbbrTitle, program.ExecuteName };
            program.Score = scores.Select(s => matcher.Evaluate(s ?? string.Empty).Score).Max();
            return program.Score > 0;
        }

        public void Init(PluginInitContext context)
        {
            this._context = context;
            Stopwatch.Debug("Preload programs", () =>
            {
                programs = _cache.Programs;
            });
            Log.Info($"Preload {programs.Count} programs from cache");
            // happlebao todo fix this
            //Stopwatch.Debug("Program Index", IndexPrograms);
            IndexPrograms();
        }

        public static void IndexPrograms()
        {
            lock (lockObject)
            {
                List<ProgramSource> programSources = new List<ProgramSource>();
                programSources.AddRange(LoadDeaultProgramSources());
                if (_settings.ProgramSources != null &&
                    _settings.ProgramSources.Count(o => o.Enabled) > 0)
                {
                    programSources.AddRange(_settings.ProgramSources);
                }

                sources.Clear();
                foreach (var source in programSources.Where(o => o.Enabled))
                {
                    // happlebao todo: temp hack for program suffixes
                    source.Suffixes = _settings.ProgramSuffixes;

                    Type sourceClass;
                    if (SourceTypes.TryGetValue(source.Type, out sourceClass))
                    {
                        ConstructorInfo constructorInfo = sourceClass.GetConstructor(new[] { typeof(ProgramSource) });
                        if (constructorInfo != null)
                        {
                            IProgramSource programSource =
                                constructorInfo.Invoke(new object[] { source }) as IProgramSource;
                            sources.Add(programSource);
                        }
                    }
                }

                var tempPrograms = new List<Program>();
                foreach (var source in sources)
                {
                    var list = source.LoadPrograms();
                    list.ForEach(o =>
                    {
                        o.Source = source;
                    });
                    tempPrograms.AddRange(list);
                }

                // filter duplicate program
                programs = tempPrograms.GroupBy(x => new { x.ExecutePath, x.ExecuteName })
                    .Select(g => g.First()).ToList();

                _cache.Programs = programs;
                _cache.Save();
            }
        }

        /// <summary>
        /// Load program sources that wox always provide
        /// </summary>
        private static List<ProgramSource> LoadDeaultProgramSources()
        {
            var list = new List<ProgramSource>();
            list.Add(new ProgramSource
            {
                BonusPoints = 0,
                Enabled = _settings.EnableStartMenuSource,
                Type = "CommonStartMenuProgramSource"
            });
            list.Add(new ProgramSource
            {
                BonusPoints = 0,
                Enabled = _settings.EnableStartMenuSource,
                Type = "UserStartMenuProgramSource"
            });
            list.Add(new ProgramSource
            {
                BonusPoints = -10,
                Enabled = _settings.EnableRegistrySource,
                Type = "AppPathsProgramSource"
            });
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
                        var hide = StartProcess(new ProcessStartInfo
                        {
                            FileName = p.ExecutePath,
                            Verb = "runas"
                        });
                        return hide;
                    },
                    IcoPath = "Images/cmd.png"
                },
                new Result
                {
                    Title = _context.API.GetTranslation("wox_plugin_program_open_containing_folder"),
                    Action = _ =>
                    {
                        //get parent folder
                        var folderPath = Directory.GetParent(p.ExecutePath).FullName;
                        //open the folder
                        var hide = StartProcess(new ProcessStartInfo(folderPath));
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