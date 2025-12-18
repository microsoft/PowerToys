// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Models;

public class GeneratedResponse
{
    public ClipboardItem Preview { get; set; }

    public DataPackage Data { get; set; }
}
