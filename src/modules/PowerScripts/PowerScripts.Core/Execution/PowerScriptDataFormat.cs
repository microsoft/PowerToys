// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Execution;

/// <summary>
/// The data shapes a script consumes or produces. Media and HTML documents are files.
/// </summary>
public enum PowerScriptDataFormat
{
    None,
    Text,
    Files = 6,
}
