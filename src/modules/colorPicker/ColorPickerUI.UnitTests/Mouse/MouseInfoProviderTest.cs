// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;

using ColorPicker.Common;
using ColorPicker.Mouse;
using ColorPicker.Settings;
using ColorPicker.ViewModels;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Mouse
{
    [TestClass]
    public class MouseInfoProviderTest
    {
        private static readonly bool[] ValidSampleSequence = [true];
        private static readonly bool[] ValidThenInvalidSampleSequence = [true, false];

        [TestMethod]
        public void Constructor_DoesNotSampleScreen()
        {
            var sampler = new TestScreenColorSampler();
            sampler.EnqueueSuccess(new System.Windows.Point(10, 20), Color.Red);

            var provider = CreateProvider(sampler);

            Assert.AreEqual(0, sampler.CallCount);
            Assert.IsFalse(provider.HasValidSample);
            Assert.AreEqual(Color.Transparent, provider.CurrentColor);
            Assert.AreEqual(new System.Windows.Point(-1, 1), provider.CurrentPosition);
        }

        [TestMethod]
        public void TryUpdateMouseInfo_TransientWin32FailuresThenSuccess_Recovers()
        {
            var sampler = new TestScreenColorSampler();
            sampler.EnqueueWin32Failure(6);
            sampler.EnqueueWin32Failure(6);
            sampler.EnqueueSuccess(new System.Windows.Point(12, 34), Color.Red);
            var provider = CreateProvider(sampler);
            int colorChangeCount = 0;
            int positionChangeCount = 0;
            var validityChanges = new List<bool>();
            provider.MouseColorChanged += (_, _) => colorChangeCount++;
            provider.MousePositionChanged += (_, _) => positionChangeCount++;
            provider.SampleValidityChanged += (_, isValid) => validityChanges.Add(isValid);

            Assert.IsFalse(provider.TryUpdateMouseInfo());
            Assert.IsFalse(provider.TryUpdateMouseInfo());
            Assert.IsFalse(provider.HasValidSample);
            Assert.AreEqual(0, colorChangeCount);
            Assert.AreEqual(0, positionChangeCount);

            Assert.IsTrue(provider.TryUpdateMouseInfo());
            Assert.IsTrue(provider.HasValidSample);
            Assert.AreEqual(Color.Red, provider.CurrentColor);
            Assert.AreEqual(new System.Windows.Point(12, 34), provider.CurrentPosition);
            Assert.AreEqual(1, colorChangeCount);
            Assert.AreEqual(1, positionChangeCount);
            CollectionAssert.AreEqual(ValidSampleSequence, validityChanges);
        }

        [TestMethod]
        public void TryHandlePrimaryMouseDown_InvalidSample_DoesNotConfirmOrExposeLastSample()
        {
            var sampler = new TestScreenColorSampler();
            var initialPosition = new System.Windows.Point(40, 50);
            sampler.EnqueueSuccess(initialPosition, Color.Blue);
            sampler.EnqueueWin32Failure(6);
            var provider = CreateProvider(sampler);
            int confirmationCount = 0;
            provider.OnPrimaryMouseDown += (_, _) => confirmationCount++;

            Assert.IsTrue(provider.TryUpdateMouseInfo());
            Assert.IsFalse(provider.TryHandlePrimaryMouseDown(IntPtr.Zero));

            Assert.IsFalse(provider.HasValidSample);
            Assert.AreEqual(Color.Transparent, provider.CurrentColor);
            Assert.AreEqual(new System.Windows.Point(-1, 1), provider.CurrentPosition);
            Assert.AreEqual(0, confirmationCount);
        }

        [TestMethod]
        public void TryHandlePrimaryMouseDown_ValidSample_IsAcceptedByMainViewModelDuringDispatch()
        {
            var sampler = new TestScreenColorSampler();
            sampler.EnqueueSuccess(new System.Windows.Point(60, 70), Color.Green);
            var provider = CreateProvider(sampler);
            bool eventRaised = false;
            bool actionAccepted = false;
            var validityChanges = new List<bool>();
            provider.SampleValidityChanged += (_, isValid) => validityChanges.Add(isValid);
            provider.OnPrimaryMouseDown += (_, _) =>
            {
                eventRaised = true;
                actionAccepted = MainViewModel.CanHandleMouseClickAction(
                    ColorPickerClickAction.PickColorThenEditor,
                    provider);
                Assert.AreEqual(Color.Green, provider.CurrentColor);
            };

            Assert.IsTrue(provider.TryHandlePrimaryMouseDown(IntPtr.Zero));

            Assert.IsTrue(eventRaised);
            Assert.IsTrue(actionAccepted);
            Assert.IsFalse(provider.HasValidSample);
            Assert.AreEqual(Color.Transparent, provider.CurrentColor);
            CollectionAssert.AreEqual(ValidThenInvalidSampleSequence, validityChanges);
        }

        [TestMethod]
        public void TryUpdateMouseInfo_FailureInvalidatesCachedViewAndPositionState()
        {
            var sampler = new TestScreenColorSampler();
            sampler.EnqueueSuccess(new System.Windows.Point(80, 90), Color.Blue);
            sampler.EnqueueWin32Failure(6);
            var provider = CreateProvider(sampler);
            string displayedColor = string.Empty;
            bool hasCachedPosition = false;
            provider.MouseColorChanged += (_, color) => displayedColor = color.Name;
            provider.MousePositionChanged += (_, _) => hasCachedPosition = true;
            provider.SampleValidityChanged += (_, isValid) =>
            {
                if (!isValid)
                {
                    displayedColor = string.Empty;
                    hasCachedPosition = false;
                }
            };

            Assert.IsTrue(provider.TryUpdateMouseInfo());
            Assert.AreEqual(Color.Blue.Name, displayedColor);
            Assert.IsTrue(hasCachedPosition);

            Assert.IsFalse(provider.TryUpdateMouseInfo());
            Assert.AreEqual(string.Empty, displayedColor);
            Assert.IsFalse(hasCachedPosition);
            Assert.IsFalse(provider.HasValidSample);
            Assert.AreEqual(new System.Windows.Point(-1, 1), provider.CurrentPosition);
        }

        [TestMethod]
        public void TryUpdateMouseInfo_ValidSamples_UpdateOnlyChangedState()
        {
            var sampler = new TestScreenColorSampler();
            var position = new System.Windows.Point(100, 200);
            sampler.EnqueueSuccess(position, Color.Red);
            sampler.EnqueueSuccess(position, Color.Red);
            sampler.EnqueueSuccess(position, Color.Green);
            var provider = CreateProvider(sampler);
            var colors = new List<Color>();
            var positions = new List<System.Windows.Point>();
            provider.MouseColorChanged += (_, color) => colors.Add(color);
            provider.MousePositionChanged += (_, point) => positions.Add(point);

            Assert.IsTrue(provider.TryUpdateMouseInfo());
            Assert.IsTrue(provider.TryUpdateMouseInfo());
            Assert.IsTrue(provider.TryUpdateMouseInfo());

            CollectionAssert.AreEqual(new[] { Color.Red, Color.Green }, colors);
            CollectionAssert.AreEqual(new[] { position }, positions);
        }

        [TestMethod]
        public void TryUpdateMouseInfo_NonRecoverableSamplerException_IsNotSwallowed()
        {
            var sampler = new TestScreenColorSampler
            {
                ExceptionToThrow = new InvalidOperationException("Non-recoverable test exception"),
            };
            var provider = CreateProvider(sampler);

            Assert.ThrowsExactly<InvalidOperationException>(() => provider.TryUpdateMouseInfo());
        }

        private static MouseInfoProvider CreateProvider(IScreenColorSampler sampler)
            => new MouseInfoProvider(null, new TestUserSettings(), sampler);

        private sealed class TestScreenColorSampler : IScreenColorSampler
        {
            private readonly Queue<SampleResult> _results = new Queue<SampleResult>();

            public int CallCount { get; private set; }

            public Exception? ExceptionToThrow { get; set; }

            public void EnqueueSuccess(System.Windows.Point position, Color color)
                => _results.Enqueue(SampleResult.Success(position, color));

            public void EnqueueWin32Failure(int nativeErrorCode)
                => _results.Enqueue(SampleResult.Failure(nativeErrorCode));

            public bool TrySample(out ScreenColorSample sample, out ScreenColorSamplingFailure failure)
            {
                CallCount++;
                if (ExceptionToThrow != null)
                {
                    throw ExceptionToThrow;
                }

                SampleResult result = _results.Dequeue();
                sample = result.Sample;
                failure = result.SamplingFailure;
                return result.Succeeded;
            }
        }

        private readonly struct SampleResult
        {
            private SampleResult(bool succeeded, ScreenColorSample sample, ScreenColorSamplingFailure samplingFailure)
            {
                Succeeded = succeeded;
                Sample = sample;
                SamplingFailure = samplingFailure;
            }

            public bool Succeeded { get; }

            public ScreenColorSample Sample { get; }

            public ScreenColorSamplingFailure SamplingFailure { get; }

            public static SampleResult Success(System.Windows.Point position, Color color)
                => new SampleResult(true, new ScreenColorSample(position, color), default);

            public static SampleResult Failure(int nativeErrorCode)
                => new SampleResult(
                    false,
                    default,
                    new ScreenColorSamplingFailure(
                        ScreenColorSamplingFailureReason.ScreenCaptureFailed,
                        nativeErrorCode,
                        "The handle is invalid."));
        }

        private sealed class TestUserSettings : IUserSettings
        {
            public SettingItem<string> ActivationShortcut { get; } = new SettingItem<string>(string.Empty);

            public SettingItem<bool> ChangeCursor { get; } = new SettingItem<bool>(false);

            public SettingItem<string> CopiedColorRepresentation { get; set; } = new SettingItem<string>("HEX");

            public SettingItem<string> CopiedColorRepresentationFormat { get; set; } = new SettingItem<string>("%Rex%Grx%Blx");

            public SettingItem<ColorPickerActivationAction> ActivationAction { get; } = new SettingItem<ColorPickerActivationAction>(ColorPickerActivationAction.OpenColorPicker);

            public SettingItem<ColorPickerClickAction> PrimaryClickAction { get; } = new SettingItem<ColorPickerClickAction>(ColorPickerClickAction.PickColorThenEditor);

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
