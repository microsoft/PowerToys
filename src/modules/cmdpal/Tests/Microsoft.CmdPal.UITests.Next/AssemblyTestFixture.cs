// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public sealed class AssemblyTestFixture
{
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        var initialized = false;
        try
        {
            IndexerTests.CreateIndexedFixture(context);
            ModuleLifecycleTests.PrepareCommandPalettePackage(context);
            initialized = true;
        }
        finally
        {
            if (!initialized)
            {
                Cleanup();
            }
        }
    }

    [AssemblyCleanup]
    public static void Cleanup()
    {
        try
        {
            ModuleLifecycleTests.RestoreCommandPalettePackage();
        }
        finally
        {
            IndexerTests.RemoveIndexedFixture();
        }
    }
}
