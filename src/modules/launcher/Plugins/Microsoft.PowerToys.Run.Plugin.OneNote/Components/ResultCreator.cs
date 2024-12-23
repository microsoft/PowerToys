// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using Odotocodot.OneNote.Linq;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components
{
    public class ResultCreator
    {
        private readonly PluginInitContext _context;
        private readonly OneNoteSettings _settings;
        private readonly IconProvider _iconProvider;

        private const string PathSeparator = " > ";
        private static readonly string _oldSeparator = OneNoteApplication.RelativePathSeparator.ToString();

        internal ResultCreator(PluginInitContext context, OneNoteSettings settings, IconProvider iconProvider)
        {
            _settings = settings;
            _context = context;
            _iconProvider = iconProvider;
        }

        private static string GetNicePath(IOneNoteItem item, string separator = PathSeparator) => item.RelativePath.Replace(_oldSeparator, separator);

        private string GetTitle(IOneNoteItem item, List<int>? highlightData)
        {
            string title = item.Name;
            if (item.IsUnread && _settings.ShowUnreadItems)
            {
                string unread = "\u2022  ";
                title = title.Insert(0, unread);

                if (highlightData != null)
                {
                    for (int i = 0; i < highlightData.Count; i++)
                    {
                        highlightData[i] += unread.Length;
                    }
                }
            }

            return title;
        }

        private string GetQueryTextDisplay(IOneNoteItem item) => $"{Keywords.NotebookExplorer}{GetNicePath(item, Keywords.NotebookExplorerSeparator)}{Keywords.NotebookExplorerSeparator}";

        private static string GetLastEdited(TimeSpan diff)
        {
            string lastEdited = "Last edited ";
            if (PluralCheck(diff.TotalDays, "day", ref lastEdited)
             || PluralCheck(diff.TotalHours, "hour", ref lastEdited)
             || PluralCheck(diff.TotalMinutes, "min", ref lastEdited)
             || PluralCheck(diff.TotalSeconds, "sec", ref lastEdited))
            {
                return lastEdited;
            }
            else
            {
                return lastEdited += "Now.";
            }

            static bool PluralCheck(double totalTime, string timeType, ref string lastEdited)
            {
                var roundedTime = (int)Math.Round(totalTime);
                if (roundedTime > 0)
                {
                    string plural = roundedTime == 1 ? string.Empty : "s";
                    lastEdited += $"{roundedTime} {timeType}{plural} ago.";
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        internal List<Result> EmptyQuery(Query query)
        {
            if (_context.CurrentPluginMetadata.IsGlobal && !query.RawUserQuery.StartsWith(query.ActionKeyword, StringComparison.Ordinal))
            {
                return [];
            }

            return new List<Result>
            {
                new Result
                {
                    Title = "Search OneNote pages",
                    QueryTextDisplay = string.Empty,
                    IcoPath = _iconProvider.Search,
                    Score = 5000,
                },
                new Result
                {
                    Title = "View notebook explorer",
                    SubTitle = $"Type \"{Keywords.NotebookExplorer}\" or select this option to search by notebook structure ",
                    QueryTextDisplay = Keywords.NotebookExplorer,
                    IcoPath = _iconProvider.NotebookExplorer,
                    Score = 2000,
                    Action = ResultAction(() =>
                    {
                        _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} {Keywords.NotebookExplorer}", true);
                        return false;
                    }),
                },
                new Result
                {
                    Title = "See recent pages",
                    SubTitle = $"Type \"{Keywords.RecentPages}\" or select this option to see recently modified pages",
                    QueryTextDisplay = Keywords.RecentPages,
                    IcoPath = _iconProvider.Recent,
                    Score = -1000,
                    Action = ResultAction(() =>
                    {
                        _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} {Keywords.RecentPages}", true);
                        return false;
                    }),
                },
                new Result
                {
                    Title = "New quick note",
                    IcoPath = _iconProvider.QuickNote,
                    Score = -4000,
                    Action = ResultAction(() =>
                    {
                        OneNoteApplication.CreateQuickNote(true);
                        return true;
                    }),
                },
                new Result
                {
                    Title = "Open and sync notebooks",
                    IcoPath = _iconProvider.Sync,
                    Score = int.MinValue,
                    Action = ResultAction(() =>
                    {
                        foreach (var notebook in OneNoteApplication.GetNotebooks())
                        {
                            notebook.Sync();
                        }

                        OneNoteApplication.GetNotebooks()
                                          .GetPages()
                                          .Where(i => !i.IsInRecycleBin)
                                          .OrderByDescending(pg => pg.LastModified)
                                          .First()
                                          .OpenItemInOneNote();
                        return true;
                    }),
                },
            };
        }

        internal Result CreateOneNoteItemResult(IOneNoteItem item, bool actionIsAutoComplete, List<int>? highlightData = null, int score = 0)
        {
            string title = GetTitle(item, highlightData);
            string toolTip = string.Empty;
            string subTitle = GetNicePath(item);
            string queryTextDisplay = GetQueryTextDisplay(item);

            switch (item)
            {
                case OneNoteNotebook notebook:
                    toolTip =
                        $"Last Modified:\t{notebook.LastModified:F}\n" +
                        $"Sections:\t\t{notebook.Sections.Count()}\n" +
                        $"Sections Groups:\t{notebook.SectionGroups.Count()}";

                    subTitle = string.Empty;
                    break;
                case OneNoteSectionGroup sectionGroup:
                    toolTip =
                        $"Path:\t\t{subTitle}\n" +
                        $"Last Modified:\t{sectionGroup.LastModified:F}\n" +
                        $"Sections:\t\t{sectionGroup.Sections.Count()}\n" +
                        $"Sections Groups:\t{sectionGroup.SectionGroups.Count()}";

                    break;
                case OneNoteSection section:
                    if (section.Encrypted)
                    {
                        // potentially replace with glyphs if supported
                        title += $" [Encrypted] {(section.Locked ? "[Locked]" : "[Unlocked]")}";
                    }

                    toolTip =
                        $"Path:\t\t{subTitle}\n" +
                        $"Last Modified:\t{section.LastModified}\n" +
                        $"Pages:\t\t{section.Pages.Count()}";

                    break;
                case OneNotePage page:
                    queryTextDisplay = !actionIsAutoComplete ? string.Empty : queryTextDisplay[..^1];

                    actionIsAutoComplete = false;

                    subTitle = subTitle[..^(page.Name.Length + PathSeparator.Length)];
                    toolTip =
                        $"Path:\t\t {subTitle} \n" +
                        $"Created:\t\t{page.Created:F}\n" +
                        $"Last Modified:\t{page.LastModified:F}";
                    break;
            }

            return new Result
            {
                Title = title,
                ToolTipData = new ToolTipData(item.Name, toolTip),
                TitleHighlightData = highlightData,
                QueryTextDisplay = queryTextDisplay,
                SubTitle = subTitle,
                Score = score,
                Icon = () => _iconProvider.GetIcon(item),
                ContextData = item,
                Action = ResultAction(() =>
                {
                    if (actionIsAutoComplete)
                    {
                        _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} {queryTextDisplay}", true);
                        return false;
                    }

                    item.Sync();
                    item.OpenItemInOneNote();
                    return true;
                }),
            };
        }

        internal Result CreatePageResult(OneNotePage page, string? query)
        {
            return CreateOneNoteItemResult(page, false, string.IsNullOrWhiteSpace(query) ? null : StringMatcher.FuzzySearch(query, page.Name).MatchData);
        }

        internal Result CreateRecentPageResult(OneNotePage page)
        {
            var result = CreateOneNoteItemResult(page, false, null);
            result.SubTitle = $"{GetLastEdited(DateTime.Now - page.LastModified)}\t{result.SubTitle}";
            result.IcoPath = _iconProvider.Page;
            return result;
        }

        internal Result CreateNewPageResult(string newPageName, OneNoteSection section)
        {
            newPageName = newPageName.Trim();
            return new Result
            {
                Title = $"Create page: \"{newPageName}\"",
                SubTitle = $"Path: {GetNicePath(section)}{PathSeparator}{newPageName}",
                QueryTextDisplay = $"{GetQueryTextDisplay}{newPageName}",
                IcoPath = _iconProvider.NewPage,
                Action = ResultAction(() =>
                {
                    OneNoteApplication.CreatePage(section, newPageName, true);
                    return true;
                }),
            };
        }

        internal Result CreateNewSectionResult(string newSectionName, IOneNoteItem parent)
        {
            newSectionName = newSectionName.Trim();
            bool validTitle = OneNoteApplication.IsSectionNameValid(newSectionName);

            return new Result
            {
                Title = $"Create section: \"{newSectionName}\"",
                SubTitle = validTitle
                        ? $"Path: {GetNicePath(parent)}{PathSeparator}{newSectionName}"
                        : $"Section names cannot contain: {string.Join(' ', OneNoteApplication.InvalidSectionChars)}",
                QueryTextDisplay = $"{GetQueryTextDisplay}{newSectionName}",
                IcoPath = _iconProvider.NewSection,
                Action = ResultAction(() =>
                {
                    if (!validTitle)
                    {
                        return false;
                    }

                    switch (parent)
                    {
                        case OneNoteNotebook notebook:
                            OneNoteApplication.CreateSection(notebook, newSectionName, true);
                            break;
                        case OneNoteSectionGroup sectionGroup:
                            OneNoteApplication.CreateSection(sectionGroup, newSectionName, true);
                            break;
                        default:
                            break;
                    }

                    _context.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeyword, true);
                    return true;
                }),
            };
        }

        internal Result CreateNewSectionGroupResult(string newSectionGroupName, IOneNoteItem parent)
        {
            newSectionGroupName = newSectionGroupName.Trim();
            bool validTitle = OneNoteApplication.IsSectionGroupNameValid(newSectionGroupName);

            return new Result
            {
                Title = $"Create section group: \"{newSectionGroupName}\"",
                SubTitle = validTitle
                    ? $"Path: {GetNicePath(parent)}{PathSeparator}{newSectionGroupName}"
                    : $"Section group names cannot contain: {string.Join(' ', OneNoteApplication.InvalidSectionGroupChars)}",
                QueryTextDisplay = $"{GetQueryTextDisplay}{newSectionGroupName}",
                IcoPath = _iconProvider.NewSectionGroup,
                Action = ResultAction(() =>
                {
                    if (!validTitle)
                    {
                        return false;
                    }

                    switch (parent)
                    {
                        case OneNoteNotebook notebook:
                            OneNoteApplication.CreateSectionGroup(notebook, newSectionGroupName, true);
                            break;
                        case OneNoteSectionGroup sectionGroup:
                            OneNoteApplication.CreateSectionGroup(sectionGroup, newSectionGroupName, true);
                            break;
                        default:
                            break;
                    }

                    _context.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeyword, true);
                    return true;
                }),
            };
        }

        internal Result CreateNewNotebookResult(string newNotebookName)
        {
            newNotebookName = newNotebookName.Trim();
            bool validTitle = OneNoteApplication.IsNotebookNameValid(newNotebookName);

            return new Result
            {
                Title = $"Create notebook: \"{newNotebookName}\"",
                SubTitle = validTitle
                    ? $"Location: {OneNoteApplication.GetDefaultNotebookLocation()}"
                    : $"Notebook names cannot contain: {string.Join(' ', OneNoteApplication.InvalidNotebookChars)}",
                QueryTextDisplay = $"{GetQueryTextDisplay}{newNotebookName}",
                IcoPath = _iconProvider.NewNotebook,
                Action = ResultAction(() =>
                {
                    if (!validTitle)
                    {
                        return false;
                    }

                    OneNoteApplication.CreateNotebook(newNotebookName, true);
                    _context.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeyword, true);
                    return true;
                }),
            };
        }

        internal List<ContextMenuResult> LoadContextMenu(Result selectedResult)
        {
            var results = new List<ContextMenuResult>();
            if (selectedResult.ContextData is IOneNoteItem item)
            {
                results.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Open and sync",
                    Glyph = "\xE8A7",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Shift,
                    Action = ResultAction(() =>
                    {
                        item.Sync();
                        item.OpenItemInOneNote();
                        return true;
                    }),
                });

                if (item is not OneNotePage)
                {
                    results.Add(new ContextMenuResult
                    {
                        PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                        Title = "Open in notebook explorer",
                        Glyph = "\xEC50",
                        FontFamily = "Segoe MDL2 Assets",
                        AcceleratorKey = Key.Enter,
                        AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                        Action = ResultAction(() =>
                        {
                            _context.API.ChangeQuery(selectedResult.QueryTextDisplay, true);
                            return false;
                        }),
                    });
                }
            }

            if (selectedResult.ContextData is string url)
            {
                results.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = "Visit the Microsoft Store",
                    Glyph = "\xE8A7",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Shift,
                    Action = ResultAction(() =>
                    {
                        try
                        {
                            Process.Start(url);
                        }
                        catch (Exception ex)
                        {
                            Log.Exception(ex.Message, ex, GetType());
                        }

                        return true;
                    }),
                });
            }

            return results;
        }

        internal List<Result> NoItemsInCollection(IOneNoteItem? parent, List<Result> results)
        {
            // parent can be null if the collection only contains notebooks.
            switch (parent)
            {
                case OneNoteNotebook:
                case OneNoteSectionGroup:
                    // Can create section/section group
                    results.Add(NoItemsInCollectionResult("section", _iconProvider.NewSection, "(unencrypted) section"));
                    results.Add(NoItemsInCollectionResult("section group", _iconProvider.NewSectionGroup));
                    break;
                case OneNoteSection section:
                    // Can create page
                    if (!section.Locked)
                    {
                        results.Add(NoItemsInCollectionResult("page", _iconProvider.NewPage));
                    }

                    break;
                default:
                    break;
            }

            return results;

            static Result NoItemsInCollectionResult(string title, string iconPath, string? subTitle = null)
            {
                return new Result
                {
                    Title = $"Create {title}: \"\"",
                    SubTitle = $"No {subTitle ?? title}s found. Type a valid title to create one",
                    IcoPath = iconPath,
                };
            }
        }

        // TODO Localize
        internal List<Result> NoMatchesFound(bool show)
        {
            return show
                ? SingleResult(
                    "No matches found",
                    "Try searching something else, or syncing your notebooks.",
                    _iconProvider.Search)
                : [];
        }

        internal List<Result> InvalidQuery(bool show)
        {
            return show
                ? SingleResult(
                    "Invalid query",
                    "The first character of the search must be a letter or a digit",
                    _iconProvider.Warning)
                : [];
        }

        internal List<Result> OneNoteNotInstalled()
        {
            var results = SingleResult(
                "OneNote is not installed",
                "Please install OneNote to use this plugin",
                _iconProvider.Warning);

            results[0].ContextData = "https://apps.microsoft.com/store/detail/XPFFZHVGQWWLHB?ocid=pdpshare";
            return results;
        }

        internal static List<Result> SingleResult(string title, string? subTitle, string iconPath)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = title,
                    SubTitle = subTitle,
                    IcoPath = iconPath,
                },
            };
        }

        internal static Func<ActionContext, bool> ResultAction(Func<bool> func)
        {
            return _ =>
            {
                bool result = func();

                // Closing the Run window, so can release the COM Object
                if (result)
                {
                    Task.Run(OneNoteApplication.ReleaseComObject);
                }

                return result;
            };
        }
    }
}
