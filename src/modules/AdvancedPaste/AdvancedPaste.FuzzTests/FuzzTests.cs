// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Helpers;
using Windows.ApplicationModel.DataTransfer;

// OneFuzz currently does not support .NET 9 code testing, so this is a temporary solution.
// Create a .NET 8 project and use a file link to include the code for testing first.
namespace AdvancedPaste.FuzzTests
{
    public class FuzzTests
    {
        public static void FuzzToJsonFromXmlOrCsv(ReadOnlySpan<byte> input)
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(input.ToString());
                _ = Task.Run(async () => await JsonHelper.ToJsonFromXmlOrCsvAsync(dataPackage.GetView())).Result;
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
