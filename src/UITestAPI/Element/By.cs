// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using OpenQA.Selenium;

namespace Microsoft.PowerToys.UITest
{
#pragma warning disable SA1649 // File name should match first type name
    public class By
    {
        private readonly OpenQA.Selenium.By by;

        private By(OpenQA.Selenium.By by)
        {
            this.by = by;
        }

        public static By Name(string name)
        {
            return new By(OpenQA.Selenium.By.Name(name));
        }

        public static By Id(string id)
        {
            return new By(OpenQA.Selenium.By.Id(id));
        }

        public static By XPath(string xpath)
        {
            return new By(OpenQA.Selenium.By.XPath(xpath));
        }

        public static By CssSelector(string xpath)
        {
            return new By(OpenQA.Selenium.By.CssSelector(xpath));
        }

        public static By LinkText(string linkText)
        {
            return new By(OpenQA.Selenium.By.LinkText(linkText));
        }

        public static By TagName(string tagName)
        {
            return new By(OpenQA.Selenium.By.TagName(tagName));
        }

        public OpenQA.Selenium.By ToSeleniumBy()
        {
            return by;
        }
    }
}
