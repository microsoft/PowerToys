// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.PreviewPane.UITests;

[TestClass]
public sealed class ProviderLogCursorTests
{
    private string directory = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        directory = Directory.CreateTempSubdirectory("PowerToys-ProviderLogCursor-").FullName;
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public void ReadsFreshActivationWhileProviderRetainsLogHandle()
    {
        const string banner = "SvgThumbnailProvider logger is initialized";
        string path = Path.Combine(directory, "provider.log");
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        writer.WriteLine(banner);
        var cursor = new ProviderLogCursor(directory);
        Assert.AreEqual(string.Empty, cursor.ReadNewText(), "An existing provider banner must not satisfy a fresh activation.");

        writer.WriteLine(banner);

        Assert.AreEqual(banner + Environment.NewLine, cursor.ReadNewText());
        Assert.IsTrue(File.Exists(path), "Observing a new activation must not delete the provider's open log.");
    }

    [TestMethod]
    public void IgnoresHistoricalFailureButPreservesNewGenerationFailure()
    {
        const string failure = "Failed to copy thumbnail input.";
        string path = Path.Combine(directory, "provider.log");
        File.WriteAllText(path, failure + Environment.NewLine);
        var cursor = new ProviderLogCursor(directory);
        Assert.IsFalse(ThumbnailProviderLog.HasGenerationFailure(cursor.ReadNewText(), "SvgThumbnailProvider"));

        File.AppendAllText(path, failure + Environment.NewLine);

        Assert.IsTrue(ThumbnailProviderLog.HasGenerationFailure(cursor.ReadNewText(), "SvgThumbnailProvider"));
    }

    [TestMethod]
    public void ReadsNewDailyLogAfterRollover()
    {
        File.WriteAllText(Path.Combine(directory, "previous.log"), "historical activation");
        var cursor = new ProviderLogCursor(directory);
        File.WriteAllText(Path.Combine(directory, "next.log"), "fresh activation");

        Assert.AreEqual("fresh activation", cursor.ReadNewText());
    }

    [TestMethod]
    public void ReadsTruncatedLogFromItsNewBeginning()
    {
        string path = Path.Combine(directory, "provider.log");
        File.WriteAllText(path, new string('x', 100));
        var cursor = new ProviderLogCursor(directory);
        File.WriteAllText(path, "fresh activation");

        Assert.AreEqual("fresh activation", cursor.ReadNewText());
    }
}
