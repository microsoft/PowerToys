// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Threading;

using ColorPicker.Common;
using ColorPicker.Mouse;
using ColorPicker.Settings;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Mouse
{
    [TestClass]
    public class MouseInfoProviderTest
    {
        [TestMethod]
        public void Constructor_DoesNotSampleScreen()
        {
            var sampler = new TestScreenSampler();
            sampler.EnqueueSuccess(new System.Windows.Point(10, 20), Color.Red);

            var provider = CreateProvider(sampler);

            Assert.AreEqual(0, sampler.PositionCallCount);
            Assert.AreEqual(0, sampler.ColorSampleCallCount);
            Assert.AreEqual(Color.Transparent, provider.CurrentColor);
            Assert.AreEqual(new System.Windows.Point(-1, 1), provider.CurrentPosition);
        }

        [TestMethod]
        public void TryRefreshSample_TransientScreenCaptureFailuresThenSuccess_Recovers()
        {
            var sampler = new TestScreenSampler();
            var position = new System.Windows.Point(12, 34);
            sampler.EnqueueScreenCaptureFailure(position, 6);
            sampler.EnqueueScreenCaptureFailure(position, 6);
            sampler.EnqueueSuccess(position, Color.Red);
            var provider = CreateProvider(sampler);
            int colorChangeCount = 0;
            int positionChangeCount = 0;
            int unavailableCount = 0;
            provider.MouseColorChanged += (_, _) => colorChangeCount++;
            provider.MousePositionChanged += (_, _) => positionChangeCount++;
            provider.SampleUnavailable += (_, _) => unavailableCount++;

            Assert.IsFalse(provider.TryRefreshSample());
            Assert.IsFalse(provider.TryRefreshSample());
            Assert.AreEqual(0, colorChangeCount);
            Assert.AreEqual(2, positionChangeCount);
            Assert.AreEqual(0, unavailableCount);

            Assert.IsTrue(provider.TryRefreshSample());
            Assert.AreEqual(Color.Red, provider.CurrentColor);
            Assert.AreEqual(position, provider.CurrentPosition);
            Assert.AreEqual(1, colorChangeCount);
            Assert.AreEqual(3, positionChangeCount);
            Assert.AreEqual(0, unavailableCount);
        }

        [TestMethod]
        public void TryRefreshSample_FailureAndRecovery_UpdateSamplingInterval()
        {
            var sampler = new TestScreenSampler();
            var position = new System.Windows.Point(12, 34);
            sampler.EnqueueScreenCaptureFailure(position, 6);
            sampler.EnqueueSuccess(position, Color.Red);
            var provider = CreateProvider(sampler);

            Assert.IsFalse(provider.TryRefreshSample());
            Assert.AreEqual(TimeSpan.FromMilliseconds(250), GetSamplingInterval(provider));

            Assert.IsTrue(provider.TryRefreshSample());
            Assert.AreEqual(TimeSpan.FromMilliseconds(1000.0 / 60.0), GetSamplingInterval(provider));
        }

        [TestMethod]
        public void TryRefreshSample_CursorFailure_DoesNotSampleColorOrRaiseEvents()
        {
            var sampler = new TestScreenSampler();
            sampler.EnqueueCursorFailure(6);
            var provider = CreateProvider(sampler);
            int positionChangeCount = 0;
            int colorChangeCount = 0;
            provider.MousePositionChanged += (_, _) => positionChangeCount++;
            provider.MouseColorChanged += (_, _) => colorChangeCount++;

            Assert.IsFalse(provider.TryRefreshSample());

            Assert.AreEqual(1, sampler.PositionCallCount);
            Assert.AreEqual(0, sampler.ColorSampleCallCount);
            Assert.AreEqual(0, positionChangeCount);
            Assert.AreEqual(0, colorChangeCount);
        }

        [TestMethod]
        public void TryRefreshSample_FailureAfterSuccess_InvalidatesDisplayedSampleOnce()
        {
            var sampler = new TestScreenSampler();
            var position = new System.Windows.Point(40, 50);
            sampler.EnqueueSuccess(position, Color.Blue);
            sampler.EnqueueScreenCaptureFailure(position, 6);
            sampler.EnqueueCursorFailure(6);
            var provider = CreateProvider(sampler);
            string displayedColor = string.Empty;
            int unavailableCount = 0;
            provider.MouseColorChanged += (_, color) => displayedColor = color.Name;
            provider.SampleUnavailable += (_, _) =>
            {
                displayedColor = string.Empty;
                unavailableCount++;
            };

            Assert.IsTrue(provider.TryRefreshSample());
            Assert.AreEqual(Color.Blue.Name, displayedColor);

            Assert.IsFalse(provider.TryRefreshSample());
            Assert.AreEqual(string.Empty, displayedColor);
            Assert.AreEqual(Color.Transparent, provider.CurrentColor);
            Assert.AreEqual(new System.Windows.Point(-1, 1), provider.CurrentPosition);
            Assert.AreEqual(1, unavailableCount);

            Assert.IsFalse(provider.TryRefreshSample());
            Assert.AreEqual(1, unavailableCount);
        }

        [TestMethod]
        public void TryHandlePrimaryMouseDown_SamplingFailure_DoesNotDispatchAction()
        {
            var sampler = new TestScreenSampler();
            sampler.EnqueueCursorFailure(6);
            var provider = CreateProvider(sampler);
            int actionCount = 0;
            provider.OnPrimaryMouseDown += (_, _) => actionCount++;

            Assert.IsFalse(provider.TryHandlePrimaryMouseDown(IntPtr.Zero));

            Assert.AreEqual(0, actionCount);
            Assert.AreEqual(1, sampler.PositionCallCount);
        }

        [TestMethod]
        public void TryHandlePrimaryMouseDown_Success_RefreshesBeforeDispatchingAction()
        {
            var sampler = new TestScreenSampler();
            sampler.EnqueueSuccess(new System.Windows.Point(60, 70), Color.Green);
            var provider = CreateProvider(sampler);
            bool colorUpdated = false;
            bool actionDispatched = false;
            provider.MouseColorChanged += (_, color) => colorUpdated = color == Color.Green;
            provider.OnPrimaryMouseDown += (_, _) =>
            {
                Assert.IsTrue(colorUpdated);
                Assert.AreEqual(Color.Green, provider.CurrentColor);
                actionDispatched = true;
            };

            Assert.IsTrue(provider.TryHandlePrimaryMouseDown(IntPtr.Zero));

            Assert.IsTrue(actionDispatched);
        }

        [TestMethod]
        public void TryHandlePrimaryMouseDown_Close_DoesNotSampleScreen()
        {
            var sampler = new TestScreenSampler();
            var settings = new TestUserSettings(ColorPickerClickAction.Close);
            var provider = CreateProvider(sampler, settings);
            int actionCount = 0;
            provider.OnPrimaryMouseDown += (_, _) => actionCount++;

            Assert.IsTrue(provider.TryHandlePrimaryMouseDown(IntPtr.Zero));

            Assert.AreEqual(1, actionCount);
            Assert.AreEqual(0, sampler.PositionCallCount);
            Assert.AreEqual(0, sampler.ColorSampleCallCount);
        }

        [TestMethod]
        public void TryRefreshSample_ValidSamples_UpdateOnlyChangedState()
        {
            var sampler = new TestScreenSampler();
            var position = new System.Windows.Point(100, 200);
            sampler.EnqueueSuccess(position, Color.Red);
            sampler.EnqueueSuccess(position, Color.Red);
            sampler.EnqueueSuccess(position, Color.Green);
            var provider = CreateProvider(sampler);
            var colors = new List<Color>();
            var positions = new List<System.Windows.Point>();
            provider.MouseColorChanged += (_, color) => colors.Add(color);
            provider.MousePositionChanged += (_, point) => positions.Add(point);

            Assert.IsTrue(provider.TryRefreshSample());
            Assert.IsTrue(provider.TryRefreshSample());
            Assert.IsTrue(provider.TryRefreshSample());

            CollectionAssert.AreEqual(new[] { Color.Red, Color.Green }, colors);
            CollectionAssert.AreEqual(new[] { position }, positions);
        }

        [TestMethod]
        public void TryRefreshSample_RaisesPositionChangedBeforeSamplingColor()
        {
            var initialPosition = new System.Windows.Point(100, 200);
            var expectedPosition = new System.Windows.Point(120, 240);
            var sampler = new TestScreenSampler();
            sampler.EnqueueSuccess(initialPosition, Color.Blue);
            sampler.EnqueueSuccess(expectedPosition, Color.Red);
            var provider = CreateProvider(sampler);
            Assert.IsTrue(provider.TryRefreshSample());

            bool positionChangedRaised = false;
            int colorSampleCountBeforeMove = sampler.ColorSampleCallCount;
            sampler.BeforeColorSample = position =>
            {
                Assert.AreEqual(expectedPosition, position);
                Assert.IsTrue(positionChangedRaised);
            };
            provider.MousePositionChanged += (_, position) =>
            {
                Assert.AreEqual(expectedPosition, position);
                positionChangedRaised = true;
            };

            Assert.IsTrue(provider.TryRefreshSample());
            Assert.IsTrue(positionChangedRaised);
            Assert.AreEqual(colorSampleCountBeforeMove + 1, sampler.ColorSampleCallCount);
        }

        [TestMethod]
        public void TryRefreshSample_NonRecoverableCursorException_IsNotSwallowed()
        {
            var sampler = new TestScreenSampler
            {
                PositionExceptionToThrow = new InvalidOperationException("Non-recoverable cursor exception"),
            };
            var provider = CreateProvider(sampler);

            Assert.ThrowsExactly<InvalidOperationException>(() => provider.TryRefreshSample());
        }

        [TestMethod]
        public void TryRefreshSample_NonRecoverableColorException_IsNotSwallowed()
        {
            var sampler = new TestScreenSampler();
            sampler.EnqueueSuccess(new System.Windows.Point(10, 20), Color.Red);
            sampler.ColorExceptionToThrow = new InvalidOperationException("Non-recoverable color exception");
            var provider = CreateProvider(sampler);

            Assert.ThrowsExactly<InvalidOperationException>(() => provider.TryRefreshSample());
        }

        private static MouseInfoProvider CreateProvider(TestScreenSampler sampler, TestUserSettings? settings = null)
            => new MouseInfoProvider(
                null,
                settings ?? new TestUserSettings(),
                sampler.TryGetCursorPosition,
                sampler.TrySampleColor);

        private static TimeSpan GetSamplingInterval(MouseInfoProvider provider)
        {
            var timerField = typeof(MouseInfoProvider).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic);
            return ((DispatcherTimer)timerField!.GetValue(provider)!).Interval;
        }

        private sealed class TestScreenSampler
        {
            private readonly Queue<(bool Success, System.Windows.Point Position, int ErrorCode)> _cursorResults = new();
            private readonly Queue<(bool Success, System.Windows.Point Position, Color Color, int ErrorCode)> _colorResults = new();

            public int PositionCallCount { get; private set; }

            public int ColorSampleCallCount { get; private set; }

            public Action<System.Windows.Point>? BeforeColorSample { get; set; }

            public Exception? PositionExceptionToThrow { get; set; }

            public Exception? ColorExceptionToThrow { get; set; }

            public void EnqueueSuccess(System.Windows.Point position, Color color)
            {
                _cursorResults.Enqueue((true, position, 0));
                _colorResults.Enqueue((true, position, color, 0));
            }

            public void EnqueueCursorFailure(int nativeErrorCode)
                => _cursorResults.Enqueue((false, default, nativeErrorCode));

            public void EnqueueScreenCaptureFailure(System.Windows.Point position, int nativeErrorCode)
            {
                _cursorResults.Enqueue((true, position, 0));
                _colorResults.Enqueue((false, position, default, nativeErrorCode));
            }

            public bool TryGetCursorPosition(out System.Windows.Point position, out int nativeErrorCode)
            {
                PositionCallCount++;
                if (PositionExceptionToThrow != null)
                {
                    throw PositionExceptionToThrow;
                }

                var result = _cursorResults.Dequeue();
                position = result.Position;
                nativeErrorCode = result.ErrorCode;
                return result.Success;
            }

            public bool TrySampleColor(System.Windows.Point position, out Color color, out int nativeErrorCode)
            {
                ColorSampleCallCount++;
                if (ColorExceptionToThrow != null)
                {
                    throw ColorExceptionToThrow;
                }

                var result = _colorResults.Dequeue();
                Assert.AreEqual(result.Position, position);
                BeforeColorSample?.Invoke(position);

                color = result.Color;
                nativeErrorCode = result.ErrorCode;
                return result.Success;
            }
        }

        private sealed class TestUserSettings : IUserSettings
        {
            public TestUserSettings(ColorPickerClickAction primaryClickAction = ColorPickerClickAction.PickColorThenEditor)
            {
                PrimaryClickAction = new SettingItem<ColorPickerClickAction>(primaryClickAction);
            }

            public SettingItem<string> ActivationShortcut { get; } = new SettingItem<string>(string.Empty);

            public SettingItem<bool> ChangeCursor { get; } = new SettingItem<bool>(false);

            public SettingItem<string> CopiedColorRepresentation { get; set; } = new SettingItem<string>("HEX");

            public SettingItem<string> CopiedColorRepresentationFormat { get; set; } = new SettingItem<string>("%Rex%Grx%Blx");

            public SettingItem<ColorPickerActivationAction> ActivationAction { get; } = new SettingItem<ColorPickerActivationAction>(ColorPickerActivationAction.OpenColorPicker);

            public SettingItem<ColorPickerClickAction> PrimaryClickAction { get; }

            public SettingItem<ColorPickerClickAction> MiddleClickAction { get; } = new SettingItem<ColorPickerClickAction>(ColorPickerClickAction.PickColorAndClose);

            public SettingItem<ColorPickerClickAction> SecondaryClickAction { get; } = new SettingItem<ColorPickerClickAction>(ColorPickerClickAction.Close);

            public RangeObservableCollection<string> ColorHistory { get; } = new RangeObservableCollection<string>();

            public SettingItem<int> ColorHistoryLimit { get; } = new SettingItem<int>(20);

            public ObservableCollection<KeyValuePair<string, string>> VisibleColorFormats { get; } = new ObservableCollection<KeyValuePair<string, string>>();

            public SettingItem<bool> ShowColorName { get; } = new SettingItem<bool>(false);

            public void SendSettingsTelemetry()
            {
            }
        }
    }
}
