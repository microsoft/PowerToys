// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Common.UI.Controls.Window;

/// <summary>
/// Data for <see cref="TransparentWindow.Showing"/>. Carries the transition the
/// content should play, or <see langword="null"/> to let the content use its own
/// configured show transition.
/// </summary>
public sealed class ShowingEventArgs : EventArgs
{
    public ShowingEventArgs(Transition? transition)
    {
        Transition = transition;
    }

    /// <summary>
    /// Gets the transition the content should play, or <see langword="null"/> to
    /// use the content's own configured show transition.
    /// </summary>
    public Transition? Transition { get; }
}
