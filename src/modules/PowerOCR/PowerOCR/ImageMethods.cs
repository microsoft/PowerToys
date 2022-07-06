namespace PowerOCR;

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
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using BitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;

internal class ImageMethods
{
    internal static ImageSource GetWindowBoundsImage(Window passedWindow)
    {
        bool isGrabFrame = false;

        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        int windowWidth = (int)(passedWindow.ActualWidth * dpi.DpiScaleX);
        int windowHeight = (int)(passedWindow.ActualHeight * dpi.DpiScaleY);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();
        int thisCorrectedLeft = (int)(absPosPoint.X * dpi.DpiScaleX);
        int thisCorrectedTop = (int)(absPosPoint.Y * dpi.DpiScaleY);

        if (isGrabFrame == true)
        {
            thisCorrectedLeft += (int)(2 * dpi.DpiScaleX);
            thisCorrectedTop += (int)(26 * dpi.DpiScaleY);
            windowWidth -= (int)(4 * dpi.DpiScaleX);
            windowHeight -= (int)(70 * dpi.DpiScaleY);
        }

        Bitmap bmp = new(windowWidth, windowHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(bmp);

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return BitmapToImageSource(bmp);
    }

    internal static async Task<string> GetRegionsText(Window? passedWindow, Rectangle selectedRegion)
    {
        Bitmap bmp = new(selectedRegion.Width, selectedRegion.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(bmp);

        System.Windows.Point absPosPoint;

        if (passedWindow == null)
            absPosPoint = new();
        else
            absPosPoint = passedWindow.GetAbsolutePosition();

        int thisCorrectedLeft = (int)absPosPoint.X + selectedRegion.Left;
        int thisCorrectedTop = (int)absPosPoint.Y + selectedRegion.Top;

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        // bmp = PadImage(bmp);

        string? ResultText = await ExtractText(bmp);

        if (ResultText != null)
            return ResultText.Trim();
        else
            return "";
    }

    internal static async Task<string> GetClickedWord(Window passedWindow, System.Windows.Point clickedPoint)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(passedWindow);
        Bitmap bmp = new((int)(passedWindow.ActualWidth * dpi.DpiScaleX), (int)(passedWindow.ActualHeight * dpi.DpiScaleY), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(bmp);

        System.Windows.Point absPosPoint = passedWindow.GetAbsolutePosition();
        int thisCorrectedLeft = (int)absPosPoint.X;
        int thisCorrectedTop = (int)absPosPoint.Y;

        g.CopyFromScreen(thisCorrectedLeft, thisCorrectedTop, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        System.Windows.Point adjustedPoint = new System.Windows.Point(clickedPoint.X, clickedPoint.Y);

        string ResultText = await ExtractText(bmp, adjustedPoint);
        return ResultText.Trim();
    }

    public static async Task<string> ExtractText(Bitmap bmp, System.Windows.Point? singlePoint = null)
    {
        Language? selectedLanguage = GetOCRLanguage();
        if (selectedLanguage == null)
            return "";

        bool isCJKLang = false;

        if (selectedLanguage.LanguageTag.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase) == true)
            isCJKLang = true;
        else if (selectedLanguage.LanguageTag.StartsWith("ja", StringComparison.InvariantCultureIgnoreCase) == true)
            isCJKLang = true;
        else if (selectedLanguage.LanguageTag.StartsWith("ko", StringComparison.InvariantCultureIgnoreCase) == true)
            isCJKLang = true;

        XmlLanguage lang = XmlLanguage.GetLanguage(selectedLanguage.LanguageTag);
        CultureInfo culture = lang.GetEquivalentCulture();

        bool scaleBMP = true;

        if (singlePoint != null
            || bmp.Width * 1.5 > OcrEngine.MaxImageDimension)
        {
            scaleBMP = false;
        }

        Bitmap scaledBitmap;
        if (scaleBMP)
            scaledBitmap = ScaleBitmapUniform(bmp, 1.5);
        else
            scaledBitmap = ScaleBitmapUniform(bmp, 1.0);

        StringBuilder text = new();

        await using (MemoryStream memory = new())
        {
            scaledBitmap.Save(memory, ImageFormat.Bmp);
            memory.Position = 0;
            BitmapDecoder bmpDecoder = await BitmapDecoder.CreateAsync(memory.AsRandomAccessStream());
            SoftwareBitmap softwareBmp = await bmpDecoder.GetSoftwareBitmapAsync();

            OcrEngine ocrEngine = OcrEngine.TryCreateFromLanguage(selectedLanguage);
            OcrResult ocrResult = await ocrEngine.RecognizeAsync(softwareBmp);

            if (singlePoint == null)
            {
                foreach (OcrLine line in ocrResult.Lines) text.AppendLine(line.Text);
            }
            else
            {
                Windows.Foundation.Point fPoint = new Windows.Foundation.Point(singlePoint.Value.X, singlePoint.Value.Y);
                foreach (OcrLine ocrLine in ocrResult.Lines)
                {
                    foreach (OcrWord ocrWord in ocrLine.Words)
                    {
                        if (ocrWord.BoundingRect.Contains(fPoint))
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
                if (isCJKLang == true)
                    _ = text.Append(string.Join("", wordArray));
                else
                    _ = text.Append(string.Join(' ', wordArray));

                if (textLine.Length > 0)
                    _ = text.Append('\n');
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
        using MemoryStream memory = new();
        passedBitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;
        BitmapImage bitmapimage = new();
        bitmapimage.BeginInit();
        bitmapimage.StreamSource = memory;
        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapimage.EndInit();
        TransformedBitmap transformedBmp = new();
        transformedBmp.BeginInit();
        transformedBmp.Source = bitmapimage;
        transformedBmp.Transform = new ScaleTransform(scale, scale);
        transformedBmp.EndInit();
        return BitmapSourceToBitmap(transformedBmp.Source);
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
        return bmp;
    }

    internal static BitmapImage BitmapToImageSource(Bitmap bitmap)
    {
        using MemoryStream memory = new();
        bitmap.Save(memory, ImageFormat.Bmp);
        memory.Position = 0;
        BitmapImage bitmapimage = new();
        bitmapimage.BeginInit();
        bitmapimage.StreamSource = memory;
        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapimage.EndInit();

        return bitmapimage;
    }

    public static Language? GetOCRLanguage()
    {
        // use currently selected Language
        string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;

        Language? selectedLanguage = new(inputLang);
        List<Language> possibleOCRLangs = OcrEngine.AvailableRecognizerLanguages.ToList();

        if (possibleOCRLangs.Count < 1)
        {
            MessageBox.Show("No possible OCR languages are installed.", "Text Grab");
            return null;
        }

        if (possibleOCRLangs.All(l => l.LanguageTag != selectedLanguage.LanguageTag))
        {
            List<Language>? similarLanguages = possibleOCRLangs.Where(
                la => la.AbbreviatedName == selectedLanguage.AbbreviatedName).ToList();

            if (similarLanguages != null)
            {
                selectedLanguage = similarLanguages.Count > 0
                    ? similarLanguages.FirstOrDefault()
                    : possibleOCRLangs.FirstOrDefault();
            }
        }

        return selectedLanguage;
    }
}
