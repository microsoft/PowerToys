// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MouseJump.Common.Telemetry;

namespace MouseJump.Common.UnitTests.Telemetry;

[TestClass]
public sealed class TelemetryContextTests
{
    [TestMethod]
    public void AScopeWritesNoRecordOfItsOwnButItsPropertiesReachRecordsInsideIt()
    {
        // a uniquely-named source, so no other test's listener can see (or add to) these records
        var telemetry = TelemetryContext.Create($"{nameof(TelemetryContextTests)}.{Guid.NewGuid()}");
        var writer = new InMemoryTelemetryWriter();
        telemetry.Start(writer);

        using (telemetry.BeginScope(new { activation = 42 }, "outerScope"))
        {
            telemetry.WriteEvent(new { step = "inside" }, "innerEvent");
        }

        telemetry.Stop();

        var record = writer.Records.Single();
        Assert.AreEqual("innerEvent", record.Operation);
        Assert.AreEqual("event", record.Kind);
        Assert.AreEqual(42, record.Properties["activation"]);
        Assert.AreEqual("inside", record.Properties["step"]);
    }
}
