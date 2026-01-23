// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Dock;

internal sealed partial class BandAlignmentConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public DockControl? Control { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ObservableCollection<DockItemViewModel> items && Control is not null)
        {
            return Control.GetBandAlignment(items);
        }

        return HorizontalAlignment.Center;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal sealed partial class BandAlignmentConverter2 : Microsoft.UI.Xaml.Data.IValueConverter
#pragma warning restore SA1402 // File may only contain a single type
{
    public DockControl? Control { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ObservableCollection<DockItemViewModel> items && Control is not null)
        {
            return Control.GetBandAlignment(items);
        }

        if (value is DockItemViewModel item && Control is not null)
        {
            return Control.GetItemAlignment(item);
        }

        return HorizontalAlignment.Center;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
