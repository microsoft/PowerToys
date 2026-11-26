// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Cli;

/// <summary>How a parameter is represented in an installed command's argument vector.</summary>
public enum CliArgumentKind
{
    Positional,
    Option,
    Switch,
}
