// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Markdig;

namespace Microsoft.PowerToys.FilePreviewCommon
{
    public static class MarkdownHelper
    {
        /// <summary>
        /// Markdown HTML header for light theme.
        /// </summary>
        private static readonly string HtmlLightHeader = "<!doctype html><style>body{width:100%;margin:0;font-family:-apple-system,BlinkMacSystemFont,\"Segoe UI\",Roboto,\"Helvetica Neue\",Arial,\"Noto Sans\",sans-serif,\"Apple Color Emoji\",\"Segoe UI Emoji\",\"Segoe UI Symbol\",\"Noto Color Emoji\";font-size:1rem;font-weight:400;line-height:1.5;color:#212529;text-align:left;background-color:#fff}.container{padding:5%}body img{max-width:100%;height:auto}body h1,body h2,body h3,body h4,body h5,body h6{margin-top:24px;margin-bottom:16px;font-weight:600;line-height:1.25}body h1,body h2{padding-bottom:.3em;border-bottom:1px solid #eaecef}body{font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif,Apple Color Emoji,Segoe UI Emoji}body h3{font-size:1.25em}body h4{font-size:1em}body h5{font-size:.875em}body h6{font-size:.85em;color:#6a737d}pre{font-family:SFMono-Regular,Consolas,Liberation Mono,Menlo,monospace;background-color:#f6f8fa;border-radius:3px;padding:16px;font-size:85%}a{color:#0366d6}strong{font-weight:600}em{font-style:italic}code{padding:.2em .4em;margin:0;font-size:85%;background-color:#f6f8fa;border-radius:3px}hr{border-color:#EEE -moz-use-text-color #FFF;border-style:solid none;border-width:.5px 0;margin:18px 0}table{display:block;width:100%;overflow:auto;border-spacing:0;border-collapse:collapse}tbody{display:table-row-group;vertical-align:middle;border-color:inherit;vertical-align:inherit;border-color:inherit}table tr{background-color:#fff;border-top:1px solid #c6cbd1}tr{display:table-row;vertical-align:inherit;border-color:inherit}table td,table th{padding:6px 13px;border:1px solid #dfe2e5}th{font-weight:600;display:table-cell;vertical-align:inherit;font-weight:bold;text-align:-internal-center}thead{display:table-header-group;vertical-align:middle;border-color:inherit}td{display:table-cell;vertical-align:inherit}code,pre,tt{font-family:SFMono-Regular,Menlo,Monaco,Consolas,\"Liberation Mono\",\"Courier New\",monospace;color:#24292e;overflow-x:auto}pre code{display:block;font-size:inherit;color:inherit;word-break:normal}blockquote{background-color:#fff;border-radius:3px;padding:15px;font-size:14px;display:block;margin-block-start:1em;margin-block-end:1em;margin-inline-start:40px;margin-inline-end:40px;padding:0 1em;color:#6a737d;border-left:.25em solid #dfe2e5}</style><body><div class=\"container\">";

        /// <summary>
        /// Markdown HTML header for dark theme.
        /// </summary>
        private static readonly string HtmlDarkHeader = "<!doctype html><style>body{width:100%;margin:0;font-family:-apple-system,BlinkMacSystemFont,\"Segoe UI\",Roboto,\"Helvetica Neue\",Arial,\"Noto Sans\",sans-serif,\"Apple Color Emoji\",\"Segoe UI Emoji\",\"Segoe UI Symbol\",\"Noto Color Emoji\";font-size:1rem;font-weight:400;line-height:1.5;color:#d4d4d4;text-align:left;background-color:#1e1e1e}.container{padding:5%}body img{max-width:100%;height:auto}body h1,body h2,body h3,body h4,body h5,body h6{margin-top:24px;margin-bottom:16px;font-weight:600;line-height:1.25}body h1,body h2{padding-bottom:.3em;border-bottom:1px solid #474747}body{font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif,Apple Color Emoji,Segoe UI Emoji}body h3{font-size:1.25em}body h4{font-size:1em}body h5{font-size:.875em}body h6{font-size:.85em;color:#d4d4d4}pre{font-family:SFMono-Regular,Consolas,Liberation Mono,Menlo,monospace;background-color:#161616;border-radius:3px;padding:16px;font-size:85%}a{color:#0366d6}strong{font-weight:600}em{font-style:italic}code{padding:.2em .4em;margin:0;font-size:85%;background-color:#161616;border-radius:3px}hr{border-color:#EEE -moz-use-text-color #FFF;border-style:solid none;border-width:.5px 0;margin:18px 0}table{display:block;width:100%;overflow:auto;border-spacing:0;border-collapse:collapse}tbody{display:table-row-group;vertical-align:middle;border-color:inherit;vertical-align:inherit;border-color:inherit}table tr{background-color:#1e1e1e;border-top:1px solid #c6cbd1}tr{display:table-row;vertical-align:inherit;border-color:inherit}table td,table th{padding:6px 13px;border:1px solid #474747}th{font-weight:600;display:table-cell;vertical-align:inherit;font-weight:bold;text-align:-internal-center}thead{display:table-header-group;vertical-align:middle;border-color:inherit}td{display:table-cell;vertical-align:inherit}code,pre,tt{font-family:SFMono-Regular,Menlo,Monaco,Consolas,\"Liberation Mono\",\"Courier New\",monospace;color:#d4d4d4;overflow-x:auto}pre code{display:block;font-size:inherit;color:inherit;word-break:normal}blockquote{background-color:#282828;border-radius:3px;padding:15px;font-size:14px;display:block;margin-block-start:1em;margin-block-end:1em;margin-inline-start:40px;margin-inline-end:40px;padding:0 1em;color:#d4d4d4;border-left:.25em solid #d4d4d4}</style><body><div class=\"container\">";

        /// <summary>
        /// Markdown HTML footer.
        /// </summary>
        private static readonly string HtmlFooter = "</div></body></html>";

        public static string MarkdownHtml(string fileContent, string theme, string filePath, ImagesBlockedCallBack imagesBlockedCallBack)
        {
            var htmlHeader = theme == "dark" ? HtmlDarkHeader : HtmlLightHeader;

            // Extension to modify markdown AST.
            HTMLParsingExtension extension = new HTMLParsingExtension(imagesBlockedCallBack);
            extension.FilePath = Path.GetDirectoryName(filePath) ?? string.Empty;

            // if you have a string with double space, some people view it as a new line.
            // while this is against spec, even GH supports this. Technically looks like GH just trims whitespace
            // https://github.com/microsoft/PowerToys/issues/10354
            var softlineBreak = new Markdig.Extensions.Hardlines.SoftlineBreakAsHardlineExtension();

            MarkdownPipelineBuilder pipelineBuilder;
            pipelineBuilder = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseEmojiAndSmiley().UseYamlFrontMatter().UseMathematics();
            pipelineBuilder.Extensions.Add(extension);
            pipelineBuilder.Extensions.Add(softlineBreak);

            MarkdownPipeline pipeline = pipelineBuilder.Build();
            string parsedMarkdown = Markdown.ToHtml(fileContent, pipeline);

            string markdownHTML = $"{htmlHeader}{parsedMarkdown}{HtmlFooter}";
            return markdownHTML;
        }
    }
}
