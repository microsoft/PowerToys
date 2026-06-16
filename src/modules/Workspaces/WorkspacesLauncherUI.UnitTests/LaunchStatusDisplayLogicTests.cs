// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Models;

namespace WorkspacesLauncherUI.UnitTests
{
    /// <summary>
    /// Tests for the AppLaunching model which drives UI display:
    /// loading indicator, state glyph, and state color.
    /// These are critical for visual parity after migration.
    /// </summary>
    [TestClass]
    public class LaunchStatusDisplayLogicTests
    {
        [TestMethod]
        [TestCategory("Model")]
        public void LoadingSpinner_WhenStateIsWaiting_IsVisible()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.Waiting };
            Assert.IsTrue(app.Loading);
        }

        [TestMethod]
        [TestCategory("Model")]
        public void LoadingSpinner_WhenStateIsLaunched_RemainsVisibleUntilMoved()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.Launched };
            Assert.IsTrue(app.Loading);
        }

        [TestMethod]
        [TestCategory("Model")]
        public void LoadingSpinner_WhenStateIsLaunchedAndMoved_IsHidden()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.LaunchedAndMoved };
            Assert.IsFalse(app.Loading);
        }

        [TestMethod]
        [TestCategory("Model")]
        public void LoadingSpinner_WhenStateIsFailed_IsHidden()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.Failed };
            Assert.IsFalse(app.Loading);
        }

        [TestMethod]
        [TestCategory("Model")]
        public void LoadingSpinner_WhenStateIsCanceled_IsHidden()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.Canceled };
            Assert.IsFalse(app.Loading);
        }

        [TestMethod]
        [TestCategory("Model")]
        public void StatusIcon_WhenSuccessful_ShowsGreenCheckmarkGlyph()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.LaunchedAndMoved };
            Assert.AreEqual("\U0000F78C", app.StateGlyph, "LaunchedAndMoved should show checkmark glyph");
        }

        [TestMethod]
        [TestCategory("Model")]
        public void StatusIcon_WhenFailed_ShowsRedErrorGlyph()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.Failed };
            Assert.AreEqual("\U0000EF2C", app.StateGlyph, "Failed should show error glyph");
        }

        [TestMethod]
        [TestCategory("Model")]
        public void StatusIcon_WhenCanceled_ShowsRedErrorGlyph()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.Canceled };
            Assert.AreEqual("\U0000EF2C", app.StateGlyph, "Canceled should fall through to default error glyph");
        }

        [TestMethod]
        [TestCategory("Model")]
        public void StatusColor_WhenSuccessful_IsGreenRgb0_128_0()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.LaunchedAndMoved };
            var brush = app.StateColor as System.Windows.Media.SolidColorBrush;

            Assert.IsNotNull(brush);
            Assert.AreEqual(0, brush.Color.R, "Green color R component");
            Assert.AreEqual(128, brush.Color.G, "Green color G component");
            Assert.AreEqual(0, brush.Color.B, "Green color B component");
            Assert.AreEqual(255, brush.Color.A, "Green color A component");
        }

        [TestMethod]
        [TestCategory("Model")]
        public void StatusColor_WhenFailed_IsRedRgb254_0_0()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.Failed };
            var brush = app.StateColor as System.Windows.Media.SolidColorBrush;

            Assert.IsNotNull(brush);
            Assert.AreEqual(254, brush.Color.R, "Red color R component");
            Assert.AreEqual(0, brush.Color.G, "Red color G component");
            Assert.AreEqual(0, brush.Color.B, "Red color B component");
        }

        [TestMethod]
        [TestCategory("Model")]
        public void StatusColor_WhenCanceled_IsRedRgb254_0_0()
        {
            var app = new AppLaunching { LaunchState = LaunchingState.Canceled };
            var brush = app.StateColor as System.Windows.Media.SolidColorBrush;

            Assert.IsNotNull(brush);
            Assert.AreEqual(254, brush.Color.R, "Canceled should fall through to red");
        }

        [TestMethod]
        [TestCategory("Model")]
        public void AppName_SetToString_ReturnsExactValue()
        {
            var app = new AppLaunching { Name = "Test Application" };
            Assert.AreEqual("Test Application", app.Name);
        }

        [TestMethod]
        [TestCategory("Model")]
        public void AppName_SetToEmpty_ReturnsEmptyString()
        {
            var app = new AppLaunching { Name = string.Empty };
            Assert.AreEqual(string.Empty, app.Name);
        }

        [TestMethod]
        [TestCategory("Model")]
        public void DisposeModel_WithActiveState_CompletesCleanly()
        {
            var app = new AppLaunching
            {
                Name = "Test",
                AppPath = @"C:\app.exe",
                LaunchState = LaunchingState.Waiting,
            };
            app.Dispose();
        }

        [TestMethod]
        [TestCategory("Model")]
        public void StateProgression_WaitingToSuccess_TransitionsSpinnerToGreenCheckmark()
        {
            var app = new AppLaunching { Name = "Test", LaunchState = LaunchingState.Waiting };
            Assert.IsTrue(app.Loading);

            app.LaunchState = LaunchingState.Launched;
            Assert.IsTrue(app.Loading);

            app.LaunchState = LaunchingState.LaunchedAndMoved;
            Assert.IsFalse(app.Loading);
            Assert.AreEqual("\U0000F78C", app.StateGlyph);
            var brush = app.StateColor as System.Windows.Media.SolidColorBrush;
            Assert.AreEqual(0, brush.Color.R);
            Assert.AreEqual(128, brush.Color.G);
        }

        [TestMethod]
        [TestCategory("Model")]
        public void StateProgression_WaitingToFailed_TransitionsSpinnerToRedError()
        {
            var app = new AppLaunching { Name = "Test", LaunchState = LaunchingState.Waiting };
            Assert.IsTrue(app.Loading);

            app.LaunchState = LaunchingState.Failed;
            Assert.IsFalse(app.Loading);
            Assert.AreEqual("\U0000EF2C", app.StateGlyph);
            var brush = app.StateColor as System.Windows.Media.SolidColorBrush;
            Assert.AreEqual(254, brush.Color.R);
            Assert.AreEqual(0, brush.Color.G);
        }
    }
}
