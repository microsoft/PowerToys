// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class WidgetTemplateTests
{
    [TestMethod]
    [DataRow(@"DevHome\Templates\SystemCPUUsageTemplate.json")]
    [DataRow(@"DevHome\Templates\SystemGPUUsageTemplate.json")]
    [DataRow(@"DevHome\Templates\SystemMemoryTemplate.json")]
    [DataRow(@"DevHome\Templates\SystemDiskUsageTemplate.json")]
    [DataRow(@"DevHome\Templates\SystemNetworkUsageTemplate.json")]
    [DataRow(@"DevHome\Templates\SystemBatteryTemplate.json")]
    public void GetContent_LoadsAndCachesDeployedTemplatesInTheUnpackagedTestHost(string templatePath)
    {
        var page = new TemplatePage(templatePath);
        var form = (IFormContent)page.GetContent()[0];
        var template = form.TemplateJson;

        Assert.IsFalse(string.IsNullOrWhiteSpace(template), $"Template loading returned an empty string for {templatePath}.");
        using var document = JsonDocument.Parse(template);
        Assert.AreEqual("AdaptiveCard", document.RootElement.GetProperty("type").GetString());
        Assert.IsTrue(document.RootElement.GetProperty("body").GetArrayLength() > 0);

        var cached = (IFormContent)page.GetContent()[0];
        Assert.AreSame(form, cached);
        Assert.AreEqual(template, cached.TemplateJson);
        Assert.AreEqual(1, page.PathRequests);
    }

    private sealed partial class TemplatePage(string templatePath) : WidgetPage
    {
        public int PathRequests { get; private set; }

        protected override string GetTemplatePath(WidgetPageState page)
        {
            PathRequests++;
            return templatePath;
        }

        protected override void LoadContentData()
        {
        }
    }
}
