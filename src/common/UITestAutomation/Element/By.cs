// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Xml.Linq;
using static OpenQA.Selenium.By;

namespace Microsoft.PowerToys.UITest
{
    // This class is a wrapper around OpenQA.Selenium.By
#pragma warning disable SA1649 // File name should match first type name
    public class By
    {
        private readonly OpenQA.Selenium.By by;

        private By(OpenQA.Selenium.By by)
        {
            this.by = by;
        }

        // Factory methods to create a By object
        public static By Name(string name) => new By(OpenQA.Selenium.By.Name(name));

        public static By Id(string id) => new By(OpenQA.Selenium.By.Id(id));

        public static By XPath(string xpath) => new By(OpenQA.Selenium.By.XPath(xpath));

        public static By CssSelector(string cssSelector) => new By(OpenQA.Selenium.By.CssSelector(cssSelector));

        public static By LinkText(string linkText) => new By(OpenQA.Selenium.By.LinkText(linkText));

        public static By TagName(string tagName) => new By(OpenQA.Selenium.By.TagName(tagName));

        public OpenQA.Selenium.By ToSeleniumBy() => by;
    }
}
