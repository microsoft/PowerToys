// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Settings.UI.Library.Enumerations;

namespace SvgPreviewHandler
{
    internal sealed class SvgHTMLPreviewGenerator
    {
        private const string CheckeredBackgroundShadeLight = """
            url('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAABhGlDQ1BJQ0MgcHJvZmlsZQAAKJF9kT1Iw0AcxV/TiiIVBzOIOGSogmBBVMRRqlgEC6Wt0KqDyaVf0KQhSXFxFFwLDn4sVh1cnHV1cBUEwQ8QVxcnRRcp8X9JoUWMB8f9eHfvcfcOEBoVplmhCUDTbTMVj0nZ3KrU/YoQRIQxBlFmlpFIL2bgO77uEeDrXZRn+Z/7c/SpeYsBAYl4jhmmTbxBPLNpG5z3iUVWklXic+Jxky5I/Mh1xeM3zkWXBZ4pmpnUPLFILBU7WOlgVjI14mniiKrplC9kPVY5b3HWKjXWuid/YTivr6S5TnMYcSwhgSQkKKihjApsRGnVSbGQov2Yj3/I9SfJpZCrDEaOBVShQXb94H/wu1urMDXpJYVjQNeL43yMAN27QLPuON/HjtM8AYLPwJXe9lcbwOwn6fW2FjkC+reBi+u2puwBlzvA4JMhm7IrBWkKhQLwfkbflAMGboHeNa+31j5OH4AMdbV8AxwcAqNFyl73eXdPZ2//nmn19wOEPHKuuso0oQAAAAlwSFlzAAAuIwAALiMBeKU/dgAAAAd0SU1FB+cEFAwrEI+z8/sAAAAZdEVYdENvbW1lbnQAQ3JlYXRlZCB3aXRoIEdJTVBXgQ4XAAAAL0lEQVQoz2O8e/cuAwzcv38fzlZUVMQqzsRAIqC9BhZi3I0sPhj9QIy7R+OB5hoACxUaWr81wGUAAAAASUVORK5CYII=');
            """;

        private const string CheckeredBackgroundShadeMedium = """
            url('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAABhGlDQ1BJQ0MgcHJvZmlsZQAAKJF9kT1Iw0AcxV/TiiIVBzOIOGSogmBBVMRRqlgEC6Wt0KqDyaVf0KQhSXFxFFwLDn4sVh1cnHV1cBUEwQ8QVxcnRRcp8X9JoUWMB8f9eHfvcfcOEBoVplmhCUDTbTMVj0nZ3KrU/YoQRIQxBlFmlpFIL2bgO77uEeDrXZRn+Z/7c/SpeYsBAYl4jhmmTbxBPLNpG5z3iUVWklXic+Jxky5I/Mh1xeM3zkWXBZ4pmpnUPLFILBU7WOlgVjI14mniiKrplC9kPVY5b3HWKjXWuid/YTivr6S5TnMYcSwhgSQkKKihjApsRGnVSbGQov2Yj3/I9SfJpZCrDEaOBVShQXb94H/wu1urMDXpJYVjQNeL43yMAN27QLPuON/HjtM8AYLPwJXe9lcbwOwn6fW2FjkC+reBi+u2puwBlzvA4JMhm7IrBWkKhQLwfkbflAMGboHeNa+31j5OH4AMdbV8AxwcAqNFyl73eXdPZ2//nmn19wOEPHKuuso0oQAAAAlwSFlzAAAuIwAALiMBeKU/dgAAAAd0SU1FB+cEFA0AJje78TwAAAAZdEVYdENvbW1lbnQAQ3JlYXRlZCB3aXRoIEdJTVBXgQ4XAAAALklEQVQoz2O8e/cuAwwsX74czo6MjMQqzsRAIqC9BhZi3I0sPgj9wDgaD4PCDwBglRs7Q+IL6QAAAABJRU5ErkJggg==');
            """;

        private const string CheckeredBackgroundShadeDark = """
            url('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAABhGlDQ1BJQ0MgcHJvZmlsZQAAKJF9kT1Iw0AcxV/TiiIVBzOIOGSogmBBVMRRqlgEC6Wt0KqDyaVf0KQhSXFxFFwLDn4sVh1cnHV1cBUEwQ8QVxcnRRcp8X9JoUWMB8f9eHfvcfcOEBoVplmhCUDTbTMVj0nZ3KrU/YoQRIQxBlFmlpFIL2bgO77uEeDrXZRn+Z/7c/SpeYsBAYl4jhmmTbxBPLNpG5z3iUVWklXic+Jxky5I/Mh1xeM3zkWXBZ4pmpnUPLFILBU7WOlgVjI14mniiKrplC9kPVY5b3HWKjXWuid/YTivr6S5TnMYcSwhgSQkKKihjApsRGnVSbGQov2Yj3/I9SfJpZCrDEaOBVShQXb94H/wu1urMDXpJYVjQNeL43yMAN27QLPuON/HjtM8AYLPwJXe9lcbwOwn6fW2FjkC+reBi+u2puwBlzvA4JMhm7IrBWkKhQLwfkbflAMGboHeNa+31j5OH4AMdbV8AxwcAqNFyl73eXdPZ2//nmn19wOEPHKuuso0oQAAAAlwSFlzAAAuIwAALiMBeKU/dgAAAAd0SU1FB+cEFA0CCa5crucAAAAZdEVYdENvbW1lbnQAQ3JlYXRlZCB3aXRoIEdJTVBXgQ4XAAAAL0lEQVQoz2NsaWlhgIETJ07A2RYWFljFmRhIBLTXwEKMu5HFB6MfiHH3aDzQXAMACHkZChhWp4QAAAAASUVORK5CYII=');
            """;

        private const string HtmlTemplateSolidColor = """
            <html>
                <body style="background-color: {0}">
                    {1}
                </body>
            </html>
            """;

        private const string HtmlTemplateCheckered = """
            <html>
                <body style="background-image: {0}">
                    {1}
                </body>
            </html>
            """;

        private readonly Settings settings = new();

        private static readonly CompositeFormat HtmlTemplateSolidColorCompositeFormat = System.Text.CompositeFormat.Parse(HtmlTemplateSolidColor);
        private static readonly CompositeFormat HtmlTemplateCheckeredCompositeFormat = System.Text.CompositeFormat.Parse(HtmlTemplateCheckered);

        public string GeneratePreview(string svgData)
        {
            var colorMode = (SvgPreviewColorMode)settings.ColorMode;
            return colorMode switch
            {
                SvgPreviewColorMode.SolidColor => string.Format(CultureInfo.InvariantCulture, HtmlTemplateSolidColorCompositeFormat, ColorTranslator.ToHtml(settings.SolidColor), svgData),
                SvgPreviewColorMode.Checkered => string.Format(CultureInfo.InvariantCulture, HtmlTemplateCheckeredCompositeFormat, GetConfiguredCheckeredShadeImage(), svgData),
                SvgPreviewColorMode.Default or _ => string.Format(CultureInfo.InvariantCulture, HtmlTemplateSolidColorCompositeFormat, ColorTranslator.ToHtml(settings.ThemeColor), svgData),
            };
        }

        private string GetConfiguredCheckeredShadeImage()
        {
            var checkeredShade = (SvgPreviewCheckeredShade)settings.CheckeredShade;
            return checkeredShade switch
            {
                SvgPreviewCheckeredShade.Light=> CheckeredBackgroundShadeLight,
                SvgPreviewCheckeredShade.Medium => CheckeredBackgroundShadeMedium,
                SvgPreviewCheckeredShade.Dark or _ => CheckeredBackgroundShadeDark,
            };
        }
    }
}
