// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace AdvancedPaste.Cli;

internal sealed record ClipboardSnapshot(string? Text, string? Html, IReadOnlyList<string> FilePaths, byte[]? BitmapBytes);
