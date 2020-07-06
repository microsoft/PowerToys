using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Wox.Infrastructure;
using Wox.Plugin;
using Microsoft.Plugin.Program.Programs;
using Moq;
using System.IO;

namespace Wox.Test.Plugins
{
    class UWPTests
    {
        // Mock UWP.Application class to test the Result Properties
        public class mockUWP : UWP.Application
        {
            public mockUWP(AppxPackageHelper.IAppxManifestApplication manifestApp, UWP package) : base(manifestApp, package) { }

            protected override int Score(string query)
            {
                // To prevent the result from being filtered, the score must be a positive integer
                const int scoreGreateThanZero = 1;
                return scoreGreateThanZero;
            }

            // Stubbing out the StringMatcher.Fuzzysearch() functionality
            protected override List<int> GetMatchingData(string query)
            {
                return null;
            }
        }

        [TestCase("Name", "NamePrefixInDescription")]
        [TestCase("ignoreName", "ignoreDescription")]
        public void PackagedApps_ShouldSetNameAsTitle_WhileCreatingResult(string Name, string Description)
        {
            // Arrange
            // Mocking the GetTranslation function called by SetSubtitle
            string subtitle = "subtitle";
            Mock<IPublicAPI> mockAPI = new Mock<IPublicAPI>();
            mockAPI.Setup(m => m.GetTranslation(It.IsAny<string>())).Returns(subtitle);

            // Create an object of the mock packaged App Class and set the name and description
            mockUWP item = new mockUWP(null, null)
            {
                DisplayName = Name,
                Description = Description
            };

            // Act
            // The query string value does not matter
            string query = "ignore";
            Result res = item.Result(query, mockAPI.Object);

            // Assert
            Assert.IsTrue(res.Title.Equals(Name));
        }
    }
}
