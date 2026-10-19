// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class FallbackResultListItem : IListItem, IExtendedAttributesProvider, IDisposable
{
    private readonly IDictionary<string, object> _extendedProperties;
    private readonly string _title;
    private readonly string _subtitle;
    private readonly IIconInfo _icon;
    private readonly ICommand _command;
    private readonly IContextItem[] _moreCommands;
    private readonly ITag[] _tags;
    private readonly IDetails _details;
    private readonly string _section;
    private readonly string _textToSuggest;
    private IDisposable? _materializationLease;
    private int _published;

    internal FallbackResultListItem(
        IListItem item,
        TopLevelViewModel source,
        FallbackSnapshotLease snapshotLease,
        CancellationToken queryToken,
        string? sourceAttribution = null)
    {
        QueryContext = new(source.ExtensionHost, source.ProviderContext, item, snapshotLease, queryToken);
        _title = item.Title;
        _subtitle = item.Subtitle;
        _icon = item.Icon;
        _command = item.Command;
        _moreCommands = [.. item.MoreCommands ?? []];
        _details = item.Details;
        _section = item.Section;
        _textToSuggest = item.TextToSuggest;
        _extendedProperties = item is IExtendedAttributesProvider attributes
            ? new Dictionary<string, object>(attributes.GetProperties() ?? new Dictionary<string, object>())
            : new Dictionary<string, object>();

        var tags = item.Tags ?? [];
        if (string.IsNullOrWhiteSpace(sourceAttribution))
        {
            _tags = [.. tags];
        }
        else
        {
            _tags = new ITag[tags.Length + 1];
            _tags[0] = new Tag(sourceAttribution);
            Array.Copy(tags, 0, _tags, 1, tags.Length);
        }

        _materializationLease = snapshotLease.Acquire()
            ?? throw new InvalidOperationException("The fallback result snapshot is no longer available.");
    }

    internal FallbackQueryContext QueryContext { get; }

    public string Title => _title;

    public string Subtitle => _subtitle;

    public IIconInfo Icon => _icon;

    public ICommand Command => _command;

    public IContextItem[] MoreCommands => _moreCommands;

    public ITag[] Tags => _tags;

    public IDetails Details => _details;

    public string Section => _section;

    public string TextToSuggest => _textToSuggest;

    public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
    {
        add { }
        remove { }
    }

    public IDictionary<string, object> GetProperties() => _extendedProperties;

    /// <summary>
    /// Gives back the reference that this wrapper took when it was built.
    /// </summary>
    /// <remarks>
    /// <see cref="ListItemViewModel"/> calls this when it takes its own reference on
    /// the snapshot. After that the view-model controls the lifetime.
    /// </remarks>
    internal void ReleaseMaterializationLease() => Dispose();

    public void Dispose()
    {
        Interlocked.Exchange(ref _materializationLease, null)?.Dispose();

        // The finalizer only exists to catch a wrapper that nothing released.
        // This one is released, so keep it off the finalizer queue.
        GC.SuppressFinalize(this);
    }

    internal void MarkPublished() => Interlocked.Exchange(ref _published, 1);

    internal void ReleaseIfUnpublished()
    {
        if (Volatile.Read(ref _published) == 0)
        {
            ReleaseMaterializationLease();
        }
    }

    // Safety net. A wrapper that reaches the rendered list but never becomes a
    // ListItemViewModel - the query is superseded between the two steps - has no
    // other release path. Without this the snapshot stays open for the life of the
    // process. Normal paths call GC.SuppressFinalize, so few wrappers reach here.
    ~FallbackResultListItem()
    {
        Interlocked.Exchange(ref _materializationLease, null)?.Dispose();
    }
}
