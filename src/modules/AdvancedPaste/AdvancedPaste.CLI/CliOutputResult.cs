// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.Cli;

internal sealed record CliOutputResult(string ResultKind, string? Text, string? OutputPath, bool OutputClipboard);
