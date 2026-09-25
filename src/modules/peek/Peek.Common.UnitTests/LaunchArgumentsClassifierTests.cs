// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Helpers;

using ClassificationMode = Peek.Common.Helpers.LaunchArgumentsClassifier.ClassificationMode;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class LaunchArgumentsClassifierTests
    {
        [TestMethod]
        public void Classify_NullArguments_ReturnsNone()
        {
            var result = LaunchArgumentsClassifier.Classify(null);

            Assert.AreEqual(ClassificationMode.None, result.Mode);
        }

        [TestMethod]
        public void Classify_EmptyArguments_ReturnsNone()
        {
            var result = LaunchArgumentsClassifier.Classify([]);

            Assert.AreEqual(ClassificationMode.None, result.Mode);
        }

        [TestMethod]
        public void Classify_ValidRunnerArguments_ReturnsRunnerWithPid()
        {
            var result = LaunchArgumentsClassifier.Classify(["--runner-pid", "1234"]);

            Assert.AreEqual(ClassificationMode.Runner, result.Mode);
            Assert.AreEqual(1234, result.RunnerPid);
        }

        [TestMethod]
        public void Classify_ValidRunnerArgumentsCaseInsensitive_ReturnsRunnerWithPid()
        {
            var result = LaunchArgumentsClassifier.Classify(["--RUNNER-PID", "5678"]);

            Assert.AreEqual(ClassificationMode.Runner, result.Mode);
            Assert.AreEqual(5678, result.RunnerPid);
        }

        [TestMethod]
        public void Classify_RunnerArgumentsMissingPid_ReturnsInvalidRunnerArguments()
        {
            var result = LaunchArgumentsClassifier.Classify(["--runner-pid"]);

            Assert.AreEqual(ClassificationMode.InvalidRunnerArguments, result.Mode);
        }

        [TestMethod]
        public void Classify_RunnerArgumentsWithNonIntegerPid_ReturnsInvalidRunnerArguments()
        {
            var result = LaunchArgumentsClassifier.Classify(["--runner-pid", "abc"]);

            Assert.AreEqual(ClassificationMode.InvalidRunnerArguments, result.Mode);
        }

        [TestMethod]
        public void Classify_RunnerArgumentsWithExtraValues_ReturnsInvalidRunnerArguments()
        {
            var result = LaunchArgumentsClassifier.Classify(["--runner-pid", "123", "extra"]);

            Assert.AreEqual(ClassificationMode.InvalidRunnerArguments, result.Mode);
        }

        [TestMethod]
        public void Classify_CliSingleArgument_ReturnsCli()
        {
            var result = LaunchArgumentsClassifier.Classify(["C:\\temp\\file.png"]);

            Assert.AreEqual(ClassificationMode.Cli, result.Mode);
            Assert.AreEqual(1, result.CliArguments.Count);
            Assert.AreEqual("C:\\temp\\file.png", result.CliArguments[0]);
        }

        [TestMethod]
        public void Classify_CliMultipleArguments_ReturnsCliWithAllArguments()
        {
            var result = LaunchArgumentsClassifier.Classify(["one.png", "two.png", "three.png"]);

            Assert.AreEqual(ClassificationMode.Cli, result.Mode);
            Assert.AreEqual(3, result.CliArguments.Count);
            Assert.AreEqual("one.png", result.CliArguments[0]);
            Assert.AreEqual("two.png", result.CliArguments[1]);
            Assert.AreEqual("three.png", result.CliArguments[2]);
        }

        [TestMethod]
        public void Classify_NumericCliArgumentWithoutRunnerFlag_RemainsCli()
        {
            var result = LaunchArgumentsClassifier.Classify(["1234"]);

            Assert.AreEqual(ClassificationMode.Cli, result.Mode);
            Assert.AreEqual(1, result.CliArguments.Count);
            Assert.AreEqual("1234", result.CliArguments[0]);
        }

        [TestMethod]
        public void Classify_FileNameThenNumericArgument_RemainsCliWithBothArguments()
        {
            var result = LaunchArgumentsClassifier.Classify(["image.png", "1234"]);

            Assert.AreEqual(ClassificationMode.Cli, result.Mode);
            Assert.AreEqual(2, result.CliArguments.Count);
            Assert.AreEqual("image.png", result.CliArguments[0]);
            Assert.AreEqual("1234", result.CliArguments[1]);
        }

        [TestMethod]
        public void Classify_RunnerPidFollowedByPath_ReturnsInvalidRunnerArguments()
        {
            var result = LaunchArgumentsClassifier.Classify(["--runner-pid", "1234", "image.png"]);

            Assert.AreEqual(ClassificationMode.InvalidRunnerArguments, result.Mode);
        }
    }
}
