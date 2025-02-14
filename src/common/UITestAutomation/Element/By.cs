// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Xml.Linq;
using static OpenQA.Selenium.By;

namespace Microsoft.PowerToys.UITest
{
    // This class is a wrapper around OpenQA.Selenium.By to provide a more user-friendly API.
#pragma warning disable SA1649 // File name should match first type name
    public class By
    {
        // The OpenQA.Selenium.By object that this class wraps.
        private readonly OpenQA.Selenium.By by;

        // Private constructor to prevent instantiation of this class from outside.
        private By(OpenQA.Selenium.By by)
        {
            this.by = by;
        }

        // Factory method to create a By object using the element's name.
        public static By Name(string name) => new By(OpenQA.Selenium.By.Name(name));

        // Factory method to create a By object using the element's ID.
        public static By Id(string id) => new By(OpenQA.Selenium.By.Id(id));

        // Factory method to create a By object using an XPath expression.
        public static By XPath(string xpath) => new By(OpenQA.Selenium.By.XPath(xpath));

        // Factory method to create a By object using a CSS selector.
        public static By CssSelector(string cssSelector) => new By(OpenQA.Selenium.By.CssSelector(cssSelector));

        // Factory method to create a By object using the link text.
        public static By LinkText(string linkText) => new By(OpenQA.Selenium.By.LinkText(linkText));

        // Factory method to create a By object using the tag name.
        public static By TagName(string tagName) => new By(OpenQA.Selenium.By.TagName(tagName));

        // Method to convert this By object to an OpenQA.Selenium.By object.
        public OpenQA.Selenium.By ToSeleniumBy() => by;
    }
}
