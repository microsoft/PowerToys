// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

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
            // Decode the input bytes as UTF-8 text. `ReadOnlySpan<byte>.ToString()`
            // returns the type name (e.g. "System.ReadOnlySpan<Byte>[N]") rather
            // than the bytes, so an explicit decode is required to actually exercise
            // the helper with the provided input.
            string text = Encoding.UTF8.GetString(input);

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);

            // Use GetAwaiter().GetResult() so any thrown exception surfaces with its
            // original type. `Task.Run(...).Result` wraps thrown exceptions in an
            // AggregateException, which would prevent the
            // `when (ex is ArgumentException)` filter below from matching.
            try
            {
                _ = Task.Run(async () => await JsonHelper.ToJsonFromXmlOrCsvAsync(dataPackage.GetView())).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is ArgumentException)
            {
                // It's important to filter out any *expected* exceptions from our code here.
                // However, catching all exceptions is considered an anti-pattern because it may suppress legitimate
                // issues, such as a NullReferenceException thrown by our code. In this case, we still re-throw
                // the exception, as the ToJsonFromXmlOrCsvAsync method is not expected to throw any exceptions.
                throw;
            }
        }
    }
}
