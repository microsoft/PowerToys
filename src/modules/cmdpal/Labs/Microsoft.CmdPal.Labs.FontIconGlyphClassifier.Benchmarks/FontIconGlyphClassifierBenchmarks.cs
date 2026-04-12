// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using ManagedGlyphClassifier = Microsoft.CmdPal.Common.Helpers.FontIconGlyphClassifier;
using ManagedGlyphKind = Microsoft.CmdPal.Common.Helpers.FontIconGlyphKind;
using NativeGlyphClassifier = Microsoft.Terminal.UI.FontIconGlyphClassifier;
using NativeGlyphKind = Microsoft.Terminal.UI.FontIconGlyphKind;

namespace Microsoft.CmdPal.Labs.FontIconGlyphClassifier.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class FontIconGlyphClassifierBenchmarks
{
    public IEnumerable<InputCase> Cases =>
    [
        new("AsciiUpper", "A"),
        new("AsciiLower", "z"),
        new("Digit", "7"),
        new("AsciiSymbol", "#"),
        new("NonAsciiSymbol", "∞"),
        new("FluentSymbol", "\uE8C8"),
        new("EmojiSimple", "😀"),
        new("EmojiZwj", "👨‍👩‍👧‍👦"),
        new("EmojiVs16", "❤️"),
        new("WordAscii", "PowerToys"),
        new("WordUnicode", "日本語"),
        new("PathLike", @"C:\Temp\icon.png"),
        new("AppxLike", "ms-appx:///Assets/Icons/ExtensionIconPlaceholder.png"),
    ];

    [ParamsSource(nameof(Cases))]
    public InputCase Case { get; set; } = default!;

    [Benchmark(Baseline = true)]
    public NativeGlyphKind Native_Classify() => NativeGlyphClassifier.Classify(Case.Value);

    [Benchmark]
    public ManagedGlyphKind Managed_Classify() => ManagedGlyphClassifier.Classify(Case.Value);

    [Benchmark]
    public bool Native_IsLikelyToBeEmojiOrSymbolIcon() => NativeGlyphClassifier.IsLikelyToBeEmojiOrSymbolIcon(Case.Value);

    [Benchmark]
    public bool Managed_IsLikelyToBeEmojiOrSymbolIcon() => ManagedGlyphClassifier.IsLikelyToBeEmojiOrSymbolIcon(Case.Value);

    public sealed record InputCase(string Name, string Value)
    {
        public override string ToString() => Name;
    }

    private sealed class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.ShortRun.WithToolchain(InProcessNoEmitToolchain.Instance));
            AddValidator(InProcessValidator.DontFailOnError);
        }
    }
}
