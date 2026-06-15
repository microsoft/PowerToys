// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using KeyboardManagerEditorUI.Templates;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    [TestClass]
    public class TemplateResolverTests
    {
        private static CommandTemplate Template(string args, params TemplateParameter[] parameters)
            => new()
            {
                Id = "test.cmd",
                Executable = "%LOCALAPPDATA%\\PowerToys\\PowerToys.exe",
                ArgsTemplate = args,
                Parameters = new List<TemplateParameter>(parameters),
            };

        private static TemplateParameter Param(string name, string type = "Text", bool required = true)
            => new() { Name = name, Type = type, Required = required };

        [TestMethod]
        public void Resolve_SubstitutesPresentValue()
        {
            var result = TemplateResolver.Resolve(
                Template("--open-settings={module}", Param("module")),
                new Dictionary<string, string> { ["module"] = "ColorPicker" });

            Assert.AreEqual("%LOCALAPPDATA%\\PowerToys\\PowerToys.exe", result.Executable);
            Assert.AreEqual("--open-settings=ColorPicker", result.Args);
        }

        [TestMethod]
        public void Resolve_MissingValue_SubstitutesEmpty()
        {
            var result = TemplateResolver.Resolve(
                Template("--open-settings={module}", Param("module")),
                new Dictionary<string, string>());

            Assert.AreEqual("--open-settings=", result.Args);
        }

        [TestMethod]
        public void Resolve_NullValues_SubstitutesEmpty()
        {
            var result = TemplateResolver.Resolve(
                Template("--open-settings={module}", Param("module")),
                values: null);

            Assert.AreEqual("--open-settings=", result.Args);
        }

        [TestMethod]
        public void Resolve_NoParameters_ReturnsTemplateVerbatim()
        {
            var result = TemplateResolver.Resolve(Template("--open-settings"), null);
            Assert.AreEqual("--open-settings", result.Args);
        }

        [TestMethod]
        public void Resolve_UnknownPlaceholder_LeftUntouched()
        {
            // {other} is not a declared parameter, so it must be preserved literally.
            var result = TemplateResolver.Resolve(
                Template("--a={module} --b={other}", Param("module")),
                new Dictionary<string, string> { ["module"] = "X" });

            Assert.AreEqual("--a=X --b={other}", result.Args);
        }

        [TestMethod]
        public void Resolve_IsSinglePass_DoesNotReSubstituteInjectedPlaceholder()
        {
            // If the first parameter's value contains "{second}", the second pass must NOT replace it.
            var result = TemplateResolver.Resolve(
                Template("{first}-{second}", Param("first"), Param("second")),
                new Dictionary<string, string>
                {
                    ["first"] = "{second}",
                    ["second"] = "INJECTED",
                });

            Assert.AreEqual("{second}-INJECTED", result.Args);
        }

        [TestMethod]
        public void Resolve_NoSubstringCollisionBetweenSimilarNames()
        {
            var result = TemplateResolver.Resolve(
                Template("{module}|{moduleVersion}", Param("module"), Param("moduleVersion")),
                new Dictionary<string, string> { ["module"] = "A", ["moduleVersion"] = "B" });

            Assert.AreEqual("A|B", result.Args);
        }

        [TestMethod]
        public void Resolve_ValueWithSpace_IsQuoted()
        {
            var result = TemplateResolver.Resolve(
                Template("--path={p}", Param("p")),
                new Dictionary<string, string> { ["p"] = "C:\\Program Files\\App" });

            Assert.AreEqual("--path=\"C:\\Program Files\\App\"", result.Args);
        }

        [TestMethod]
        public void Resolve_SimpleValue_NotQuoted()
        {
            var result = TemplateResolver.Resolve(
                Template("--x={p}", Param("p")),
                new Dictionary<string, string> { ["p"] = "Plain" });

            Assert.AreEqual("--x=Plain", result.Args);
        }

        [TestMethod]
        public void Resolve_ValueWithQuote_IsEscaped()
        {
            var result = TemplateResolver.Resolve(
                Template("--x={p}", Param("p")),
                new Dictionary<string, string> { ["p"] = "a\"b c" });

            // Embedded quote is backslash-escaped and the whole value wrapped in quotes.
            Assert.AreEqual("--x=\"a\\\"b c\"", result.Args);
        }

        [TestMethod]
        public void QuoteArgumentIfNeeded_TrailingBackslashesBeforeClosingQuote_AreDoubled()
        {
            // "a b\\" must become "a b\\\\" so the backslashes don't escape the closing quote.
            Assert.AreEqual("\"a b\\\\\"", TemplateResolver.QuoteArgumentIfNeeded("a b\\"));
        }

        [TestMethod]
        public void QuoteArgumentIfNeeded_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, TemplateResolver.QuoteArgumentIfNeeded(string.Empty));
        }
    }
}
