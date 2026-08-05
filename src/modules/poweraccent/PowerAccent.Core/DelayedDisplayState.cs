// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core;

internal sealed class DelayedDisplayState
{
    private int _generation;

    public bool IsVisible { get; private set; }

    public int Begin()
    {
        IsVisible = true;
        return ++_generation;
    }

    public bool ShouldShow(int generation)
    {
        return IsVisible && generation == _generation;
    }

    public void Cancel()
    {
        IsVisible = false;
        _generation++;
    }
}
