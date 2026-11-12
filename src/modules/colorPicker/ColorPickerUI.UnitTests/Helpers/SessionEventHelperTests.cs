// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    [TestClass]
    public class SessionEventHelperTests
    {
        [TestMethod]
        public void Start_sets_Event_with_StartedAs()
        {
            SessionEventHelper.Start(ColorPickerActivationAction.OpenColorPicker);

            Assert.IsNotNull(SessionEventHelper.Event);
            Assert.AreEqual(ColorPickerActivationAction.OpenColorPicker.ToString(), SessionEventHelper.Event.StartedAs);
        }

        [TestMethod]
        public void End_after_Start_finalizes_a_nonnegative_duration()
        {
            SessionEventHelper.Start(ColorPickerActivationAction.OpenColorPicker);
            SessionEventHelper.End();

            // End() does not clear Event; it finalizes Duration and writes the telemetry event.
            Assert.IsNotNull(SessionEventHelper.Event);
            Assert.IsTrue(SessionEventHelper.Event.Duration >= 0);
        }
    }
}
