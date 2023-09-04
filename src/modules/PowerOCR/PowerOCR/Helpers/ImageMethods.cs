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

    internal static ImageSource GetWindowBoundsImage(Window passedWindow)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        int windowWidth = (int)(passedWindow.ActualWidth * dpi.DpiScaleX);
        int windowHeight = (int)(passedWindow.ActualHeight * dpi.DpiScaleY);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();
        int thisCorrectedLeft = (int)absPosPoint.X;
        int thisCorrectedTop = (int)absPosPoint.Y;

        using Bitmap bmp = new(windowWidth, windowHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return BitmapToImageSource(bmp);
    }

    internal static Bitmap GetWindowBoundsBitmap(Window passedWindow)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        int windowWidth = (int)(passedWindow.ActualWidth * dpi.DpiScaleX);
        int windowHeight = (int)(passedWindow.ActualHeight * dpi.DpiScaleY);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();
        int thisCorrectedLeft = (int)absPosPoint.X;
        int thisCorrectedTop = (int)absPosPoint.Y;

        Bitmap bmp = new(
            windowWidth,
            windowHeight,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(
            thisCorrectedLeft,
            thisCorrectedTop,
            0,
            0,
            bmp.Size,
            CopyPixelOperation.SourceCopy);

        return bmp;
    }

    internal static Bitmap GetRegionAsBitmap(Window passedWindow, Rectangle selectedRegion)
    {
        Bitmap bmp = new(
            selectedRegion.Width,
            selectedRegion.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using Graphics g = Graphics.FromImage(bmp);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        g.CopyFromScreen(
            thisCorrectedLeft,
            thisCorrectedTop,
            0,
            0,
            bmp.Size,
            CopyPixelOperation.SourceCopy);

        bmp = PadImage(bmp);
        return bmp;
    }

    internal static async Task<string> GetRegionsText(Window? passedWindow, Rectangle selectedRegion, Language? preferredLanguage)
    {
        if (passedWindow is null)
        {
            return string.Empty;
        }

        Bitmap bmp = GetRegionAsBitmap(passedWindow, selectedRegion);
        string? resultText = await ExtractText(bmp, preferredLanguage);

        return resultText != null ? resultText.Trim() : string.Empty;
    }

    internal static async Task<string> GetClickedWord(Window passedWindow, System.Windows.Point clickedPoint, Language? preferredLanguage)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        Bitmap bmp = new((int)(passedWindow.ActualWidth * dpi.DpiScaleX), (int)(passedWindow.ActualHeight * dpi.DpiScaleY), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(bmp);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();
        int thisCorrectedLeft = (int)absPosPoint.X;
        int thisCorrectedTop = (int)absPosPoint.Y;

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        System.Windows.Point adjustedPoint = new(clickedPoint.X, clickedPoint.Y);

        string resultText = await ExtractText(bmp, preferredLanguage, adjustedPoint);
        return resultText.Trim();
    }

    public static async Task<string> ExtractText(Bitmap bmp, Language? preferredLanguage, System.Windows.Point? singlePoint = null)
    {
        Language? selectedLanguage = preferredLanguage ?? GetOCRLanguage();
        if (selectedLanguage == null)
        {
            return string.Empty;
        }

        XmlLanguage lang = XmlLanguage.GetLanguage(selectedLanguage.LanguageTag);
        CultureInfo culture = lang.GetEquivalentCulture();

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
            string[] textListLines = text.ToString().Split(new char[] { '\n', '\r' });

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
