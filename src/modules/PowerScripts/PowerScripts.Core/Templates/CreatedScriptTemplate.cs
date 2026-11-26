// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PowerScripts.Core.Cli;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;

namespace PowerScripts.Core.Templates;

public sealed record CreatedScriptTemplate(string EntryPath, string GuidePath);
