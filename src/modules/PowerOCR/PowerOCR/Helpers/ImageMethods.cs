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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

namespace PowerOCR;

internal class ImageMethods
{
    internal static ImageSource GetWindowBoundsImage(Window passedWindow)
    {
        bool isGrabFrame = false;

        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        int windowWidth = (int)(passedWindow.ActualWidth * dpi.DpiScaleX);
        int windowHeight = (int)(passedWindow.ActualHeight * dpi.DpiScaleY);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();
        int thisCorrectedLeft = (int)absPosPoint.X;
        int thisCorrectedTop = (int)absPosPoint.Y;

        if (isGrabFrame == true)
        {
            thisCorrectedLeft += (int)(2 * dpi.DpiScaleX);
            thisCorrectedTop += (int)(26 * dpi.DpiScaleY);
            windowWidth -= (int)(4 * dpi.DpiScaleX);
            windowHeight -= (int)(70 * dpi.DpiScaleY);
        }

        using Bitmap bmp = new Bitmap(windowWidth, windowHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return BitmapToImageSource(bmp);
    }

    internal static async Task<string> GetRegionsText(Window? passedWindow, Rectangle selectedRegion, Language? preferredLanguage)
    {
        using Bitmap bmp = new Bitmap(selectedRegion.Width, selectedRegion.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(bmp);

        System.Windows.Point absPosPoint = passedWindow == null ? default(System.Windows.Point) : passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        // bmp = PadImage(bmp);
        string? resultText = await ExtractText(bmp, preferredLanguage);

        return resultText != null ? resultText.Trim() : string.Empty;
    }

    internal static async Task<string> GetClickedWord(Window passedWindow, System.Windows.Point clickedPoint, Language? preferredLanguage)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        Bitmap bmp = new Bitmap((int)(passedWindow.ActualWidth * dpi.DpiScaleX), (int)(passedWindow.ActualHeight * dpi.DpiScaleY), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(bmp);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();
        int thisCorrectedLeft = (int)absPosPoint.X;
        int thisCorrectedTop = (int)absPosPoint.Y;

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        System.Windows.Point adjustedPoint = new System.Windows.Point(clickedPoint.X, clickedPoint.Y);

        string resultText = await ExtractText(bmp, preferredLanguage, adjustedPoint);
        return resultText.Trim();
    }

    public static async Task<string> ExtractText(Bitmap bmp, Language? preferredLanguage, System.Windows.Point? singlePoint = null)
    {
        Language? selectedLanguage = preferredLanguage;
        if (selectedLanguage == null)
        {
            selectedLanguage = GetOCRLanguage();
        }

        if (selectedLanguage == null)
        {
            return string.Empty;
        }

        bool isCJKLang = false;

        if (selectedLanguage.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase) == true)
        {
            isCJKLang = true;
        }
        else if (selectedLanguage.LanguageTag.StartsWith("ja", StringComparison.InvariantCultureIgnoreCase) == true)
        {
            isCJKLang = true;
        }
        else if (selectedLanguage.LanguageTag.StartsWith("ko", StringComparison.InvariantCultureIgnoreCase) == true)
        {
            isCJKLang = true;
        }

        XmlLanguage lang = XmlLanguage.GetLanguage(selectedLanguage.LanguageTag);
        CultureInfo culture = lang.GetEquivalentCulture();

        bool scaleBMP = true;

        if (singlePoint != null
            || bmp.Width * 1.5 > OcrEngine.MaxImageDimension)
        {
            scaleBMP = false;
        }

        using Bitmap scaledBitmap = scaleBMP ? ScaleBitmapUniform(bmp, 1.5) : ScaleBitmapUniform(bmp, 1.0);
        StringBuilder text = new StringBuilder();

        await using (MemoryStream memory = new MemoryStream())
        {
            scaledBitmap.Save(memory, ImageFormat.Bmp);
            memory.Position = 0;
            BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
            SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

            OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(selectedLanguage);
            OcrResult ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

            if (singlePoint == null)
            {
                if (isCJKLang == false)
                {
                    foreach (OcrLine line in ocrResult.Lines)
                    {
                        text.AppendLine(line.Text);
                    }
                }
                else
                {
                    // Kanji, Hiragana, Katakana, Hankaku-Katakana do not need blank.(not only the symbol in CJKUnifiedIdeographs).
                    // Maybe there are more symbols that don't require spaces like \u3001 \u3002.
                    // var cjkRegex = new Regex(@"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}|[\uFF61-\uFF9F]|[\u3000-\u3003]");
                    var cjkRegex = new Regex(@"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}|[\uFF61-\uFF9F]");

                    foreach (OcrLine ocrLine in ocrResult.Lines)
                    {
                        bool isBeginning = true;
                        bool isCJKPrev = false;
                        foreach (OcrWord ocrWord in ocrLine.Words)
                        {
                            bool isCJK = cjkRegex.IsMatch(ocrWord.Text);

                            // Use spaces to separate non-CJK words.
                            if (!isBeginning && (!isCJK || !isCJKPrev))
                            {
                                _ = text.Append(' ');
                            }

                            _ = text.Append(ocrWord.Text);
                            isCJKPrev = isCJK;
                            isBeginning = false;
                        }

                        text.Append(Environment.NewLine);
                    }
                }
            }
            else
            {
                Windows.Foundation.Point fPoint = new Windows.Foundation.Point(singlePoint.Value.X, singlePoint.Value.Y);
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
        }

        if (culture.TextInfo.IsRightToLeft)
        {
            string[] textListLines = text.ToString().Split(new char[] { '\n', '\r' });

            _ = text.Clear();
            foreach (string textLine in textListLines)
            {
                List<string> wordArray = textLine.Split().ToList();
                wordArray.Reverse();
                _ = isCJKLang == true ? text.Append(string.Join(string.Empty, wordArray)) : text.Append(string.Join(' ', wordArray));

                if (textLine.Length > 0)
                {
                    _ = text.Append('\n');
                }
            }

            return text.ToString();
        }
        else
        {
            return text.ToString();
        }
    }

    public static Bitmap ScaleBitmapUniform(Bitmap passedBitmap, double scale)
    {
        using MemoryStream memory = new MemoryStream();
        passedBitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;
        BitmapImage bitmapimage = new BitmapImage();
        bitmapimage.BeginInit();
        bitmapimage.StreamSource = memory;
        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapimage.EndInit();
        bitmapimage.Freeze();
        TransformedBitmap transformedBmp = new TransformedBitmap();
        transformedBmp.BeginInit();
        transformedBmp.Source = bitmapimage;
        transformedBmp.Transform = new ScaleTransform(scale, scale);
        transformedBmp.EndInit();
        transformedBmp.Freeze();
        return BitmapSourceToBitmap(transformedBmp);
    }

    public static Bitmap BitmapSourceToBitmap(BitmapSource source)
    {
        Bitmap bmp = new Bitmap(
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
        return bmp;
    }

    internal static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using MemoryStream memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;
        BitmapImage bitmapimage = new BitmapImage();
        bitmapimage.BeginInit();
        bitmapimage.StreamSource = memory;
        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapimage.EndInit();
        bitmapimage.Freeze();

        return bitmapimage;
    }

    public static Language? GetOCRLanguage()
    {
        // use currently selected Language
        string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;

        Language? selectedLanguage = new Language(inputLang);
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
