// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace PowerOCR.Services;

internal interface IClipboardService
{
    Task SetTextAsync(string text);
}
