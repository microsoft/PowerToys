// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Execution;

/// <summary>
/// The data shapes a script consumes or produces. Video remains file-backed.
/// </summary>
public enum PowerScriptDataFormat
{
    None,
    Text,
    Image = 3,
    Audio = 4,
    Files = 6,
}
