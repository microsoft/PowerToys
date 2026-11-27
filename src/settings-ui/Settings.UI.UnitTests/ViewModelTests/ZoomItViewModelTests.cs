// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class ZoomItViewModelTests
    {
        private const string EmptyDescription = "No input file selected";

        private static readonly string[] ExpectedOperations = ["save", "refresh"];

        [TestMethod]
        public void ClearDemoTypeFile_PersistsEmptyPathBeforeRefreshAndPreservesOtherSettings()
        {
            var settings = CreateSettings(@"C:\Demos\script.txt");
            var originalHotkey = settings.Properties.DemoTypeToggleKey.Value;
            var operations = new List<string>();
            string savedJson = null;
            var viewModel = new ZoomItViewModel(
                settings,
                json =>
                {
                    savedJson = json;
                    operations.Add("save");
                },
                message =>
                {
                    using var document = JsonDocument.Parse(message);
                    Assert.AreEqual("refresh_settings", document.RootElement.GetProperty("action").GetProperty("ZoomIt").GetProperty("action_name").GetString());
                    operations.Add("refresh");
                    return 0;
                },
                EmptyDescription);
            var changedProperties = new List<string>();
            viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

            viewModel.ClearDemoTypeFileCommand.Execute(null);

            Assert.AreEqual(string.Empty, viewModel.DemoTypeFile);
            Assert.IsFalse(viewModel.HasDemoTypeFile);
            Assert.AreEqual(EmptyDescription, viewModel.DemoTypeFileDescription);
            CollectionAssert.AreEqual(ExpectedOperations, operations);
            CollectionAssert.AreEquivalent(
                new[] { nameof(viewModel.DemoTypeFile), nameof(viewModel.HasDemoTypeFile), nameof(viewModel.DemoTypeFileDescription) },
                changedProperties);
            var persisted = JsonSerializer.Deserialize<ZoomItSettings>(savedJson);
            Assert.AreEqual(string.Empty, persisted.Properties.DemoTypeFile.Value);
            Assert.AreEqual(JsonSerializer.Serialize(originalHotkey), JsonSerializer.Serialize(persisted.Properties.DemoTypeToggleKey.Value));
            Assert.AreEqual(50, persisted.Properties.DemoTypeSpeedSlider.Value);
            Assert.IsTrue(persisted.Properties.DemoTypeUserDrivenMode.Value);
        }

        [TestMethod]
        public void ClearDemoTypeFile_AlreadyEmpty_DoesNotSaveOrRefresh()
        {
            var viewModel = new ZoomItViewModel(
                CreateSettings(string.Empty),
                _ => Assert.Fail("An unchanged path must not be saved."),
                _ =>
                {
                    Assert.Fail("An unchanged path must not refresh settings.");
                    return 0;
                },
                EmptyDescription);

            viewModel.ClearDemoTypeFileCommand.Execute(null);

            Assert.IsFalse(viewModel.HasDemoTypeFile);
            Assert.AreEqual(EmptyDescription, viewModel.DemoTypeFileDescription);
        }

        [TestMethod]
        public void DemoTypeFile_SelectedAfterClear_RestoresDescriptionAndCanBeClearedAgain()
        {
            var saves = new List<string>();
            var viewModel = new ZoomItViewModel(CreateSettings(@"C:\Demos\first.txt"), saves.Add, _ => 0, EmptyDescription);
            viewModel.ClearDemoTypeFileCommand.Execute(null);

            viewModel.DemoTypeFile = @"C:\Demos\second.txt";

            Assert.IsTrue(viewModel.HasDemoTypeFile);
            Assert.AreEqual(@"C:\Demos\second.txt", viewModel.DemoTypeFileDescription);
            viewModel.ClearDemoTypeFileCommand.Execute(null);
            Assert.IsFalse(viewModel.HasDemoTypeFile);
            Assert.AreEqual(3, saves.Count);
            Assert.AreEqual(string.Empty, JsonSerializer.Deserialize<ZoomItSettings>(saves[2]).Properties.DemoTypeFile.Value);
        }

        private static ZoomItSettings CreateSettings(string filePath)
        {
            return new ZoomItSettings
            {
                Properties = new ZoomItProperties
                {
                    DemoTypeFile = new StringProperty(filePath),
                    DemoTypeToggleKey = new KeyboardKeysProperty(ZoomItProperties.DefaultDemoTypeToggleKey),
                    DemoTypeSpeedSlider = new IntProperty(50),
                    DemoTypeUserDrivenMode = new BoolProperty(true),
                },
            };
        }
    }
}
