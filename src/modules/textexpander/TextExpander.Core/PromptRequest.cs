// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>One runtime question embedded in a snippet.</summary>
public readonly record struct PromptRequest(string Label, bool Required);
