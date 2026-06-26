// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Common.UI.Controls.Window;

/// <summary>
/// Data for <see cref="TransparentWindow.Showing"/>. Carries the edge the
/// content should slide in from, or <see langword="null"/> to let the content
/// use its own configured direction.
/// </summary>
public sealed class ShowingEventArgs : EventArgs
{
    public ShowingEventArgs(SlideDirection? direction)
    {
        Direction = direction;
    }

    /// <summary>
    /// Gets the edge the content should slide in from, or <see langword="null"/>
    /// to use the content's own configured direction.
    /// </summary>
    public SlideDirection? Direction { get; }
}
