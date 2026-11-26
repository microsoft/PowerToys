// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Templates;

/// <summary>Instantiates a known operation without running it, checking deployment or granting trust.</summary>
public sealed record PowerToysOperationTemplateOptions(
    string CommandId,
    IReadOnlyDictionary<string, CliActionBinding>? Bindings = null);
