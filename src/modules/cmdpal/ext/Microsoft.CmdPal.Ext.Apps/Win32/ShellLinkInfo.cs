// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Apps.Win32;

internal sealed record ShellLinkInfo(
    string TargetPath,
    string Description,
    string Arguments,
    string WorkingDirectory,
    string PackagedAppUserModelId,
    string IconLocation,
    string ExplicitAppUserModelId);
