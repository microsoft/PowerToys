// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class PluginAdditionalOptionCompatibilityTests
    {
        [TestMethod]
        [DataRow(nameof(PluginAdditionalOption.ComboBoxItems))]
        [DataRow(nameof(PluginAdditionalOption.TextValueAsMultilineList))]
        [DataRow(nameof(PluginAdditionalOption.ComboBoxOptions))]
        public void CollectionSettersRetainThirdPartyPluginBinarySignatures(string propertyName)
        {
            // An init accessor adds an IsExternalInit modifier to the setter's return type.
            // Plugins compiled against the original public set accessor cannot call that method.
            var setter = typeof(PluginAdditionalOption).GetProperty(propertyName)?.SetMethod;

            Assert.IsNotNull(setter);
            Assert.IsTrue(setter.IsPublic);
            CollectionAssert.DoesNotContain(setter.ReturnParameter.GetRequiredCustomModifiers(), typeof(IsExternalInit));
        }

        [TestMethod]
        public void CollectionOptionsRemainAssignableAfterConstruction()
        {
            var option = new PluginAdditionalOption();
            option.ComboBoxItems = [new KeyValuePair<string, string>("First", "1")];
            option.TextValueAsMultilineList = ["first", "second"];
            option.ComboBoxOptions = ["legacy-plugin"];

            Assert.AreEqual("First", option.ComboBoxItems[0].Key);
            Assert.AreEqual("first\rsecond", option.TextValue);
            CollectionAssert.AreEqual(new[] { "first", "second" }, option.TextValueAsMultilineList);
            Assert.AreEqual("legacy-plugin", option.ComboBoxOptions[0]);
        }
    }
}
