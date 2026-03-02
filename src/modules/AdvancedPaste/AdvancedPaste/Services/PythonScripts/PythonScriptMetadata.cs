// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Models;

namespace AdvancedPaste.Services.PythonScripts;

public sealed record PythonScriptMetadata(
    string ScriptPath,
    string Name,
    string Description,
    ClipboardFormat SupportedFormats,
    string Platform,
    string Version);
