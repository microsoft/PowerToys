// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

// Fixed marks {{date}}, {{time}}, {{datetime}} and {{isodate}}, whose Value is the variable's name.
internal readonly record struct SnippetToken(SnippetTokenKind Kind, string Value = "", bool Required = false, bool Fixed = false);
