// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core;

public sealed record ConsumerState(string Id, bool UserEnabled, bool IsEnabled, bool CanChange, bool IsPolicyControlled, string? DisabledReason);
