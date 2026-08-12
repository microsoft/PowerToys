// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class IconProtocolRegistryTests
{
    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("\uE700")]
    [DataRow("C:\\Icons\\sample.svg")]
    [DataRow("|Unknown|value")]
    public void UnknownInputsDoNotEnterTheBuiltInRegistry(string? value)
    {
        Assert.IsNull(IconProtocolRegistry.Find(value));
    }

    [TestMethod]
    public void OrdinaryInputsSkipProcessorPrefixAccess()
    {
        var processor = new TestProcessor("|Test|");

        var result = IconProtocolRegistry.Find("ordinary.png", [processor]);

        Assert.IsNull(result);
        Assert.AreEqual(0, processor.PrefixAccesses);
    }

    [TestMethod]
    public void RegistryReturnsMatchingProcessorWithoutInspectingLaterProcessors()
    {
        var first = new TestProcessor("|Other|");
        var matching = new TestProcessor("|Test|", "|Alternate|");
        var later = new TestProcessor("|Later|");

        var result = IconProtocolRegistry.Find("|Alternate|value", [first, matching, later]);

        Assert.AreSame(matching, result);
        Assert.AreEqual(1, first.PrefixAccesses);
        Assert.AreEqual(1, matching.PrefixAccesses);
        Assert.AreEqual(0, later.PrefixAccesses);
    }

    [TestMethod]
    public void ValidationAcceptsDistinctProtocolPrefixes()
    {
        IconProtocolRegistry.ValidateProcessors(
            [
                new TestProcessor("|Svg|", "|ThemedSvg|"),
                new TestProcessor("|AppIcon|", "|JumboAppIcon|"),
            ]);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Test|")]
    public void ValidationRejectsMalformedProtocolPrefixes(string? prefix)
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            IconProtocolRegistry.ValidateProcessors([new TestProcessor([prefix!])]));
    }

    [DataTestMethod]
    [DataRow("|Test|", "|Test|")]
    [DataRow("|Icon", "|IconX|")]
    [DataRow("|Icon|", "|Icon|Variant|")]
    public void ValidationRejectsDuplicateOrOverlappingProtocolPrefixes(string first, string second)
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            IconProtocolRegistry.ValidateProcessors(
                [new TestProcessor(first), new TestProcessor(second)]));
    }

    [TestMethod]
    public void ValidationRejectsProcessorWithoutProtocolPrefixes()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            IconProtocolRegistry.ValidateProcessors([new TestProcessor()]));
    }

    [TestMethod]
    public void ProcessingResultCanTransferPreparedIconOwnershipOnce()
    {
        var prepared = IconPathConverter.PreparedIcon.FromGlyph("\uE700", "Segoe Fluent Icons", 20);
        using var result = IconProtocolProcessingResult.FromPreparedIcon(prepared);

        var transferred = result.TakePreparedIcon();

        Assert.IsNotNull(transferred);
        Assert.AreSame(prepared, transferred);
        Assert.IsNull(result.TakePreparedIcon());
        transferred.Dispose();
    }

    private sealed class TestProcessor : IIconProtocolProcessor
    {
        private readonly string[] _prefixes;

        public TestProcessor(params string[] prefixes)
        {
            _prefixes = prefixes;
        }

        public int PrefixAccesses { get; private set; }

        public IconCachePartition CachePartition => IconCachePartition.Other;

        public ReadOnlySpan<string> ProtocolPrefixes
        {
            get
            {
                PrefixAccesses++;
                return _prefixes;
            }
        }

        public ElementTheme GetCacheTheme(string value, ElementTheme theme) => ElementTheme.Default;

        public IconLoadInputKind ClassifyInput(string value) => IconLoadInputKind.String;

        public bool TryPrepareSynchronously(
            string value,
            int targetSize,
            ElementTheme theme,
            out IconPathConverter.PreparedIcon preparedIcon)
        {
            preparedIcon = IconPathConverter.PreparedIcon.Empty();
            return true;
        }

        public ValueTask<IconProtocolProcessingResult> PrepareAsync(
            string value,
            int targetSize,
            ElementTheme theme) =>
            ValueTask.FromResult(IconProtocolProcessingResult.Empty());
    }
}
