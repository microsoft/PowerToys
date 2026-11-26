// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace WorkspacesCsharpLibrary.Utils;

/// <summary>
/// The saved workspace changed since the editor read it.
/// </summary>
public sealed class WorkspaceConflictException(string message) : IOException(message);
