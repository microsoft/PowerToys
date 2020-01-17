using System;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using MarkDownPreviewHandler;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PreviewPaneUnitTests
{
    [TestClass]
    public class MarkDownPreviewHandlerHTMLParsingExtensionTest
    {
        public MarkdownPipeline TestBase()
        {
            MarkdownPipeline pipeline = new MarkdownPipelineBuilder().Build();
            return pipeline;
        }

        [TestMethod]
        public void PipelineOnDocumentProcessed_UpdatesTablesClass_WhenCalled()
        {
            // Arrange 
            String mdString = "Markdown | Less | Pretty\n-- - | --- | ---";
            MarkdownDocument md = Markdown.Parse(mdString);
            HTMLParsingExtension hTMLParsingExtension = new HTMLParsingExtension();

            // Act
            hTMLParsingExtension.PipelineOnDocumentProcessed(md);

            // Assert
            Assert.IsInstanceOfType(md.LastChild, typeof(Table));
        }

        [TestMethod]
        public void PipelineOnDocumentProcessed_UpdatesBlockQuotesClass_WhenCalled()
        {
            // Arrange 
            String mdString = "> Blockquotes are very handy in email to emulate reply text.";
            MarkdownDocument md = Markdown.Parse(mdString);
            HTMLParsingExtension hTMLParsingExtension = new HTMLParsingExtension();

            // Act
            hTMLParsingExtension.PipelineOnDocumentProcessed(md);

            // Assert
            Assert.IsInstanceOfType(md.LastChild, typeof(QuoteBlock));
        }
    }
}
