// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Awake.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class ProcessMonitoringResourceTests
{
    [TestMethod]
    public void MonitoringFailureResource_DescribesMonitoringInsteadOfMissingProcess()
    {
        string message = Resources.ResourceManager.GetString("AWAKE_EXIT_PROCESS_MONITORING_FAILURE_MESSAGE", CultureInfo.InvariantCulture);

        Assert.AreEqual("Exiting because the requested process could not be monitored.", message);
        Assert.AreNotEqual(Resources.AWAKE_EXIT_PROCESS_BINDING_FAILURE_MESSAGE, message);
    }
}
