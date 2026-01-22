// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public sealed record AcrylicBackdropParameters(Color TintColor, Color FallbackColor, float TintOpacity, float LuminosityOpacity);
