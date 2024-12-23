// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using AdvancedPaste.Models;
using Microsoft.SemanticKernel;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Helpers;

internal static class KernelExtensions
{
    private const string DataPackageKey = "DataPackage";
    private const string LastErrorKey = "LastError";
    private const string ActionChainKey = "ActionChain";

    internal static DataPackageView GetDataPackageView(this Kernel kernel)
    {
        kernel.Data.TryGetValue(DataPackageKey, out object obj);
        return obj as DataPackageView ?? (obj as DataPackage)?.GetView();
    }

    internal static DataPackage GetDataPackage(this Kernel kernel)
    {
        kernel.Data.TryGetValue(DataPackageKey, out object obj);
        return obj as DataPackage ?? new();
    }

    internal static async Task<string> GetDataFormatsAsync(this Kernel kernel)
    {
        var clipboardFormats = await kernel.GetDataPackageView().GetAvailableFormatsAsync();
        return clipboardFormats.ToString();
    }

    internal static void SetDataPackage(this Kernel kernel, DataPackage dataPackage) => kernel.Data[DataPackageKey] = dataPackage;

    internal static void SetDataPackageView(this Kernel kernel, DataPackageView dataPackageView) => kernel.Data[DataPackageKey] = dataPackageView;

    internal static Exception GetLastError(this Kernel kernel) => kernel.Data.TryGetValue(LastErrorKey, out object obj) ? obj as Exception : null;

    internal static void SetLastError(this Kernel kernel, Exception error) => kernel.Data[LastErrorKey] = error;

    internal static List<ActionChainItem> GetOrAddActionChain(this Kernel kernel)
    {
        if (kernel.Data.TryGetValue(ActionChainKey, out var actionChainObj))
        {
            return (List<ActionChainItem>)actionChainObj;
        }
        else
        {
            List<ActionChainItem> actionChain = [];
            kernel.Data[ActionChainKey] = actionChain;
            return actionChain;
        }
    }
}
