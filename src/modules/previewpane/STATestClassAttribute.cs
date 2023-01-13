// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.VisualStudio.TestTools.UnitTesting;

// Used for STA tests in PreviewPane
namespace Microsoft.PowerToys.STATestExtension
{
    public class STATestClassAttribute : TestClassAttribute
    {
        public override TestMethodAttribute GetTestMethodAttribute(TestMethodAttribute testMethodAttribute)
        {
            if (testMethodAttribute is STATestMethodAttribute)
            {
                return testMethodAttribute;
            }

            return new STATestMethodAttribute(base.GetTestMethodAttribute(testMethodAttribute));
        }
    }
}
