// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using LinqToOneNote;
using LinqToOneNote.Abstractions;
using Microsoft.PowerToys.Run.Plugin.OneNote.Properties;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;
using OneNoteApplication = LinqToOneNote.OneNote;

namespace Microsoft.PowerToys.Run.Plugin.OneNote.Components
{
    public class ResultCreator
    {
        private readonly PluginInitContext _context;
        private readonly OneNoteSettings _settings;
        private readonly IconProvider _iconProvider;

        private const string PathSeparator = " > ";

        private static readonly CompositeFormat ViewNotebookExplorerDescription = CompositeFormat.Parse(Resources.ViewNotebookExplorerDescription);
        private static readonly CompositeFormat ViewRecentPagesDescription = CompositeFormat.Parse(Resources.ViewRecentPagesDescription);
        private static readonly CompositeFormat CreatePage = CompositeFormat.Parse(Resources.CreatePage);
        private static readonly CompositeFormat CreateSection = CompositeFormat.Parse(Resources.CreateSection);
        private static readonly CompositeFormat CreateSectionGroup = CompositeFormat.Parse(Resources.CreateSectionGroup);
        private static readonly CompositeFormat CreateNotebook = CompositeFormat.Parse(Resources.CreateNotebook);
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

        private static string GetNicePath(IOneNoteItem item, string separator = PathSeparator) => item.GetRelativePath(false, separator);

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

        private static string GetQueryTextDisplay(IOneNoteItem? parent)
        {
            return parent is null
                ? $"{Keywords.NotebookExplorer}"
                : $"{Keywords.NotebookExplorer}{GetNicePath(parent, Keywords.NotebookExplorerSeparator)}{Keywords.NotebookExplorerSeparator}";
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
                        OneNoteApplication.CreateQuickNote(OpenMode.ExistingOrNewWindow);
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
                        IReadOnlyList<Notebook> notebooks = OneNoteApplication.GetFullHierarchy().Notebooks;
                        foreach (var notebook in notebooks)
                        {
                            notebook.Sync();
                        }

                        notebooks.GetAllPages()
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
                case Notebook notebook:
                    subTitle = string.Empty;
                    break;
                case Section section:
                    if (section.Encrypted)
                    {
                        // potentially replace with glyphs when/if supported
                        title += string.Format(CultureInfo.CurrentCulture, " [{0}]", section.Locked ? Resources.Locked : Resources.Unlocked);
                    }

                    break;
                case Page page:
                    queryTextDisplay = !actionIsAutoComplete ? page.Name : queryTextDisplay[..^1];

                    actionIsAutoComplete = false;

                    subTitle = subTitle[..^(page.Name.Length + PathSeparator.Length)];
                    break;
            }

            var toolTip = string.Format(CultureInfo.CurrentCulture, LastModified, item.LastModified);
            if (item is not Notebook)
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

        internal Result CreatePageResult(Page page, string? query)
        {
            return CreateOneNoteItemResult(page, false, string.IsNullOrWhiteSpace(query) ? null : StringMatcher.FuzzySearch(query, page.Name).MatchData);
        }

        internal Result CreateRecentPageResult(Page page)
        {
            var result = CreateOneNoteItemResult(page, false, null);
            result.IcoPath = _iconProvider.Recent;
            result.SubTitle = $"{page.LastModified.ToString(CultureInfo.CurrentCulture)} | {result.SubTitle}";
            return result;
        }

        private Result CreateNewOneNoteItemResult(string newItemName, IOneNoteItem? parent, CompositeFormat titleFormat, IReadOnlyList<char> invalidCharacters, CompositeFormat subTitleFormat, string iconPath, Action createItemAction)
        {
            newItemName = newItemName.Trim();

            bool validTitle = !string.IsNullOrWhiteSpace(newItemName) && !invalidCharacters.Any(newItemName.Contains);

            string subTitle = parent == null
                ? $"{OneNoteApplication.GetDefaultNotebookLocation()}{System.IO.Path.DirectorySeparatorChar}{newItemName}"
                : $"{GetNicePath(parent)}{PathSeparator}{newItemName}";

            return new Result
            {
                Title = string.Format(CultureInfo.CurrentCulture, titleFormat, newItemName),
                SubTitle = validTitle
                    ? string.Format(CultureInfo.CurrentCulture, Path, subTitle)
                    : string.Format(CultureInfo.CurrentCulture, subTitleFormat, string.Join(' ', invalidCharacters)),
                QueryTextDisplay = $"{GetQueryTextDisplay(parent)}{newItemName}",
                IcoPath = iconPath,
                Action = ResultAction(() =>
                {
                    if (!validTitle)
                    {
                        return false;
                    }

                    createItemAction();

                    OneNoteItemExtensions.ShowOneNote();
                    _context.API.ChangeQuery($"{GetQueryTextDisplay(parent)}{newItemName}", true);
                    return true;
                }),
            };
        }

        internal Result CreateNewPageResult(string newPageName, Section section)
        {
            return CreateNewOneNoteItemResult(newPageName, section, CreatePage, [], SectionNamesCannotContain, _iconProvider.NewPage, () => OneNoteApplication.CreatePage(section, newPageName, OpenMode.ExistingOrNewWindow));
        }

        internal Result CreateNewSectionResult(string newSectionName, INotebookOrSectionGroup parent)
        {
            return CreateNewOneNoteItemResult(newSectionName, parent, CreateSection, Section.InvalidCharacters, SectionNamesCannotContain, _iconProvider.NewSection, () => OneNoteApplication.CreateSection(parent, newSectionName, OpenMode.ExistingOrNewWindow));
        }

        internal Result CreateNewSectionGroupResult(string newSectionGroupName, INotebookOrSectionGroup parent)
        {
            return CreateNewOneNoteItemResult(newSectionGroupName, parent, CreateSectionGroup, SectionGroup.InvalidCharacters, SectionGroupNamesCannotContain, _iconProvider.NewSectionGroup, () => OneNoteApplication.CreateSectionGroup(parent, newSectionGroupName, OpenMode.ExistingOrNewWindow));
        }

        internal Result CreateNewNotebookResult(string newNotebookName)
        {
            return CreateNewOneNoteItemResult(newNotebookName, null, CreateNotebook, Notebook.InvalidCharacters, NotebookNamesCannotContain, _iconProvider.NewNotebook, () => OneNoteApplication.CreateNotebook(newNotebookName, OpenMode.ExistingOrNewWindow));
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

                if (item is not Page)
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
                case INotebookOrSectionGroup:
                    // Can create section/section group
                    results.Add(NoItemsInCollectionResult(Resources.CreateSection, _iconProvider.NewSection));
                    results.Add(NoItemsInCollectionResult(Resources.CreateSectionGroup, _iconProvider.NewSectionGroup));
                    break;
                case Section section when !section.IsDeletedPages && !section.Locked:
                    // Can create page
                    results.Add(NoItemsInCollectionResult(Resources.CreatePage, _iconProvider.NewPage));
                    break;
                default:
                    break;
            }

            return results;

            Result NoItemsInCollectionResult(string title, string iconPath)
            {
                return new Result
                {
                    Title = string.Format(CultureInfo.CurrentCulture, title, string.Empty),
                    QueryTextDisplay = $"{GetQueryTextDisplay(parent)}",
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
