// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;

namespace Microsoft.CmdPal.Common.UnitTests.Services;

[TestClass]
public sealed class ApplicationInfoServiceTests
{
    [TestMethod]
    public void LogFile_ReflectsDeferredInitializationAndCurrentPath()
    {
        var applicationInfo = new ApplicationInfoService();
        Assert.IsNull(applicationInfo.LogFile);

        string? logFile = null;
        applicationInfo.SetLogFile(() => logFile);
        Assert.IsNull(applicationInfo.LogFile);

        logFile = @"C:\Logs\first.log";
        Assert.AreEqual(logFile, applicationInfo.LogFile);

        logFile = @"C:\Logs\second.log";
        Assert.AreEqual(logFile, applicationInfo.LogFile);
    }
}
