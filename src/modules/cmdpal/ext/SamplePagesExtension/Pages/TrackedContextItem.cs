// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

/// <summary>
/// A MoreCommands entry whose release by the host is observable.
/// </summary>
/// <remarks>
/// The host builds these eagerly - <c>BuildAndInitMoreCommands</c> runs from
/// <c>InitializeProperties</c>, not when a context menu opens - so every item in
/// a list materialises all of its context items and their proxies up front.
/// That multiplier is the reason these are worth counting separately.
/// </remarks>
internal sealed partial class TrackedContextItem : CommandContextItem
{
    public TrackedContextItem(string title)
        : base(new TrackedCommand(title))
    {
        Title = title;
        LeakTracker.ContextItems.OnCreated();
    }

    ~TrackedContextItem() => LeakTracker.ContextItems.OnReleased();
}
