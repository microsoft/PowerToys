// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using PowerOCR.Helpers;
using PowerOCR.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace PowerOCR;

internal sealed class ImageMethods
{
    internal static Bitmap PadImage(Bitmap image, int minW = 64, int minH = 64)
    {
        if (image.Height >= minH && image.Width >= minW)
        {
            return image;
        }

        int width = Math.Max(image.Width + 16, minW + 16);
        int height = Math.Max(image.Height + 16, minH + 16);

        // Create a compatible bitmap
        Bitmap destination = new(width, height, image.PixelFormat);
        using Graphics gd = Graphics.FromImage(destination);

        gd.Clear(image.GetPixel(0, 0));
        gd.DrawImageUnscaled(image, 8, 8);

        return destination;
    }

    internal static ImageSource GetWindowBoundsImage(OCROverlay passedWindow)
    {
        Rectangle screenRectangle = passedWindow.GetScreenRectangle();
        using Bitmap bmp = new(screenRectangle.Width, screenRectangle.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(screenRectangle.Left, screenRectangle.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return BitmapToImageSource(bmp);
    }

    internal static Bitmap GetRegionAsBitmap(OCROverlay passedWindow, Rectangle selectedRegion)
    {
        Bitmap bmp = new(
            selectedRegion.Width,
            selectedRegion.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using Graphics g = Graphics.FromImage(bmp);
        Rectangle screenRectangle = passedWindow.GetScreenRectangle();

        g.CopyFromScreen(
            screenRectangle.Left + selectedRegion.Left,
            screenRectangle.Top + selectedRegion.Top,
            0,
            0,
            bmp.Size,
            CopyPixelOperation.SourceCopy);

        bmp = PadImage(bmp);
        return bmp;
    }

    internal static async Task<string> GetRegionsText(OCROverlay? passedWindow, Rectangle selectedRegion, Language? preferredLanguage)
    {
        if (passedWindow is null)
        {
            return string.Empty;
        }

        Bitmap bmp = GetRegionAsBitmap(passedWindow, selectedRegion);
        string? resultText = await ExtractText(bmp, preferredLanguage);

        return resultText != null ? resultText.Trim() : string.Empty;
    }

    internal static async Task<string> GetClickedWord(OCROverlay passedWindow, System.Windows.Point clickedPoint, Language? preferredLanguage)
    {
        Rectangle screenRectangle = passedWindow.GetScreenRectangle();
        Bitmap bmp = new((int)screenRectangle.Width, (int)passedWindow.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(bmp);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();

        g.CopyFromScreen((int)absPosPoint.X, (int)absPosPoint.Y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        System.Windows.Point adjustedPoint = new(clickedPoint.X, clickedPoint.Y);

        string resultText = await ExtractText(bmp, preferredLanguage, adjustedPoint);
        return resultText.Trim();
    }

    internal static readonly char[] Separator = new char[] { '\n', '\r' };

    public static async Task<string> ExtractText(Bitmap bmp, Language? preferredLanguage, System.Windows.Point? singlePoint = null)
    {
        Logger.LogInfo($"ExtractText called");
        Language? selectedLanguage = preferredLanguage ?? GetOCRLanguage();
        if (selectedLanguage == null)
        {
            return string.Empty;
        }

        // Attempt AI backend first if enabled & usable and not a single-point extraction (keep single word quick path legacy for now)
        bool aiTried = false;
        try
        {
            if (singlePoint == null)
            {
                var userSettings = new Settings.UserSettings(new ThrottledActionInvoker());
                Logger.LogInfo($"AI OCR setting={userSettings.UseAITextRecognition.Value} pre_init_usable={Helpers.AiTextRecognizer.Instance.IsUsable}");
                if (userSettings.UseAITextRecognition.Value)
                {
                    aiTried = true;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    string aiText = await Helpers.AiTextRecognizer.Instance.RecognizeAsync(bmp, selectedLanguage, singlePoint != null, System.Threading.CancellationToken.None);
                    sw.Stop();
                    PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRAIInvokedEvent
                    {
                        Backend = Helpers.AiTextRecognizer.Instance.Name,
                        DurationMs = (int)sw.ElapsedMilliseconds,
                        Success = !string.IsNullOrWhiteSpace(aiText),
                    });
                    if (!string.IsNullOrWhiteSpace(aiText))
                    {
                        return aiText;
                    }
                    else
                    {
                        PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRAIFallbackEvent { Reason = "EmptyResult" });
                    }
                }
                else
                {
                    Logger.LogDebug("AI OCR disabled by setting; skipping AI path");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("AI backend failed, falling back to legacy", ex);
            if (aiTried)
            {
                PowerToysTelemetry.Log.WriteEvent(new PowerOCR.Telemetry.PowerOCRAIFallbackEvent { Reason = "Exception" });
            }
        }

        XmlLanguage lang = XmlLanguage.GetLanguage(selectedLanguage.LanguageTag);
        CultureInfo culture = lang.GetEquivalentCulture();

        bool isSpaceJoiningLang = PowerOCR.Helpers.LanguageHelper.IsLanguageSpaceJoining(selectedLanguage);

        bool scaleBMP = true;

        if (singlePoint != null
            || bmp.Width * 1.5 > OcrEngine.MaxImageDimension)
        {
            scaleBMP = false;
        }

        using Bitmap scaledBitmap = scaleBMP ? ScaleBitmapUniform(bmp, 1.5) : ScaleBitmapUniform(bmp, 1.0);
        StringBuilder text = new();

        await using MemoryStream memoryStream = new();
        using WrappingStream wrappingStream = new(memoryStream);

        scaledBitmap.Save(wrappingStream, ImageFormat.Bmp);
        wrappingStream.Position = 0;
        BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(wrappingStream.AsRandomAccessStream());
        SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

        OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(selectedLanguage);
        OcrResult ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

        await memoryStream.DisposeAsync();
        await wrappingStream.DisposeAsync();
        GC.Collect();

        if (singlePoint == null)
        {
            foreach (OcrLine ocrLine in ocrResult.Lines)
            {
                ocrLine.GetTextFromOcrLine(isSpaceJoiningLang, text);
            }
        }
        else
        {
            Windows.Foundation.Point fPoint = new(singlePoint.Value.X, singlePoint.Value.Y);
            foreach (OcrLine ocrLine in ocrResult.Lines)
            {
                foreach (OcrWord ocrWord in ocrLine.Words)
                {
                    if (ocrWord.BoundingRect.Contains(fPoint))
                    {
                        _ = text.Append(ocrWord.Text);
                    }
                }
            }
        }

        if (culture.TextInfo.IsRightToLeft)
        {
            string[] textListLines = text.ToString().Split(Separator);

            _ = text.Clear();
            foreach (string textLine in textListLines)
            {
                List<string> wordArray = textLine.Split().ToList();
                wordArray.Reverse();
                _ = text.Append(string.Join(' ', wordArray));

                if (textLine.Length > 0)
                {
                    _ = text.Append('\n');
                }
            }

            return text.ToString();
        }

        return text.ToString();
    }

    public static Bitmap ScaleBitmapUniform(Bitmap passedBitmap, double scale)
    {
        using MemoryStream memoryStream = new();
        using WrappingStream wrappingStream = new(memoryStream);
        passedBitmap.Save(wrappingStream, ImageFormat.Bmp);
        wrappingStream.Position = 0;
        BitmapImage bitmapImage = new();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = wrappingStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        TransformedBitmap transformedBmp = new();
        transformedBmp.BeginInit();
        transformedBmp.Source = bitmapImage;
        transformedBmp.Transform = new ScaleTransform(scale, scale);
        transformedBmp.EndInit();
        transformedBmp.Freeze();

        memoryStream.Dispose();
        wrappingStream.Dispose();
        GC.Collect();
        return BitmapSourceToBitmap(transformedBmp);
    }

    public static Bitmap BitmapSourceToBitmap(BitmapSource source)
    {
        Bitmap bmp = new(
          source.PixelWidth,
          source.PixelHeight,
          System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        BitmapData data = bmp.LockBits(
          new Rectangle(System.Drawing.Point.Empty, bmp.Size),
          ImageLockMode.WriteOnly,
          System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        source.CopyPixels(
          Int32Rect.Empty,
          data.Scan0,
          data.Height * data.Stride,
          data.Stride);
        bmp.UnlockBits(data);
        GC.Collect();
        return bmp;
    }

    internal static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using MemoryStream memoryStream = new();
        using WrappingStream wrappingStream = new(memoryStream);

        bitmap.Save(wrappingStream, ImageFormat.Bmp);
        wrappingStream.Position = 0;
        BitmapImage bitmapImage = new();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = wrappingStream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        memoryStream.Dispose();
        wrappingStream.Dispose();
        GC.Collect();
        return bitmapImage;
    }

    public static Language? GetOCRLanguage()
    {
        // use currently selected Language
        string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;

        Language? selectedLanguage = new(inputLang);
        List<Language> possibleOcrLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();

        if (possibleOcrLanguages.Count < 1)
        {
            MessageBox.Show("No possible OCR languages are installed.", "Text Grab");
            return null;
        }

        if (possibleOcrLanguages.All(l => l.LanguageTag != selectedLanguage.LanguageTag))
        {
            List<Language>? similarLanguages = possibleOcrLanguages.Where(
                la => la.AbbreviatedName == selectedLanguage.AbbreviatedName).ToList();

            if (similarLanguages != null)
            {
                selectedLanguage = similarLanguages.Count > 0
                    ? similarLanguages.FirstOrDefault()
                    : possibleOcrLanguages.FirstOrDefault();
            }
        }

        return selectedLanguage;
    }
}
