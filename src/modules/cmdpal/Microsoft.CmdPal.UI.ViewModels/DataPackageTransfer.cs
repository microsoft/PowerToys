// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class DataPackageTransfer
{
    public static void Copy(DataPackageView source, DataPackage destination)
    {
        destination.RequestedOperation = source.RequestedOperation;

        foreach (var (key, value) in source.Properties)
        {
            try
            {
                destination.Properties[key] = value;
            }
            catch (Exception)
            {
                // Skip properties that cannot be copied into the drag data package.
            }
        }

        foreach (var format in source.AvailableFormats)
        {
            try
            {
                destination.SetDataProvider(format, request => DelayRenderer(request, source, format));
            }
            catch (Exception)
            {
                // Skip formats that cannot be registered on the drag data package.
            }
        }
    }

    private static void DelayRenderer(DataProviderRequest request, DataPackageView source, string format)
    {
        var deferral = request.GetDeferral();
        try
        {
            source.GetDataAsync(format)
                .AsTask()
                .ContinueWith(dataTask =>
                {
                    try
                    {
                        if (dataTask.IsCompletedSuccessfully)
                        {
                            request.SetData(dataTask.Result);
                        }
                        else if (dataTask.IsFaulted && dataTask.Exception is not null)
                        {
                            Logger.LogError($"Failed to get data for format '{format}' during drag-and-drop", dataTask.Exception);
                        }
                    }
                    finally
                    {
                        deferral.Complete();
                    }
                });
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to set data for format '{format}' during drag-and-drop", ex);
            deferral.Complete();
        }
    }
}
