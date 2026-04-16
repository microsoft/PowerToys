// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Media.Imaging;
using PowerOCR.Helpers;
using PowerOCR.Models;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using Point = Windows.Foundation.Point;

namespace PowerOCR;

internal sealed class ImageMethods
{
    internal static bool PadImage(Bitmap image, [NotNullWhen(true)] out Bitmap? paddedBitmap, int minW = 64, int minH = 64)
    {
        if (image.Height >= minH && image.Width >= minW)
        {
            paddedBitmap = null;
            return false;
        }

        int width = Math.Max(image.Width + 16, minW + 16);
        int height = Math.Max(image.Height + 16, minH + 16);

        // Create a compatible bitmap
        Bitmap destination = new(width, height, image.PixelFormat);
        using Graphics gd = Graphics.FromImage(destination);

        gd.Clear(image.GetPixel(0, 0));
        gd.DrawImageUnscaled(image, 8, 8);
        paddedBitmap = destination;

        return true;
    }

    internal static async Task<BitmapImage> GetWindowBoundsImageAsync(OCROverlay passedWindow)
    {
        Rectangle screenRectangle = passedWindow.GetScreenRectangle();
        using Bitmap bmp = new(screenRectangle.Width, screenRectangle.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(screenRectangle.Left, screenRectangle.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return await BitmapToImageSourceAsync(bmp);
    }

    internal static BitmapImage GetWindowBoundsImage(OCROverlay passedWindow)
    {
        Rectangle screenRectangle = passedWindow.GetScreenRectangle();
        using Bitmap bmp = new(screenRectangle.Width, screenRectangle.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(screenRectangle.Left, screenRectangle.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return BitmapToImageSourceSync(bmp);
    }

    internal static Bitmap GetRegionAsBitmap(OCROverlay passedWindow, Rectangle selectedRegion)
    {
        Bitmap bmp = new(
            selectedRegion.Width,
            selectedRegion.Height,
            PixelFormat.Format32bppArgb);

        using Graphics g = Graphics.FromImage(bmp);
        Rectangle screenRectangle = passedWindow.GetScreenRectangle();

        g.CopyFromScreen(
            screenRectangle.Left + selectedRegion.Left,
            screenRectangle.Top + selectedRegion.Top,
            0,
            0,
            bmp.Size,
            CopyPixelOperation.SourceCopy);

        if (PadImage(bmp, out var paddedBmp))
        {
            bmp.Dispose();
            return paddedBmp;
        }
        else
        {
            return bmp;
        }
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

    internal static async Task<string> GetClickedWord(OCROverlay passedWindow, Point clickedPoint, Language? preferredLanguage)
    {
        Rectangle screenRectangle = passedWindow.GetScreenRectangle();
        Bitmap bmp = new(screenRectangle.Width, screenRectangle.Height, PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(screenRectangle.Left, screenRectangle.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        Point adjustedPoint = new(clickedPoint.X, clickedPoint.Y);

        string resultText = await ExtractText(bmp, preferredLanguage, adjustedPoint);
        return resultText.Trim();
    }

    internal static readonly char[] Separator = new char[] { '\n', '\r' };

    public static async Task<string> ExtractText(Bitmap bmp, Language? preferredLanguage, Point? singlePoint = null)
    {
        Language? selectedLanguage = preferredLanguage ?? GetOCRLanguage();
        if (selectedLanguage == null)
        {
            return string.Empty;
        }

        CultureInfo culture = new(selectedLanguage.LanguageTag);

        bool isSpaceJoiningLang = LanguageHelper.IsLanguageSpaceJoining(selectedLanguage);

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
        int newWidth = (int)(passedBitmap.Width * scale);
        int newHeight = (int)(passedBitmap.Height * scale);
        Bitmap scaled = new(newWidth, newHeight, passedBitmap.PixelFormat);
        using Graphics g = Graphics.FromImage(scaled);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(passedBitmap, 0, 0, newWidth, newHeight);
        return scaled;
    }

    internal static BitmapImage BitmapToImageSourceSync(Bitmap bitmap)
    {
        using MemoryStream ms = new();
        bitmap.Save(ms, ImageFormat.Bmp);
        ms.Position = 0;

        var bitmapImage = new BitmapImage();
        var stream = ms.AsRandomAccessStream();
        bitmapImage.SetSourceAsync(stream).AsTask().GetAwaiter().GetResult();
        return bitmapImage;
    }

    internal static async Task<BitmapImage> BitmapToImageSourceAsync(Bitmap bitmap)
    {
        using MemoryStream ms = new();
        bitmap.Save(ms, ImageFormat.Bmp);
        ms.Position = 0;

        var bitmapImage = new BitmapImage();
        var stream = ms.AsRandomAccessStream();
        await bitmapImage.SetSourceAsync(stream);
        return bitmapImage;
    }

    public static Language? GetOCRLanguage()
    {
        // Use current input language from Windows.Globalization
        string inputLang = Windows.Globalization.Language.CurrentInputMethodLanguageTag;

        Language? selectedLanguage = new(inputLang);
        List<Language> possibleOcrLanguages = OcrEngine.AvailableRecognizerLanguages.ToList();

        if (possibleOcrLanguages.Count < 1)
        {
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
