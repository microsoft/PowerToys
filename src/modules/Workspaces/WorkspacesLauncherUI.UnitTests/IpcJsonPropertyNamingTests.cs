// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Utils;

namespace WorkspacesLauncherUI.UnitTests
{
    /// <summary>
    /// Tests for DashCaseNamingPolicy and StringUtils.
    /// These utilities control JSON property name mapping for IPC messages.
    /// </summary>
    [TestClass]
    public class IpcJsonPropertyNamingTests
    {
        private readonly DashCaseNamingPolicy _policy = DashCaseNamingPolicy.Instance;

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_ApplicationPath_MapsTo_application_path()
        {
            Assert.AreEqual("application-path", _policy.ConvertName("ApplicationPath"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_Application_MapsTo_application()
        {
            Assert.AreEqual("application", _policy.ConvertName("Application"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_AppUserModelId_MapsTo_app_user_model_id()
        {
            Assert.AreEqual("app-user-model-id", _policy.ConvertName("AppUserModelId"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_LowercaseInput_RemainsUnchanged()
        {
            Assert.AreEqual("title", _policy.ConvertName("title"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_SingleUppercaseChar_PreservedAsIs()
        {
            Assert.AreEqual("X", _policy.ConvertName("X"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_SingleLowercaseChar_PreservedAsIs()
        {
            Assert.AreEqual("x", _policy.ConvertName("x"));
        }

        // Exact IPC property names that must match the C++ side
        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_PackageFullName_MatchesCppIpcKey()
        {
            Assert.AreEqual("package-full-name", _policy.ConvertName("PackageFullName"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_AppUserModelId_MatchesCppIpcKey()
        {
            Assert.AreEqual("app-user-model-id", _policy.ConvertName("AppUserModelId"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_PwaAppId_MatchesCppIpcKey()
        {
            Assert.AreEqual("pwa-app-id", _policy.ConvertName("PwaAppId"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_CommandLineArguments_MatchesCppIpcKey()
        {
            Assert.AreEqual("command-line-arguments", _policy.ConvertName("CommandLineArguments"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_IsElevated_MatchesCppIpcKey()
        {
            Assert.AreEqual("is-elevated", _policy.ConvertName("IsElevated"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_CanLaunchElevated_MatchesCppIpcKey()
        {
            Assert.AreEqual("can-launch-elevated", _policy.ConvertName("CanLaunchElevated"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_ApplicationPath_MatchesCppIpcKey()
        {
            Assert.AreEqual("application-path", _policy.ConvertName("ApplicationPath"));
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void NamingPolicy_Singleton_ReturnsSameInstanceEveryTime()
        {
            var instance1 = DashCaseNamingPolicy.Instance;
            var instance2 = DashCaseNamingPolicy.Instance;
            Assert.AreSame(instance1, instance2);
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void StringConversion_TwoUppercaseLetters_InsertsDashBetween()
        {
            Assert.AreEqual("a-b", "AB".UpperCamelCaseToDashCase());
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void StringConversion_AllLowercase_NoTransformation()
        {
            Assert.AreEqual("alllowercase", "alllowercase".UpperCamelCaseToDashCase());
        }

        [TestMethod]
        [TestCategory("Serialization")]
        public void StringConversion_NumbersInMiddle_PreservedWithDashBeforeNextUpper()
        {
            Assert.AreEqual("version2-test", "Version2Test".UpperCamelCaseToDashCase());
        }
    }
}
