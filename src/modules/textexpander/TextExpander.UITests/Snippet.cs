// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>A snippet as the snippets database stores it: the trigger and the exact text it inserts.</summary>
internal sealed record Snippet(string Trigger, string Replacement);
