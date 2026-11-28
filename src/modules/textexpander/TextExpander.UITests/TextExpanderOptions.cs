// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>The engine options in the module's settings.json, with the defaults a new install has.</summary>
internal sealed record TextExpanderOptions
{
    public string InjectionBackend { get; init; } = "auto";

    public int ClipboardThresholdChars { get; init; } = 5;

    public bool CaseSensitive { get; init; } = true;

    public bool RequireWordBoundary { get; init; } = true;

    public bool WordBoundaryKeepsSpace { get; init; } = true;

    public bool TreatRemapsAsTyping { get; init; } = true;

    public string SnippetsPath { get; init; } = string.Empty;
}
