// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// Used for STA tests in PreviewPane
namespace Microsoft.PowerToys.STATestExtension
{
    public class STATestMethodAttribute : TestMethodAttribute
    {
        private readonly TestMethodAttribute _testMethodAttribute;

        public STATestMethodAttribute()
        {
        }

        public STATestMethodAttribute(TestMethodAttribute testMethodAttribute)
        {
            _testMethodAttribute = testMethodAttribute;
        }

        public override TestResult[] Execute(ITestMethod testMethod)
        {
            ArgumentNullException.ThrowIfNull(testMethod);

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                return Invoke(testMethod);
            }

            TestResult[] result = null;
            var thread = new Thread(() => result = Invoke(testMethod));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }

        private TestResult[] Invoke(ITestMethod testMethod)
        {
            if (_testMethodAttribute != null)
            {
                return _testMethodAttribute.Execute(testMethod);
            }

            return new[] { testMethod.Invoke(null) };
        }
    }
}
