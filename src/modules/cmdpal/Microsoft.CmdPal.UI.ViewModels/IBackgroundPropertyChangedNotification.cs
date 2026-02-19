// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace Microsoft.CmdPal.ViewModels;

/// <summary>
/// Provides a notification mechanism for property changes that fires
/// synchronously on the calling thread.
/// </summary>
public interface IBackgroundPropertyChangedNotification
{
    /// <summary>
    /// Occurs when the value of a property changes.
    /// </summary>
    event PropertyChangedEventHandler? PropertyChangedBackground;
}
