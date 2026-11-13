// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace PowerScripts.Core.Tests;

[TestClass]
[DoNotParallelize]
public sealed class HostDiscoveryTests
{
    [TestMethod]
    public void InstalledWinUIConsumerFindsSiblingHostAndPreservesOverridePrecedence()
    {
        var root = Path.Combine(Path.GetTempPath(), "ps-installed-host-" + Guid.NewGuid().ToString("N"));
        var application = Directory.CreateDirectory(Path.Combine(root, "WinUI3Apps")).FullName;
        var host = Path.Combine(Directory.CreateDirectory(Path.Combine(root, "PowerScripts")).FullName, "PowerScripts.Host.exe");
        var previousOverride = Environment.GetEnvironmentVariable("POWERSCRIPTS_HOST");
        try
        {
            Environment.SetEnvironmentVariable("POWERSCRIPTS_HOST", null);
            File.WriteAllBytes(host, []);
            Assert.AreEqual(host, PowerScriptsClient.ResolveHostPath(application));

            var adjacent = Path.Combine(application, "PowerScripts.Host.exe");
            File.WriteAllBytes(adjacent, []);
            Assert.AreEqual(adjacent, PowerScriptsClient.ResolveHostPath(application));

            Environment.SetEnvironmentVariable("POWERSCRIPTS_HOST", host);
            Assert.AreEqual(host, PowerScriptsClient.ResolveHostPath(application));
        }
        finally
        {
            Environment.SetEnvironmentVariable("POWERSCRIPTS_HOST", previousOverride);
            Directory.Delete(root, recursive: true);
        }
    }
}
