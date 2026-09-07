// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

// A null value hides the row; a null color hides its swatch.
internal readonly record struct GraphTooltipItem(string Name, string? Value, Color? Color);
