// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>
/// A single PowerScript discovered from <c>PowerScripts.Host.exe list --json</c>. Only the fields the
/// Command Palette needs are captured.
/// </summary>
internal sealed record PowerScriptInfo(string Id, string Name, string Description);
