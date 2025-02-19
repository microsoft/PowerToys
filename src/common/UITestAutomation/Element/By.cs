// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static OpenQA.Selenium.By;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// This class represents a By selector.
    /// </summary>
    public class By
    {
        private readonly OpenQA.Selenium.By by;

        private By(OpenQA.Selenium.By by)
        {
            this.by = by;
        }

        /// <summary>
        /// Creates a By object using the name attribute.
        /// </summary>
        /// <param name="name">The name attribute to search for.</param>
        /// <returns>A By object.</returns>
        public static By Name(string name) => new By(OpenQA.Selenium.By.Name(name));

        /// <summary>
        /// Creates a By object using the ID attribute.
        /// </summary>
        /// <param name="id">The ID attribute to search for.</param>
        /// <returns>A By object.</returns>
        public static By Id(string id) => new By(OpenQA.Selenium.By.Id(id));

        /// <summary>
        /// Creates a By object using the XPath expression.
        /// </summary>
        /// <param name="xpath">The XPath expression to search for.</param>
        /// <returns>A By object.</returns>
        public static By XPath(string xpath) => new By(OpenQA.Selenium.By.XPath(xpath));

        /// <summary>
        /// Creates a By object using the CSS selector.
        /// </summary>
        /// <param name="cssSelector">The CSS selector to search for.</param>
        /// <returns>A By object.</returns>
        public static By CssSelector(string cssSelector) => new By(OpenQA.Selenium.By.CssSelector(cssSelector));

        /// <summary>
        /// Creates a By object using the link text.
        /// </summary>
        /// <param name="linkText">The link text to search for.</param>
        /// <returns>A By object.</returns>
        public static By LinkText(string linkText) => new By(OpenQA.Selenium.By.LinkText(linkText));

        /// <summary>
        /// Creates a By object using the tag name.
        /// </summary>
        /// <param name="tagName">The tag name to search for.</param>
        /// <returns>A By object.</returns>
        public static By TagName(string tagName) => new By(OpenQA.Selenium.By.TagName(tagName));

        /// <summary>
        /// Converts the By object to an OpenQA.Selenium.By object.
        /// </summary>
        /// <returns>An OpenQA.Selenium.By object.</returns>
        internal OpenQA.Selenium.By ToSeleniumBy() => by;
    }
}
