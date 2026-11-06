// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace AdvancedPaste.Cli;

internal sealed class CliActionUnavailableException(string message) : InvalidOperationException(message);
