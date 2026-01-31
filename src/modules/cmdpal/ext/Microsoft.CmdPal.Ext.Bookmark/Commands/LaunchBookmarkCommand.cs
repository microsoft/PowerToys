// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Bookmarks.Commands;

internal sealed partial class LaunchBookmarkCommand : BaseObservable, IInvokableCommand, IDisposable
{
    private static readonly CompositeFormat FailedToOpenMessageFormat = CompositeFormat.Parse(Resources.bookmark_toast_failed_open_text!);

    private readonly BookmarkData _bookmarkData;
    private readonly Dictionary<string, string>? _placeholders;
    private readonly IBookmarkResolver _bookmarkResolver;
    private readonly SupersedingAsyncValueGate<IIconInfo?> _iconReloadGate;
    private readonly Classification _classification;

    private IIconInfo? _icon;

    public IIconInfo Icon => _icon ?? Icons.Reloading;

    public string Name { get; }

    public string Id { get; }

    public LaunchBookmarkCommand(BookmarkData bookmarkData, Classification classification, IBookmarkIconLocator iconLocator, IBookmarkResolver bookmarkResolver, Dictionary<string, string>? placeholders = null)
    {
        ArgumentNullException.ThrowIfNull(bookmarkData);
        ArgumentNullException.ThrowIfNull(classification);

        _bookmarkData = bookmarkData;
        _classification = classification;
        _placeholders = placeholders;
        _bookmarkResolver = bookmarkResolver;

        Id = CommandIds.GetLaunchBookmarkItemId(bookmarkData.Id);
        Name = Resources.bookmarks_command_name_open;

        _iconReloadGate = new(
            async ct => await iconLocator.GetIconForPath(_classification, ct),
            icon =>
            {
                _icon = icon;
                OnPropertyChanged(nameof(Icon));
            });

        RequestIconReloadAsync();
    }

    private void RequestIconReloadAsync()
    {
        _icon = null;
        OnPropertyChanged(nameof(Icon));
        _ = _iconReloadGate.ExecuteAsync();
    }

    public ICommandResult Invoke(object sender)
    {
        var bookmarkAddress = ReplacePlaceholders(_bookmarkData.Bookmark);
        var classification = _bookmarkResolver.ClassifyOrUnknown(bookmarkAddress);

        var success = CommandLauncher.Launch(classification);

        return success
            ? CommandResult.Dismiss()
            : CommandResult.ShowToast(new ToastArgs
            {
                Message = !string.IsNullOrWhiteSpace(_bookmarkData.Name)
                    ? string.Format(CultureInfo.CurrentCulture, FailedToOpenMessageFormat, _bookmarkData.Name + ": " + bookmarkAddress)
                    : string.Format(CultureInfo.CurrentCulture, FailedToOpenMessageFormat, bookmarkAddress),
                Result = CommandResult.KeepOpen(),
            });
    }

    private string ReplacePlaceholders(string input)
    {
        var result = input;
        if (_placeholders?.Count > 0)
        {
            foreach (var (key, value) in _placeholders)
            {
                var placeholderString = $"{{{key}}}";

                var encodedValue = value;
                if (_classification.Kind is CommandKind.Protocol or CommandKind.WebUrl)
                {
                    encodedValue = Uri.EscapeDataString(value);
                }

                result = result.Replace(placeholderString, encodedValue, StringComparison.OrdinalIgnoreCase);
            }
        }

        return result;
    }

    public void Dispose()
    {
        _iconReloadGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
