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
        Title = item.Title;
        Subtitle = item.Subtitle;
        Icon = item.Icon;
        Command = item.Command;
        MoreCommands = [.. item.MoreCommands ?? []];
        Details = item.Details;
        Section = item.Section;
        TextToSuggest = item.TextToSuggest;
        _extendedProperties = item is IExtendedAttributesProvider attributes
            ? new Dictionary<string, object>(attributes.GetProperties() ?? new Dictionary<string, object>())
            : new Dictionary<string, object>();

        var tags = item.Tags ?? [];
        if (string.IsNullOrWhiteSpace(sourceAttribution))
        {
            Tags = [.. tags];
        }
        else
        {
            Tags = new ITag[tags.Length + 1];
            Tags[0] = new Tag(sourceAttribution);
            Array.Copy(tags, 0, Tags, 1, tags.Length);
        }

        _materializationLease = snapshotLease.Acquire()
            ?? throw new InvalidOperationException("The fallback result snapshot is no longer available.");
    }

    internal FallbackQueryContext QueryContext { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public IIconInfo Icon { get; }

    public ICommand Command { get; }

    public IContextItem[] MoreCommands { get; }

    public ITag[] Tags { get; }

    public IDetails Details { get; }

    public string Section { get; }

    public string TextToSuggest { get; }

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
