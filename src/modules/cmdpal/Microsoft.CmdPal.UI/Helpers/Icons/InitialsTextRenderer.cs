// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Numerics;
using ManagedCommon;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI.Text;
using WinRT;

namespace Microsoft.CmdPal.UI.Helpers;

internal static class InitialsTextRenderer
{
    private const float LayoutExtent = 512;
    private const float NominalFontSize = 100;
    private const float TargetHeight = 18;
    private const float TargetWidth = 23;
    private const float ViewBoxSize = 32;

    // Initials preparation is dispatched to a worker before this is touched. Keep
    // its software device private so font-outline extraction cannot contend with a
    // Win2D device used by XAML on the STA.
    private static Lazy<CanvasDevice> _device = CreateDevice();
    private static int _outlineFailureLogged;

    public static bool TryCreatePathData(
        string text,
        out string pathData,
        out bool useEvenOddFill)
    {
        pathData = string.Empty;
        useEvenOddFill = false;
        var device = Volatile.Read(ref _device);

        try
        {
            using var format = new CanvasTextFormat
            {
                Direction = CanvasTextDirection.LeftToRightThenTopToBottom,
                FontFamily = "Segoe UI",
                FontSize = NominalFontSize,
                FontWeight = new FontWeight { Weight = 600 },
                LocaleName = CultureInfo.CurrentUICulture.Name,
                WordWrapping = CanvasWordWrapping.NoWrap,
            };

            if (!HasFontForEveryCharacter(text, format))
            {
                return false;
            }

            using var layout = new CanvasTextLayout(
                device.Value,
                text,
                format,
                LayoutExtent,
                LayoutExtent);
            using var geometry = CanvasGeometry.CreateText(layout);
            var bounds = geometry.ComputeBounds();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return false;
            }

            var scale = MathF.Min(
                TargetWidth / (float)bounds.Width,
                TargetHeight / (float)bounds.Height);
            var width = (float)bounds.Width * scale;
            var height = (float)bounds.Height * scale;
            var transform = Matrix3x2.CreateTranslation(-(float)bounds.X, -(float)bounds.Y)
                * Matrix3x2.CreateScale(scale)
                * Matrix3x2.CreateTranslation(
                    (ViewBoxSize - width) / 2,
                    (ViewBoxSize - height) / 2);

            using var transformed = geometry.Transform(transform);
            var receiver = new SvgPathDataReceiver();
            transformed.SendPathTo(receiver);
            pathData = receiver.PathData;
            useEvenOddFill = receiver.UseEvenOddFill;
            return pathData.Length > 0;
        }
        catch (Exception ex)
        {
            var failure = ex;
            try
            {
                ResetDeviceIfLost(device, ex.HResult);
            }
            catch (Exception resetFailure)
            {
                failure = new AggregateException(
                    "Failed while checking whether the initials rendering device was lost.",
                    ex,
                    resetFailure);
            }

            if (Interlocked.Exchange(ref _outlineFailureLogged, 1) == 0)
            {
                Logger.LogError("Initials outline extraction failed; falling back to background tile", failure);
            }

            pathData = string.Empty;
            useEvenOddFill = false;
            return false;
        }
    }

    private static Lazy<CanvasDevice> CreateDevice() =>
        new(
            static () => new CanvasDevice(forceSoftwareRenderer: true) { LowPriority = true },

            // PublicationOnly deliberately retries transient CanvasDevice factory failures.
            // Concurrent losing devices use GC-based release, matching the lock-free lifetime
            // policy used when replacing a lost device.
            LazyThreadSafetyMode.PublicationOnly);

    private static void ResetDeviceIfLost(Lazy<CanvasDevice> device, int failureHResult)
    {
        if (device.IsValueCreated)
        {
            var canvasDevice = device.Value;
            if (canvasDevice.IsDeviceLost(failureHResult) || canvasDevice.IsDeviceLost())
            {
                // Do not dispose the lost device here: another worker may still be
                // unwinding a call through it. Once those calls finish, replacing
                // the Lazy releases the last long-lived reference without a lock.
                Interlocked.CompareExchange(ref _device, CreateDevice(), device);
            }
        }
    }

    private static bool HasFontForEveryCharacter(string text, CanvasTextFormat format)
    {
        var analyzer = new CanvasTextAnalyzer(
            text,
            CanvasTextDirection.LeftToRightThenTopToBottom);
        try
        {
            var mappings = analyzer.GetFonts(format);
            Span<bool> covered = stackalloc bool[text.Length];

            foreach (var mapping in mappings)
            {
                var range = mapping.Key;
                if (range.CharacterIndex < 0
                    || range.CharacterCount <= 0
                    || range.CharacterIndex > text.Length - range.CharacterCount)
                {
                    return false;
                }

                covered.Slice(range.CharacterIndex, range.CharacterCount).Fill(true);
            }

            foreach (var isCovered in covered)
            {
                if (!isCovered)
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            // CanvasTextAnalyzer's projected IDisposable currently queries an IID
            // the Win2D runtime object does not expose. Releasing its owned object
            // reference avoids both that InvalidCastException and finalizer-delayed
            // retention of the analyzer's native state.
            ((IWinRTObject)analyzer).NativeObject.Dispose();
        }
    }
}
