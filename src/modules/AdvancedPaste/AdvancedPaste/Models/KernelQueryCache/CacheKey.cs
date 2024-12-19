// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace AdvancedPaste.Models.KernelQueryCache;

public sealed class CacheKey : IEquatable<CacheKey>
{
    public static StringComparer PromptComparer => StringComparer.CurrentCultureIgnoreCase;

    public string Prompt { get; init; }

    public ClipboardFormat AvailableFormats { get; init; }

    public override string ToString() => $"{AvailableFormats}: {Prompt}";

    public override bool Equals(object obj) => Equals(obj as CacheKey);

    public bool Equals(CacheKey other) => other != null && PromptComparer.Equals(Prompt, other.Prompt) && AvailableFormats == other.AvailableFormats;

    public override int GetHashCode() => PromptComparer.GetHashCode(Prompt) ^ AvailableFormats.GetHashCode();
}
