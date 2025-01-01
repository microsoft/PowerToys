// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using Humanizer;
using Microsoft.PowerToys.Run.Plugin.OneNote.Properties;
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

        private static readonly CompositeFormat ViewNotebookExplorerDescription = CompositeFormat.Parse(Resources.ViewNotebookExplorerDescription);
        private static readonly CompositeFormat ViewRecentPagesDescription = CompositeFormat.Parse(Resources.ViewRecentPagesDescription);
        private static readonly CompositeFormat CreatePage = CompositeFormat.Parse(Resources.CreatePage);
        private static readonly CompositeFormat CreateSection = CompositeFormat.Parse(Resources.CreateSection);
        private static readonly CompositeFormat CreateSectionGroup = CompositeFormat.Parse(Resources.CreateSectionGroup);
        private static readonly CompositeFormat CreateNotebook = CompositeFormat.Parse(Resources.CreateNotebook);
        private static readonly CompositeFormat Location = CompositeFormat.Parse(Resources.Location);
        private static readonly CompositeFormat Path = CompositeFormat.Parse(Resources.Path);
        private static readonly CompositeFormat LastModified = CompositeFormat.Parse(Resources.LastModified);
        private static readonly CompositeFormat SectionNamesCannotContain = CompositeFormat.Parse(Resources.SectionNamesCannotContain);
        private static readonly CompositeFormat SectionGroupNamesCannotContain = CompositeFormat.Parse(Resources.SectionGroupNamesCannotContain);
        private static readonly CompositeFormat NotebookNamesCannotContain = CompositeFormat.Parse(Resources.NotebookNamesCannotContain);

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

            if (!item.IsUnread || !_settings.ShowUnreadItems)
            {
                return title;
            }

            const string unread = "\u2022  ";
            title = title.Insert(0, unread);

            if (highlightData == null)
            {
                return title;
            }

            for (int i = 0; i < highlightData.Count; i++)
            {
                highlightData[i] += unread.Length;
            }

            return title;
        }

        private string GetQueryTextDisplay(IOneNoteItem item) => $"{Keywords.NotebookExplorer}{GetNicePath(item, Keywords.NotebookExplorerSeparator)}{Keywords.NotebookExplorerSeparator}";

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
                    Title = Resources.SearchOneNotePages,
                    IcoPath = _iconProvider.Search,
                    Score = 5000,
                },
                new Result
                {
                    Title = Resources.ViewNotebookExplorer,
                    SubTitle = string.Format(CultureInfo.CurrentCulture, ViewNotebookExplorerDescription, Keywords.NotebookExplorer),
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
                    Title = Resources.ViewRecentPages,
                    SubTitle = string.Format(CultureInfo.CurrentCulture, ViewRecentPagesDescription, Keywords.RecentPages),
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
                    Title = Resources.NewQuickNote,
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
                    Title = Resources.OpenSyncNotebooks,
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
            string subTitle = GetNicePath(item);
            string queryTextDisplay = GetQueryTextDisplay(item);

            // TODO: Potential improvement would be to show the children of the OneNote item in its tooltip.
            // E.g. for a notebook, it would display the number of section groups, sections and pages.
            // Would require even more localisation.
            // An example: https://github.com/Odotocodot/Flow.Launcher.Plugin.OneNote/blob/5f56aa81a19641197d4ea4a97dc22cf1aa21f5e6/Flow.Launcher.Plugin.OneNote/ResultCreator.cs#L145
            switch (item)
            {
                case OneNoteNotebook notebook:
                    subTitle = string.Empty;
                    break;
                case OneNoteSection section:
                    if (section.Encrypted)
                    {
                        // potentially replace with glyphs when/if supported
                        title += string.Format(CultureInfo.CurrentCulture, " [{0}]", section.Locked ? Resources.Locked : Resources.Unlocked);
                    }

                    break;
                case OneNotePage page:
                    queryTextDisplay = !actionIsAutoComplete ? string.Empty : queryTextDisplay[..^1];

                    actionIsAutoComplete = false;

                    subTitle = subTitle[..^(page.Name.Length + PathSeparator.Length)];
                    break;
            }

            var toolTip = string.Format(CultureInfo.CurrentCulture, LastModified, item.LastModified);
            if (item is not OneNoteNotebook)
            {
                toolTip = toolTip.Insert(0, string.Format(CultureInfo.CurrentCulture, Path, subTitle) + "\n");
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
            result.IcoPath = _iconProvider.Recent;
            result.SubTitle = $"{page.LastModified.Humanize(culture: CultureInfo.CurrentCulture)} | {result.SubTitle}";
            return result;
        }

        internal Result CreateNewPageResult(string newPageName, OneNoteSection section)
        {
            newPageName = newPageName.Trim();
            return new Result
            {
                Title = string.Format(CultureInfo.CurrentCulture, CreatePage, newPageName),
                SubTitle = string.Format(CultureInfo.CurrentCulture, Path, GetNicePath(section) + PathSeparator + newPageName),
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
                Title = string.Format(CultureInfo.CurrentCulture, CreateSection, newSectionName),
                SubTitle = validTitle
                        ? string.Format(CultureInfo.CurrentCulture, Path, GetNicePath(parent) + PathSeparator + newSectionName)
                        : string.Format(CultureInfo.CurrentCulture, SectionNamesCannotContain, string.Join(' ', OneNoteApplication.InvalidSectionChars)),
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
                Title = string.Format(CultureInfo.CurrentCulture, CreateSectionGroup, newSectionGroupName),
                SubTitle = validTitle
                    ? string.Format(CultureInfo.CurrentCulture, Path, GetNicePath(parent) + PathSeparator + newSectionGroupName)
                    : string.Format(CultureInfo.CurrentCulture, SectionGroupNamesCannotContain, string.Join(' ', OneNoteApplication.InvalidSectionGroupChars)),
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
                Title = string.Format(CultureInfo.CurrentCulture, CreateNotebook, newNotebookName),
                SubTitle = validTitle
                    ? string.Format(CultureInfo.CurrentCulture, Location, OneNoteApplication.GetDefaultNotebookLocation())
                    : string.Format(CultureInfo.CurrentCulture, NotebookNamesCannotContain, string.Join(' ', OneNoteApplication.InvalidNotebookChars)),
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
                    Title = Resources.OpenAndSync,
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
                        Title = Resources.OpenInNotebookExplorer,
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
                    Title = Resources.VisitMicrosoftStore,
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
                    results.Add(NoItemsInCollectionResult(Resources.CreateSection, _iconProvider.NewSection));
                    results.Add(NoItemsInCollectionResult(Resources.CreateSectionGroup, _iconProvider.NewSectionGroup));
                    break;
                case OneNoteSection section:
                    // Can create page
                    if (!section.Locked)
                    {
                        results.Add(NoItemsInCollectionResult(Resources.CreatePage, _iconProvider.NewPage));
                    }

                    break;
                default:
                    break;
            }

            return results;

            static Result NoItemsInCollectionResult(string title, string iconPath)
            {
                return new Result
                {
                    Title = title,
                    SubTitle = Resources.NoItemsFoundTypeValidName,
                    IcoPath = iconPath,
                };
            }
        }

        internal List<Result> NoMatchesFound(bool show)
        {
            return show
                ? SingleResult(
                    Resources.NoMatchesFound,
                    Resources.NoMatchesFoundDescription,
                    _iconProvider.Search)
                : [];
        }

        internal List<Result> InvalidQuery(bool show)
        {
            return show
                ? SingleResult(
                    Resources.InvalidQuery,
                    Resources.InvalidQueryDescription,
                    _iconProvider.Warning)
                : [];
        }

        internal List<Result> OneNoteNotInstalled()
        {
            var results = SingleResult(
                Resources.OneNoteNotInstalled,
                Resources.OneNoteNotInstalledDescription,
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
