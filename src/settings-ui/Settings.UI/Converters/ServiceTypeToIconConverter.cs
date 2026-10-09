// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.PowerToys.Settings.UI.Converters;

public partial class ServiceTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string serviceType || string.IsNullOrWhiteSpace(serviceType))
        {
            return new ImageIcon { Source = new SvgImageSource(new Uri(AIServiceTypeRegistry.GetIconPath(AIServiceType.OpenAI))) };
        }

        var iconPath = AIServiceTypeRegistry.GetIconPath(serviceType);
        return new ImageIcon { Source = new SvgImageSource(new Uri(iconPath)) };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
