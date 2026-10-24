// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

/// <summary>Coalesces one source's refresh work and tracks completion under the catalog lock.</summary>
internal sealed class SourceRefreshRequest
{
    internal const int MaximumPathChanges = 256;

    private readonly List<AppSourcePathChange> _pathChanges = [];
    private readonly Dictionary<string, int> _pathChangeIndexes = new(StringComparer.OrdinalIgnoreCase);
    private TaskCompletionSource? _publication;

    public IAppSource Source { get; }

    public bool RequiresFullRefresh { get; private set; }

    public bool Background { get; private set; }

    public long? QueuedAt { get; private set; }

    public IReadOnlyList<AppSourcePathChange> PathChanges => _pathChanges;

    /// <summary>Initializes a new instance of the <see cref="SourceRefreshRequest"/> class. Creates coalesced refresh work for one source and records its initial priority and queue time.</summary>
    public SourceRefreshRequest(IAppSource source, bool background, long? queuedAt)
    {
        Source = source;
        Background = background;
        QueuedAt = queuedAt;
    }

    /// <summary>Gets a task completed when this request is handled or retired, independently of later queued work.</summary>
    public Task GetPublicationTask()
    {
        return (_publication ??= new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
    }

    /// <summary>Releases waiters after this request has been handled or retired.</summary>
    public void CompletePublication()
    {
        _publication?.TrySetResult();
    }

    /// <summary>Promotes the request to foreground work when any contributor requires foreground priority.</summary>
    public void IncludePriority(bool background)
    {
        // Manual and watcher work takes priority over coalesced background recovery.
        Background &= background;
    }

    /// <summary>Adds an invalidation and incorporates its requested priority and scan scope.</summary>
    public void Add(AppSourceInvalidatedEventArgs invalidation, bool background = false)
    {
        IncludePriority(background);
        if (invalidation.RequiresFullRefresh)
        {
            RequireFullRefresh();
        }
        else if (invalidation.PathChange is not null)
        {
            AddPathChange(invalidation.PathChange);
        }
    }

    /// <summary>Merges dirty paths, scan scope, priority, and the oldest queue timestamp from another request.</summary>
    public void Merge(SourceRefreshRequest other)
    {
        if (other.QueuedAt is { } queuedAt && (QueuedAt is null || queuedAt < QueuedAt))
        {
            QueuedAt = queuedAt;
        }

        IncludePriority(other.Background);
        if (other.RequiresFullRefresh)
        {
            RequireFullRefresh();
        }

        foreach (var pathChange in other.PathChanges)
        {
            AddPathChange(pathChange);
        }
    }

    /// <summary>Requests a whole-source scan while preserving dirty paths that must bypass cached reads.</summary>
    public void RequireFullRefresh()
    {
        // A full scan still needs dirty paths to bypass cached candidate results.
        RequiresFullRefresh = true;
    }

    private void AddPathChange(AppSourcePathChange change)
    {
        if (!string.IsNullOrWhiteSpace(change.OldPath))
        {
            AddOrReplacePathChange(new AppSourcePathChange(WatcherChangeTypes.Deleted, change.OldPath));
        }

        AddOrReplacePathChange(new AppSourcePathChange(change.ObservedKind, change.Path));
    }

    private void AddOrReplacePathChange(AppSourcePathChange change)
    {
        if (_pathChangeIndexes.TryGetValue(change.Path, out var index))
        {
            _pathChanges[index] = change;
            return;
        }

        if (_pathChanges.Count >= MaximumPathChanges)
        {
            RequireFullRefresh();
            return;
        }

        _pathChangeIndexes.Add(change.Path, _pathChanges.Count);
        _pathChanges.Add(change);
    }
}
