// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;

namespace WorkspacesLauncherUI.UnitTests;

[TestClass]
public sealed class WorkspacesDataSerializationTests
{
    [TestMethod]
    public void WorkspacesData_DeserializesInitOnlyCollections()
    {
        const string Json = """
            {
              "workspaces": [
                {
                  "id": "workspace-id",
                  "name": "Workspace",
                  "monitor-configuration": [{ "id": "monitor-id" }],
                  "applications": [{ "id": "application-id" }]
                }
              ]
            }
            """;

        var result = new WorkspacesData().Deserialize(Json);

        Assert.HasCount(1, result.Workspaces);
        Assert.HasCount(1, result.Workspaces[0].MonitorConfiguration);
        Assert.HasCount(1, result.Workspaces[0].Applications);
        Assert.AreEqual("monitor-id", result.Workspaces[0].MonitorConfiguration[0].Id);
        Assert.AreEqual("application-id", result.Workspaces[0].Applications[0].Id);
    }
}
