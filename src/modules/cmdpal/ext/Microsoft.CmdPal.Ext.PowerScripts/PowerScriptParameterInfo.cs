// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.PowerScripts;

internal sealed record PowerScriptParameterInfo(
    string Name,
    string Type,
    bool IsRequired,
    string? Label,
    string? Description,
    string? Default,
    IReadOnlyList<string> Options,
    int? Min,
    int? Max);
