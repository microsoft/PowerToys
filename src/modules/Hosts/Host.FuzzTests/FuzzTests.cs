// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Hosts.FuzzTests
{
    public class FuzzTests
    {
        public static void FuzzTargetMethod(ReadOnlySpan<byte> input)
        {
            try
            {
                // … use input parameter in code under test …
                //
                // TargetMethod(…);
            }
            catch (Exception ex) when (ex is ArgumentException)
            {
                // This is an example. It's important to filter out any *expected* exceptions from our code here.
                // However, catching all exceptions is considered an anti-pattern because it may suppress legitimate
                // issues, such as a NullReferenceException thrown by our code. In this case, we still re-throw
                // the exception, as the ToJsonFromXmlOrCsvAsync method is not expected to throw any exceptions.
                throw;
            }
        }
    }
}
