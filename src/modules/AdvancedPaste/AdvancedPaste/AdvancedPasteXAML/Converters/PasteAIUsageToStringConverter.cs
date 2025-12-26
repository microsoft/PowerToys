// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using AdvancedPaste.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Data;

namespace AdvancedPaste.Converters;

public sealed partial class PasteAIUsageToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var usage = value switch
        {
            string s => PasteAIUsageExtensions.FromConfigString(s),
            PasteAIUsage u => u,
            _ => PasteAIUsage.ChatCompletion,
        };

        return ResourceLoaderInstance.ResourceLoader.GetString($"PasteAIUsage_{usage}_Label");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
