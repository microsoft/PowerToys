// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Describes one natively rendered element type: how to parse it and how to draw it.</summary>
internal sealed record AdaptiveVisualElementType(
    string Name,
    Func<string, ICollection<string>, IAdaptiveVisualModel> Parse,
    Func<IAdaptiveVisualModel, UIElement> Create);
