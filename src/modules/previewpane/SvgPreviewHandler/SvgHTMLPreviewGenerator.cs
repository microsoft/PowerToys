// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Globalization;

namespace SvgPreviewHandler
{
    internal sealed class SvgHTMLPreviewGenerator
    {
        private const string CheckeredBackgroundShade1 = """
            url('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAABhGlDQ1BJQ0MgcHJvZmlsZQAAKJF9kT1Iw0AcxV/TiqItDnYQcchQxcGCqIijVLEIFkpboVUHk0u/oElDkuLiKLgWHPxYrDq4OOvq4CoIgh8gri5Oii5S4v+SQosYD4778e7e4+4dIDQqTDUDE4CqWUYqHhOzuVWx+xUB9CEEYExipp5IL2bgOb7u4ePrXZRneZ/7c4SUvMkAn0g8x3TDIt4gntm0dM77xGFWkhTic+Jxgy5I/Mh12eU3zkWHBZ4ZNjKpeeIwsVjsYLmDWclQiaeJI4qqUb6QdVnhvMVZrdRY6578hcG8tpLmOs1hxLGEBJIQIaOGMiqwEKVVI8VEivZjHv4hx58kl0yuMhg5FlCFCsnxg//B727NwtSkmxSMAV0vtv0xAnTvAs26bX8f23bzBPA/A1da219tALOfpNfbWuQI6N8GLq7bmrwHXO4Ag0+6ZEiO5KcpFArA+xl9Uw4YuAV619zeWvs4fQAy1NXyDXBwCIwWKXvd4909nb39e6bV3w87j3KR+nFUEgAAAAlwSFlzAAAuIwAALiMBeKU/dgAAAAd0SU1FB+cECw0KNtiZThsAAAAZdEVYdENvbW1lbnQAQ3JlYXRlZCB3aXRoIEdJTVBXgQ4XAAAAMElEQVQoz2N0dXVlgIFdu3bB2W5ubljFmRhIBLTXwPj//3+C7kYWH4x+GI2HQeEHAKsiGbWMbaqGAAAAAElFTkSuQmCC');
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

        private Settings settings = new Settings();

        public string GeneratePreview(string svgData)
        {
            var colorMode = settings.ColorMode;
            return colorMode switch
            {
                1 => string.Format(CultureInfo.InvariantCulture, HtmlTemplateSolidColor, ColorTranslator.ToHtml(settings.SolidColor), svgData),
                2 => string.Format(CultureInfo.InvariantCulture, HtmlTemplateCheckered, CheckeredBackgroundShade1, svgData),
                _ => string.Format(CultureInfo.InvariantCulture, HtmlTemplateSolidColor, ColorTranslator.ToHtml(settings.ThemeColor), svgData),
            };
        }
    }
}
