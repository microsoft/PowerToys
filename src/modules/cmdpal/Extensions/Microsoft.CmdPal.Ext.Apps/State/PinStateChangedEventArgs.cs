// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Apps.State;

public class PinStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the identifier of the application whose pin state has changed.
    /// </summary>
    public string AppIdentifier { get; }

    /// <summary>
    /// Gets a value indicating whether the specified app identifier was pinned or not.
    /// </summary>
    public bool IsPinned { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PinStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="appIdentifier">The identifier of the application whose pin state has changed.</param>
    public PinStateChangedEventArgs(string appIdentifier, bool isPinned)
    {
        AppIdentifier = appIdentifier ?? throw new ArgumentNullException(nameof(appIdentifier));
        IsPinned = isPinned;
    }
}
