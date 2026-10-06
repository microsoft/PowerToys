// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Helpers;

internal interface IIconSourceProvider
{
    /// <summary>Loads an icon for the captured rendering context and display scale.</summary>
    /// <param name="icon">The icon data and current presentation to resolve.</param>
    /// <param name="scale">The display scale for this request.</param>
    /// <param name="context">The captured surface theme and contrast.</param>
    /// <param name="diagnostics">Optional measurements for this request.</param>
    /// <param name="demand">Optional consumer demand for scheduling and cancelling queued work.</param>
    /// <returns>A task for the resolved source, which can be null when no icon is available or fault on a load failure.</returns>
    Task<IconSource?> GetIconSource(
        IconDataViewModel icon,
        double scale,
        IconRenderContext context,
        IconRequestMeasurement diagnostics = default,
        IIconRequestDemand? demand = null);
}
