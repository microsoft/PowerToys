// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

using MouseJump.Models.Layout;

namespace MouseJump.Common.Capture;

/// <summary>
/// Receives screenshots as <see cref="ScreenshotCapturePipeline"/> captures them, one screen at
/// a time, in whatever order they actually complete.
/// </summary>
public interface IScreenshotCaptureSink
{
    /// <summary>
    /// Applies <paramref name="bitmap"/> as the screenshot for <paramref name="screenLayout"/>.
    /// Ownership of <paramref name="bitmap"/> transfers to the implementation - the caller never
    /// disposes it, and never reuses the same instance for a later capture, so an implementation
    /// is free to keep holding onto it (e.g. to read from on a background thread) well after
    /// this returns, and is responsible for disposing it once actually done.
    /// </summary>
    Task SetScreenshotAsync(ScreenLayout screenLayout, Bitmap bitmap);
}
