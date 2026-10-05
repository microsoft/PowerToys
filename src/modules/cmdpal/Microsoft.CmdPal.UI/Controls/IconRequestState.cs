// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Controls;

internal sealed class IconRequestState
{
    private long _version;
    private long _invalidatedThrough;
    private long _lastAppliedVersion;
    private bool _isLatestRequestActive;

    public object? SourceKey { get; private set; }

    public object? PresentationOwner { get; private set; }

    public ElementTheme Theme { get; private set; }

    public bool IsLatestRequestActive => _isLatestRequestActive;

    public bool ChangeSource(object? sourceKey, ElementTheme theme, object? owner, out bool retainPresentation)
    {
        var sourceChanged = !ReferenceEquals(SourceKey, sourceKey);
        var sameOwner = owner is not null && ReferenceEquals(owner, PresentationOwner);
        retainPresentation = !sourceChanged || sameOwner;
        if (!sourceChanged && theme == Theme)
        {
            return false;
        }

        var retainCandidates = sameOwner && theme == Theme;
        SourceKey = sourceKey;
        PresentationOwner = owner;
        Theme = theme;
        if (!retainCandidates)
        {
            Invalidate();
        }

        return sourceChanged;
    }

    public long Begin()
    {
        _isLatestRequestActive = true;
        return ++_version;
    }

    public void Invalidate()
    {
        _invalidatedThrough = ++_version;
        _isLatestRequestActive = false;
    }

    public bool IsCurrent(long version)
    {
        return _isLatestRequestActive && version == _version;
    }

    public bool CanApply(long version, bool hasResult)
    {
        return version > _invalidatedThrough && version > _lastAppliedVersion && version <= _version
            && (hasResult || IsCurrent(version));
    }

    public void MarkApplied(long version)
    {
        if (CanApply(version, hasResult: true))
        {
            _lastAppliedVersion = version;
        }
    }

    public bool ShouldClearOnFailure(long version)
    {
        return IsCurrent(version) && CanApply(version, hasResult: false);
    }

    public void Complete(long version)
    {
        if (IsCurrent(version))
        {
            _isLatestRequestActive = false;
        }
    }
}
