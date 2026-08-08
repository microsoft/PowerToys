// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Foundation
{
    [TestClass]
    public class FrozenContractTests
    {
        [TestMethod]
        public void Show_event_name_is_frozen()
        {
            Assert.AreEqual(
                @"Local\ShowColorPickerEvent-8c46be2a-3e05-4186-b56b-4ae986ef2525",
                PowerToys.Interop.Constants.ShowColorPickerSharedEvent());
        }

        [TestMethod]
        public void Terminate_event_name_is_frozen()
        {
            Assert.AreEqual(
                @"Local\TerminateColorPickerEvent-3d676258-c4d5-424e-a87a-4be22020e813",
                PowerToys.Interop.Constants.TerminateColorPickerSharedEvent());
        }

        [TestMethod]
        public void SendSettingsTelemetry_event_name_is_frozen()
        {
            Assert.AreEqual(
                @"Local\ColorPickerSettingsTelemetryEvent-6c7071d8-4014-46ec-b687-913bd8a422f1",
                PowerToys.Interop.Constants.ColorPickerSendSettingsTelemetryEvent());
        }
    }
}
