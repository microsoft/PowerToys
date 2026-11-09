// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.FilePreviewer.Models;
using Settings.UI.Library;

namespace Peek.FilePreviewer.UnitTests
{
    [TestClass]
    public class PreviewSettingsTests
    {
        [TestMethod]
        public void ToMaxFileSizeBytes_InRangeValue_ShouldConvertToBytes()
        {
            Assert.AreEqual(10240L * 1024, PreviewSettings.ToMaxFileSizeBytes(10240));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(int.MinValue)]
        public void ToMaxFileSizeBytes_BelowMinimum_ShouldClampToMinimum(int kilobytes)
        {
            Assert.AreEqual((long)PeekPreviewSettings.MinSourceCodeMaxFileSize * 1024, PreviewSettings.ToMaxFileSizeBytes(kilobytes));
        }

        [TestMethod]
        [DataRow(PeekPreviewSettings.MaxSourceCodeMaxFileSize + 1)]
        [DataRow(int.MaxValue)]
        public void ToMaxFileSizeBytes_AboveMaximum_ShouldClampToMaximum(int kilobytes)
        {
            Assert.AreEqual((long)PeekPreviewSettings.MaxSourceCodeMaxFileSize * 1024, PreviewSettings.ToMaxFileSizeBytes(kilobytes));
        }

        [TestMethod]
        public void Deserialize_LegacySettingsWithoutMaxFileSize_ShouldUseDefault()
        {
            // A preview-settings.json written before SourceCodeMaxFileSize existed.
            const string legacyJson = "{\"SourceCodeWrapText\":{\"value\":true},\"SourceCodeFontSize\":{\"value\":16}}";

            var settings = JsonSerializer.Deserialize(legacyJson, SettingsSerializationContext.Default.PeekPreviewSettings);

            Assert.IsNotNull(settings);
            Assert.AreEqual(PeekPreviewSettings.DefaultSourceCodeMaxFileSize, settings.SourceCodeMaxFileSize.Value);
        }

        [TestMethod]
        public void Deserialize_PeekMediaPlayback_UnsetStaysNullAndRoundTrips()
        {
            var legacy = JsonSerializer.Deserialize("{\"name\":\"Peek\",\"version\":\"0.0.2\",\"properties\":{}}", SettingsSerializationContext.Default.PeekSettings);
            Assert.IsNotNull(legacy);
            Assert.IsNull(legacy.Properties.MediaVolume);
            Assert.IsNull(legacy.Properties.MediaMuted);

            var settings = new PeekSettings();
            settings.Properties.MediaVolume = new DoubleProperty(0d);
            settings.Properties.MediaMuted = new BoolProperty(true);
            var clone = JsonSerializer.Deserialize(settings.ToJsonString(), SettingsSerializationContext.Default.PeekSettings);
            Assert.IsNotNull(clone);
            Assert.AreEqual(0d, clone.Properties.MediaVolume.Value, 0.0000001d);
            Assert.IsTrue(clone.Properties.MediaMuted.Value);
        }
    }
}
