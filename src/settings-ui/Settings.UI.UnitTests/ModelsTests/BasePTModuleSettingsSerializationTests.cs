// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommonLibTest
{
    [TestClass]
    public class BasePTModuleSettingsSerializationTests
    {
        /// <summary>
        /// Test to verify that all classes derived from BasePTModuleSettings are registered
        /// in the SettingsSerializationContext for Native AOT compatibility.
        /// </summary>
        [TestMethod]
        public void AllBasePTModuleSettingsClasses_ShouldBeRegisteredInSerializationContext()
        {
            // Arrange
            var assembly = typeof(BasePTModuleSettings).Assembly;
            var settingsClasses = assembly.GetTypes()
                .Where(t => typeof(BasePTModuleSettings).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(BasePTModuleSettings))
                .OrderBy(t => t.Name)
                .ToList();

            Assert.IsTrue(settingsClasses.Count > 0, "No BasePTModuleSettings derived classes found. This test may be broken.");

            var jsonSerializerOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = SettingsSerializationContext.Default,
            };

            var unregisteredTypes = new System.Collections.Generic.List<string>();

            // Act & Assert
            foreach (var settingsType in settingsClasses)
            {
                var typeInfo = jsonSerializerOptions.TypeInfoResolver?.GetTypeInfo(settingsType, jsonSerializerOptions);

                if (typeInfo == null)
                {
                    unregisteredTypes.Add(settingsType.FullName ?? settingsType.Name);
                }
            }

            // Assert
            if (unregisteredTypes.Count > 0)
            {
                var errorMessage = $"The following {unregisteredTypes.Count} settings class(es) are NOT registered in SettingsSerializationContext:\n" +
                                   $"{string.Join("\n", unregisteredTypes.Select(t => $"  - {t}"))}\n\n" +
                                   $"Please add [JsonSerializable(typeof(ClassName))] attribute to SettingsSerializationContext.cs for each missing type.";
                Assert.Fail(errorMessage);
            }

            // Print success message with count
            Console.WriteLine($"✓ All {settingsClasses.Count} BasePTModuleSettings derived classes are properly registered in SettingsSerializationContext.");
        }

        /// <summary>
        /// Test to verify that calling ToJsonString() on an unregistered type throws InvalidOperationException
        /// with a helpful error message.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ToJsonString_UnregisteredType_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var unregisteredSettings = new UnregisteredTestSettings
            {
                Name = "UnregisteredModule",
                Version = "1.0.0",
            };

            // Act - This should throw InvalidOperationException
            var jsonString = unregisteredSettings.ToJsonString();

            // Assert - Exception should be thrown, so this line should never be reached
            Assert.Fail("Expected InvalidOperationException was not thrown.");
        }

        /// <summary>
        /// Test to verify that the error message for unregistered types is helpful and contains
        /// necessary information for developers.
        /// </summary>
        [TestMethod]
        public void ToJsonString_UnregisteredType_ShouldHaveHelpfulErrorMessage()
        {
            // Arrange
            var unregisteredSettings = new UnregisteredTestSettings
            {
                Name = "UnregisteredModule",
                Version = "1.0.0",
            };

            // Act & Assert
            try
            {
                var jsonString = unregisteredSettings.ToJsonString();
                Assert.Fail("Expected InvalidOperationException was not thrown.");
            }
            catch (InvalidOperationException ex)
            {
                // Verify the error message contains helpful information
                Assert.IsTrue(ex.Message.Contains("UnregisteredTestSettings"), "Error message should contain the type name.");
                Assert.IsTrue(ex.Message.Contains("SettingsSerializationContext"), "Error message should mention SettingsSerializationContext.");
                Assert.IsTrue(ex.Message.Contains("JsonSerializable"), "Error message should mention JsonSerializable attribute.");

                Console.WriteLine($"✓ Error message is helpful: {ex.Message}");
            }
        }

        /// <summary>
        /// Test class that is intentionally NOT registered in SettingsSerializationContext
        /// to verify error handling for unregistered types.
        /// </summary>
        private sealed class UnregisteredTestSettings : BasePTModuleSettings
        {
            // Intentionally empty - this class should NOT be registered in SettingsSerializationContext
        }
    }
}
