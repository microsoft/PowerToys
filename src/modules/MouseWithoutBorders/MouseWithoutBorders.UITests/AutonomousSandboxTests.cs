// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]

namespace Microsoft.MouseWithoutBorders.UITests;

[TestClass]
public sealed class AutonomousSandboxTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("MouseWithoutBorders")]
    [TestCategory("NestedSandboxDebugPilot")]
    public void AutonomousSandboxSmoke()
    {
        using var fixture = new TwoEndpointFixture(TestContext);
        Exception? failure = null;
        try
        {
            fixture.Run();
        }
        catch (Exception error)
        {
            failure = error;
        }

        var cleanupErrors = fixture.Cleanup();
        if (failure is not null || cleanupErrors.Count > 0)
        {
            var errors = new List<Exception>(cleanupErrors);
            if (failure is not null)
            {
                errors.Insert(0, failure);
            }

            throw new AggregateException("Autonomous MWB smoke did not complete cleanly; see attached phase and cleanup journals.", errors);
        }
    }
}
