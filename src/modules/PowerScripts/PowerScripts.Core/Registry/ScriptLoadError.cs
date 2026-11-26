// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerScripts.Core.Execution;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Registry;

/// <summary>
/// A manifest that failed to load or validate, kept so the UI can surface problems.
/// </summary>
public sealed record ScriptLoadError(string FolderPath, string Message);
