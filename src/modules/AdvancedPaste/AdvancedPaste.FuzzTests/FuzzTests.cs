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
            // Decode the fuzzer-provided bytes as UTF-8 text. Previously we called
            // `input.ToString()`, which on ReadOnlySpan<byte> returns the type name
            // (e.g. "System.ReadOnlySpan<Byte>[N]") rather than the bytes — the
            // helper was effectively never being fuzzed.
            string text = Encoding.UTF8.GetString(input);

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);

            // `Task.Run(...).Result` wraps any thrown exception in AggregateException,
            // which causes `when (ex is SomeException)` filters to never match (the
            // outer type is always AggregateException). Use GetAwaiter().GetResult()
            // so the original exception type propagates and the filter below works
            // as intended. This also fixes the crashes reported by OneFuzz
            // (ThrowIfExceptional in Task.GetResultCore).
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
