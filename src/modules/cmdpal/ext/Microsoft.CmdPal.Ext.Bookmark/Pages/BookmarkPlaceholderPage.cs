// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Bookmarks.Pages;

internal sealed partial class BookmarkPlaceholderPage : ParametersPage, IDisposable
{
    private readonly BookmarkData _bookmarkData;
    private readonly IBookmarkResolver _resolver;
    private readonly Classification _bookmarkClassification;
    private readonly IParameterRun[] _parameters;
    private readonly Dictionary<string, StringParameterRun> _placeholderRuns;
    private readonly ListItem _commandItem;
    private readonly SupersedingAsyncValueGate<IIconInfo?> _iconReloadGate;

    public BookmarkPlaceholderPage(BookmarkData bookmarkData, IBookmarkIconLocator iconLocator, IBookmarkResolver resolver, IPlaceholderParser placeholderParser)
    {
        ArgumentNullException.ThrowIfNull(bookmarkData);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(placeholderParser);

        _bookmarkData = bookmarkData;
        _resolver = resolver;

        // Cache the original bookmark's classification — it doesn't depend on
        // placeholder values, and we need it on every keystroke to know how to
        // encode the preview/launched URL.
        _bookmarkClassification = resolver.ClassifyOrUnknown(bookmarkData.Bookmark);

        Name = Resources.bookmarks_command_name_open;
        Id = CommandIds.GetLaunchBookmarkItemId(bookmarkData.Id);

        placeholderParser.ParsePlaceholders(bookmarkData.Bookmark, out _, out var placeholders);
        (_parameters, _placeholderRuns) = BuildParameterRuns(bookmarkData.Bookmark, placeholders);

        var submitCommand = new LaunchPlaceholderCommand(this);
        _commandItem = new ListItem(submitCommand);

        foreach (var run in _placeholderRuns.Values)
        {
            run.PropChanged += OnPlaceholderChanged;
        }

        UpdateSubtitle();

        _iconReloadGate = new(
            async ct => await iconLocator.GetIconForPath(_bookmarkClassification, ct),
            icon =>
            {
                Icon = icon as IconInfo ?? Icons.PinIcon;
            });
        RequestIconReloadAsync();
    }

    public override IParameterRun[] Parameters => _parameters;

    public override IListItem Command => _commandItem;

    private static (IParameterRun[] Runs, Dictionary<string, StringParameterRun> RunsByName) BuildParameterRuns(string bookmark, List<PlaceholderInfo> placeholders)
    {
        var runs = new List<IParameterRun>();
        var byName = new Dictionary<string, StringParameterRun>(StringComparer.OrdinalIgnoreCase);
        var cursor = 0;

        // PlaceholderParser emits placeholders in source order, but be defensive
        // in case that ever changes — slicing relies on monotonic indices.
        placeholders.Sort((a, b) => a.Index.CompareTo(b.Index));

        foreach (var placeholder in placeholders)
        {
            if (placeholder.Index > cursor)
            {
                runs.Add(new LabelRun(bookmark.Substring(cursor, placeholder.Index - cursor)));
            }

            if (!byName.TryGetValue(placeholder.Name, out var run))
            {
                run = new StringParameterRun
                {
                    PlaceholderText = placeholder.Name,
                };
                byName[placeholder.Name] = run;
            }

            runs.Add(run);

            // Advance past "{Name}" — name length plus the two braces.
            cursor = placeholder.Index + placeholder.Name.Length + 2;
        }

        if (cursor < bookmark.Length)
        {
            runs.Add(new LabelRun(bookmark.Substring(cursor)));
        }

        return (runs.ToArray(), byName);
    }

    private CommandResult LaunchWithCurrentValues()
    {
        var target = BuildEvaluatedBookmark();

        // Re-classify the final target — adding placeholder values may change
        // what kind of command this is (e.g. a path that needs different launch).
        var classification = _resolver.ClassifyOrUnknown(target);
        var success = CommandLauncher.Launch(classification);
        return success ? CommandResult.Dismiss() : CommandResult.KeepOpen();
    }

    private string BuildEvaluatedBookmark()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, run) in _placeholderRuns)
        {
            values[name] = run.Text ?? string.Empty;
        }

        return ReplacePlaceholders(_bookmarkData.Bookmark, values, _bookmarkClassification);
    }

    private bool AllPlaceholdersHaveValues()
    {
        foreach (var run in _placeholderRuns.Values)
        {
            if (string.IsNullOrEmpty(run.Text))
            {
                return false;
            }
        }

        return true;
    }

    private void OnPlaceholderChanged(object sender, IPropChangedEventArgs args)
    {
        if (args.PropertyName == nameof(StringParameterRun.Text))
        {
            UpdateSubtitle();
        }
    }

    private void UpdateSubtitle()
    {
        _commandItem.Subtitle = AllPlaceholdersHaveValues() ? BuildEvaluatedBookmark() : string.Empty;
    }

    private static string ReplacePlaceholders(string input, Dictionary<string, string> placeholders, Classification classification)
    {
        var result = input;
        foreach (var (key, value) in placeholders)
        {
            var placeholderString = $"{{{key}}}";
            var encodedValue = value;
            if (classification.Kind is CommandKind.Protocol or CommandKind.WebUrl)
            {
                encodedValue = Uri.EscapeDataString(value);
            }

            result = result.Replace(placeholderString, encodedValue, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private void RequestIconReloadAsync()
    {
        Icon = Icons.Reloading;
        OnPropertyChanged(nameof(Icon));
        _ = _iconReloadGate.ExecuteAsync();
    }

    public void Dispose()
    {
        foreach (var run in _placeholderRuns.Values)
        {
            run.PropChanged -= OnPlaceholderChanged;
        }

        _iconReloadGate.Dispose();
    }

    private sealed partial class LaunchPlaceholderCommand : InvokableCommand
    {
        private readonly BookmarkPlaceholderPage _page;

        public LaunchPlaceholderCommand(BookmarkPlaceholderPage page)
        {
            _page = page;
            Name = Resources.bookmarks_form_open;
            Icon = Icons.PinIcon;
        }

        public override ICommandResult Invoke() => _page.LaunchWithCurrentValues();
    }
}
