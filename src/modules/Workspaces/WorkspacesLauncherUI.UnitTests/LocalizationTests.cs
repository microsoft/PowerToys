// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Models;
using WorkspacesLauncherUI.Properties;

[assembly: DoNotParallelize]

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class LocalizationTests
    {
        [TestMethod]
        public void EveryNeutralResourceHasAMatchingStronglyTypedGetter()
        {
            using var culture = new CultureScope("en-US");
            var source = ReadNeutralResources();
            var getters = typeof(Resources).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.PropertyType == typeof(string))
                .ToArray();

            Assert.IsTrue(source.Count > 0);
            CollectionAssert.AreEquivalent(source.Keys.ToArray(), getters.Select(property => property.Name).ToArray());
            foreach (var getter in getters)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(source[getter.Name]), getter.Name);
                Assert.AreEqual(source[getter.Name], getter.GetValue(null), getter.Name);
            }
        }

        [DataTestMethod]
        [DataRow("unsigned", nameof(Resources.SignatureWarningUnsigned))]
        [DataRow("invalid-signature", nameof(Resources.SignatureWarningInvalid))]
        [DataRow("certificate-untrusted", nameof(Resources.SignatureWarningCertificate))]
        [DataRow("revoked", nameof(Resources.SignatureWarningRevoked))]
        [DataRow("explicit-distrust", nameof(Resources.SignatureWarningDistrusted))]
        [DataRow("expired", nameof(Resources.SignatureWarningExpired))]
        [DataRow("revocation-unavailable", nameof(Resources.SignatureWarningRevocationUnavailable))]
        [DataRow("unresolved-target", nameof(Resources.SignatureWarningUnresolvedTarget))]
        [DataRow("package-not-found", nameof(Resources.SignatureWarningPackageNotFound))]
        [DataRow("package-development", nameof(Resources.SignatureWarningPackageDevelopment))]
        [DataRow("package-external-content", nameof(Resources.SignatureWarningPackageExternalContent))]
        [DataRow("package-integrity-failed", nameof(Resources.SignatureWarningPackageIntegrity))]
        [DataRow("package-unavailable", nameof(Resources.SignatureWarningPackageUnavailable))]
        [DataRow("package-unsigned", nameof(Resources.SignatureWarningPackageUnsigned))]
        [DataRow("package-signing-policy", nameof(Resources.SignatureWarningPackageSigningPolicy))]
        [DataRow("package-verification-unavailable", nameof(Resources.SignatureWarningPackageVerificationUnavailable))]
        [DataRow("package-changed", nameof(Resources.SignatureWarningPackageChanged))]
        [DataRow("unavailable", nameof(Resources.SignatureWarningUnavailable))]
        [DataRow("", nameof(Resources.SignatureWarningUnavailable))]
        [DataRow("unknown-future-reason", nameof(Resources.SignatureWarningUnavailable))]
        [DataRow("package-unknown-future-reason", nameof(Resources.SignatureWarningUnavailable))]
        [DataRow("UNSIGNED", nameof(Resources.SignatureWarningUnavailable))]
        public void ReasonUsesExpectedNonemptyResource(string reason, string resourceKey)
        {
            using var culture = new CultureScope("en-US");
            var request = new SignatureWarningRequest { Reason = reason };

            Assert.IsFalse(string.IsNullOrWhiteSpace(request.ReasonText));
            Assert.AreEqual(ReadNeutralResources()[resourceKey], request.ReasonText);
        }

        [DataTestMethod]
        [DataRow(nameof(SignatureWarningRequest.Title), nameof(Resources.SignatureWarningTitle))]
        [DataRow(nameof(SignatureWarningRequest.TrustLimits), nameof(Resources.SignatureWarningTrustLimits))]
        [DataRow(nameof(SignatureWarningRequest.AppLabel), nameof(Resources.SignatureWarningApp))]
        [DataRow(nameof(SignatureWarningRequest.PathLabel), nameof(Resources.SignatureWarningPath))]
        [DataRow(nameof(SignatureWarningRequest.ReasonLabel), nameof(Resources.SignatureWarningReason))]
        [DataRow(nameof(SignatureWarningRequest.DetailsLabel), nameof(Resources.SignatureWarningDetails))]
        [DataRow(nameof(SignatureWarningRequest.ArgumentsLabel), nameof(Resources.SignatureWarningArguments))]
        [DataRow(nameof(SignatureWarningRequest.StatusLabel), nameof(Resources.SignatureWarningStatus))]
        [DataRow(nameof(SignatureWarningRequest.SkipText), nameof(Resources.SignatureWarningSkip))]
        [DataRow(nameof(SignatureWarningRequest.SkipHelp), nameof(Resources.SignatureWarningSkipHelp))]
        [DataRow(nameof(SignatureWarningRequest.RunAnywayText), nameof(Resources.SignatureWarningRunAnyway))]
        [DataRow(nameof(SignatureWarningRequest.RunAnywayHelp), nameof(Resources.SignatureWarningRunAnywayHelp))]
        [DataRow(nameof(SignatureWarningRequest.CopyDetailsButtonText), nameof(Resources.SignatureWarningCopyDetails))]
        [DataRow(nameof(SignatureWarningRequest.CopyDetailsErrorText), nameof(Resources.SignatureWarningCopyDetailsError))]
        public void LabelAndActionUseExpectedNonemptyResource(string propertyName, string resourceKey)
        {
            using var culture = new CultureScope("en-US");
            var property = typeof(SignatureWarningRequest).GetProperty(propertyName);
            Assert.IsNotNull(property);
            var value = (string)property.GetValue(new SignatureWarningRequest());

            Assert.IsFalse(string.IsNullOrWhiteSpace(value));
            Assert.AreEqual(ReadNeutralResources()[resourceKey], value);
        }

        [DataTestMethod]
        [DataRow("unsigned", nameof(Resources.SignatureWarningExplanation))]
        [DataRow("", nameof(Resources.SignatureWarningExplanation))]
        [DataRow("package-development", nameof(Resources.SignatureWarningPackageExplanation))]
        [DataRow("package-unknown-future-reason", nameof(Resources.SignatureWarningPackageExplanation))]
        [DataRow("PACKAGE-development", nameof(Resources.SignatureWarningExplanation))]
        public void ExplanationDistinguishesPackageReasons(string reason, string resourceKey)
        {
            using var culture = new CultureScope("en-US");
            var request = new SignatureWarningRequest { Reason = reason };

            Assert.IsFalse(string.IsNullOrWhiteSpace(request.Explanation));
            Assert.AreEqual(ReadNeutralResources()[resourceKey], request.Explanation);
        }

        [DataTestMethod]
        [DataRow("fr-FR", "[TEST fr-FR] Warning title")]
        [DataRow("ar-SA", "[TEST ar-SA] Warning title")]
        public void SatelliteUsesExactMarkerAndOmittedKeysFallBackToEnglish(string cultureName, string expectedTitle)
        {
            using var culture = new CultureScope(cultureName);
            var satellite = typeof(Resources).Assembly.GetSatelliteAssembly(CultureInfo.CurrentUICulture);
            CollectionAssert.Contains(satellite.GetManifestResourceNames(), $"WorkspacesLauncherUI.Properties.Resources.{cultureName}.resources");
            var request = new SignatureWarningRequest { Reason = "unsigned" };
            var neutral = ReadNeutralResources();

            Assert.AreEqual(expectedTitle, Resources.SignatureWarningTitle);
            Assert.AreEqual(expectedTitle, request.Title);
            Assert.AreEqual(neutral[nameof(Resources.SignatureWarningPath)], request.PathLabel);
            Assert.AreEqual(neutral[nameof(Resources.SignatureWarningUnsigned)], request.ReasonText);
            Assert.AreEqual(neutral[nameof(Resources.SignatureWarningSkipHelp)], request.SkipHelp);
        }

        [TestMethod]
        public void MissingSatelliteFallsBackToEnglish()
        {
            using var culture = new CultureScope("de-DE");
            var neutral = ReadNeutralResources();

            foreach (var resource in neutral)
            {
                Assert.AreEqual(resource.Value, Resources.ResourceManager.GetString(resource.Key, CultureInfo.CurrentUICulture), resource.Key);
            }

            Assert.AreEqual(neutral[nameof(Resources.SignatureWarningTitle)], new SignatureWarningRequest().Title);
        }

        [DataTestMethod]
        [DataRow("fr-FR", "ar-SA", "[TEST ar-SA] Warning title")]
        [DataRow("ar-SA", "fr-FR", "[TEST fr-FR] Warning title")]
        [DataRow("fr-FR", "de-DE", "Run this app as administrator?")]
        public void ExplicitResourceCultureOverridesCurrentUICulture(string uiCulture, string resourceCulture, string expectedTitle)
        {
            using var culture = new CultureScope(uiCulture);
            Resources.Culture = CultureInfo.GetCultureInfo(resourceCulture);

            Assert.AreEqual(expectedTitle, Resources.SignatureWarningTitle);
            Assert.AreEqual(expectedTitle, new SignatureWarningRequest().Title);
            Assert.AreEqual(ReadNeutralResources()[nameof(Resources.SignatureWarningPath)], new SignatureWarningRequest().PathLabel);

            Resources.Culture = null;
            Assert.AreEqual($"[TEST {uiCulture}] Warning title", new SignatureWarningRequest().Title);
        }

        [DataTestMethod]
        [DataRow("en-US")]
        [DataRow("fr-FR")]
        [DataRow("ar-SA")]
        public void CopiedDetailsPreserveRawValuesAndLocalizedLabels(string cultureName)
        {
            using var culture = new CultureScope(cultureName);
            var request = new SignatureWarningRequest
            {
                AppName = "Example application",
                Path = @"C:\Apps with spaces\example.exe",
                Arguments = "--name \"quoted value\" --other=value",
                Status = "0x800B0100",
                Reason = "unsigned",
            };

            CollectionAssert.AreEqual(
                new[]
                {
                    request.AppLabel, request.AppName, string.Empty,
                    request.PathLabel, request.Path, string.Empty,
                    request.ReasonLabel, request.ReasonText, string.Empty,
                    request.ArgumentsLabel, request.Arguments, string.Empty,
                    request.StatusLabel, request.Status,
                },
                request.DetailsText.Split(Environment.NewLine));
        }

        private static Dictionary<string, string> ReadNeutralResources()
        {
            // This is a build-copied test resource, never a user's settings or workspace file.
            return XDocument.Load(Path.Combine(AppContext.BaseDirectory, "TestResources", "NeutralResources.xml"))
                .Root.Elements("data")
                .ToDictionary(data => data.Attribute("name").Value, data => data.Element("value").Value, StringComparer.Ordinal);
        }
    }
}
