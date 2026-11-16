// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace RobocopyUI.Converters;

/// <summary>
/// Turns an AI service type into its branding icon. The icons are shared with Settings and
/// Advanced Paste, so a provider looks the same wherever it is listed.
/// </summary>
public sealed partial class ServiceTypeToIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        string? iconPath = value switch
        {
            string service when !string.IsNullOrWhiteSpace(service) => AIServiceTypeRegistry.GetIconPath(service),
            AIServiceType serviceType => AIServiceTypeRegistry.GetIconPath(serviceType),
            _ => null,
        };

        if (string.IsNullOrEmpty(iconPath))
        {
            iconPath = AIServiceTypeRegistry.GetIconPath(AIServiceType.Unknown);
        }

        try
        {
            return new SvgImageSource(new Uri(iconPath));
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Failed to create SvgImageSource for AI service icon", ex.Message);
            return null;
        }
    }

    public object? ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
