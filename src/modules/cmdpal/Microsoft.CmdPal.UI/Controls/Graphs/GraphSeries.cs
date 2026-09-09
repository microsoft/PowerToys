// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

public sealed record GraphSeries(string Name, Color? Color = null, GraphStrokeStyle LineStyle = GraphStrokeStyle.Solid, bool IsReadoutOnly = false, string ReadoutValueSuffix = "");
