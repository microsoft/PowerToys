// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Services;

/// <summary>
/// Provides functionality for sending text to the active application by simulating keystrokes.
/// </summary>
public interface IKeystrokeService
{
    /// <summary>
    /// Sends the specified text to the active application as a sequence of keystrokes.
    /// </summary>
    /// <param name="text">The text to send as simulated keystrokes.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="text"/> is <see langword="null"/>.
    /// </exception>
    void SendTextAsKeystrokes(string text);
}
