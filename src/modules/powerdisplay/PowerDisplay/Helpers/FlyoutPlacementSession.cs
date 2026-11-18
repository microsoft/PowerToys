// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Common.UI.Controls.Window;

namespace PowerDisplay.Helpers;

/// <summary>
/// Keeps one placement context throughout a flyout's visible lifetime.
/// </summary>
internal sealed class FlyoutPlacementSession
{
    public FlyoutPlacementContext? Current { get; private set; }

    public bool IsShowing { get; private set; }

    /// <summary>
    /// Gets the generation used to discard callbacks queued for an earlier session.
    /// </summary>
    public long Generation { get; private set; }

    public void BeginShow(FlyoutPlacementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Current = context;
        IsShowing = true;
        Generation++;
    }

    public void FinishShow()
    {
        IsShowing = false;
    }

    public void Clear()
    {
        Current = null;
        IsShowing = false;
        Generation++;
    }

    public bool CanApplyLayout(bool isWindowVisible)
        => Current is not null && (IsShowing || isWindowVisible);
}
