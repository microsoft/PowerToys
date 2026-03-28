// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class AwakeSettingsCloneTests
    {
        /// <summary>
        /// Verify Clone() copies all AwakeProperties fields, not just the defaults.
        /// Set every property to a non-default value, clone, and compare.
        /// </summary>
        [TestMethod]
        public void Clone_CopiesAllProperties()
        {
            var original = new AwakeSettings
            {
                Properties = new AwakeProperties
                {
                    KeepDisplayOn = true,
                    KeepAwakeOnLidClose = true,
                    Mode = AwakeMode.INDEFINITE,
                    IntervalHours = 5,
                    IntervalMinutes = 30,
                    ExpirationDateTime = new DateTimeOffset(2030, 6, 15, 12, 0, 0, TimeSpan.Zero),
                    CustomTrayTimes = new Dictionary<string, uint>
                    {
                        { "15 minutes", 15 },
                        { "1 hour", 60 },
                    },
                },
            };

            var clone = (AwakeSettings)original.Clone();

            Assert.AreEqual(original.Properties.KeepDisplayOn, clone.Properties.KeepDisplayOn);
            Assert.AreEqual(original.Properties.KeepAwakeOnLidClose, clone.Properties.KeepAwakeOnLidClose);
            Assert.AreEqual(original.Properties.Mode, clone.Properties.Mode);
            Assert.AreEqual(original.Properties.IntervalHours, clone.Properties.IntervalHours);
            Assert.AreEqual(original.Properties.IntervalMinutes, clone.Properties.IntervalMinutes);
            Assert.AreEqual(original.Properties.ExpirationDateTime, clone.Properties.ExpirationDateTime);
            CollectionAssert.AreEqual(
                original.Properties.CustomTrayTimes.OrderBy(kv => kv.Key).ToList(),
                clone.Properties.CustomTrayTimes.OrderBy(kv => kv.Key).ToList());
        }

        /// <summary>
        /// Verify Clone() produces a deep copy: mutating the clone does not affect the original.
        /// </summary>
        [TestMethod]
        public void Clone_ProducesDeepCopy()
        {
            var original = new AwakeSettings
            {
                Properties = new AwakeProperties
                {
                    KeepDisplayOn = true,
                    KeepAwakeOnLidClose = true,
                    Mode = AwakeMode.TIMED,
                    IntervalHours = 2,
                    IntervalMinutes = 15,
                    CustomTrayTimes = new Dictionary<string, uint> { { "30 minutes", 30 } },
                },
            };

            var clone = (AwakeSettings)original.Clone();

            // Mutate clone
            clone.Properties.KeepDisplayOn = false;
            clone.Properties.KeepAwakeOnLidClose = false;
            clone.Properties.Mode = AwakeMode.PASSIVE;
            clone.Properties.IntervalHours = 0;
            clone.Properties.IntervalMinutes = 0;
            clone.Properties.CustomTrayTimes["new entry"] = 99;

            // Original unchanged
            Assert.IsTrue(original.Properties.KeepDisplayOn);
            Assert.IsTrue(original.Properties.KeepAwakeOnLidClose);
            Assert.AreEqual(AwakeMode.TIMED, original.Properties.Mode);
            Assert.AreEqual(2u, original.Properties.IntervalHours);
            Assert.AreEqual(15u, original.Properties.IntervalMinutes);
            Assert.IsFalse(original.Properties.CustomTrayTimes.ContainsKey("new entry"));
        }

        /// <summary>
        /// Guard against future properties being added to AwakeProperties without
        /// updating Clone(). Compares the set of public instance properties on
        /// AwakeProperties against a known list. If this test fails, a new property
        /// was added — update Clone() and this test's expected list.
        /// </summary>
        [TestMethod]
        public void Clone_CoversAllAwakeProperties()
        {
            var propertyNames = typeof(AwakeProperties)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToList();

            var expected = new List<string>
            {
                "CustomTrayTimes",
                "ExpirationDateTime",
                "IntervalHours",
                "IntervalMinutes",
                "KeepAwakeOnLidClose",
                "KeepDisplayOn",
                "Mode",
            };

            var message = "AwakeProperties has properties not covered by this test. " +
                "If you added a new property, update AwakeSettings.Clone() and this expected list.";
            CollectionAssert.AreEqual(expected, propertyNames, message);
        }
    }
}
