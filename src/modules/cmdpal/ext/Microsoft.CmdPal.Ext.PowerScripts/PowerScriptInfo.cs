// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>A PowerScript and its consumer-renderable parameter contract.</summary>
internal sealed record PowerScriptInfo(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<PowerScriptParameterInfo> Parameters);
