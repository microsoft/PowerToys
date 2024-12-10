// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Helpers;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.FuzzTests
{
    public class FuzzTests
    {
        public static void FuzzToJsonFromXmlOrCsv(ReadOnlySpan<byte> input)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(input.ToString());
            JsonHelper.ToJsonFromXmlOrCsv(dataPackage.GetView());
        }
    }
}
