// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Describes one natively rendered element type: how to parse it and how to draw it.</summary>
/// <param name="Name">The element's <c>type</c>.</param>
/// <param name="Parse">Parses the element JSON into its model.</param>
/// <param name="Create">Creates the control that draws a model.</param>
/// <param name="RendersItself">Gets whether element JSON draws itself, or null when every element of the type does.</param>
internal sealed record AdaptiveVisualElementType(
    string Name,
    Func<string, ICollection<string>, IAdaptiveVisualModel> Parse,
    Func<IAdaptiveVisualModel, UIElement> Create,
    Func<JsonElement, bool>? RendersItself = null);
