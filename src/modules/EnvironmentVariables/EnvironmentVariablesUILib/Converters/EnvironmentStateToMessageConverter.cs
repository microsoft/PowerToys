// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using EnvironmentVariablesUILib.Helpers;
using EnvironmentVariablesUILib.Models;
using Microsoft.UI.Xaml.Data;

namespace EnvironmentVariablesUILib.Converters;

public class EnvironmentStateToMessageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var resourceLoader = ResourceLoaderInstance.ResourceLoader;
        var type = (EnvironmentState)value;
        return type switch
        {
            EnvironmentState.Unchanged => string.Empty,
            EnvironmentState.ChangedOnStartup => resourceLoader.GetString("StateNotUpToDateOnStartupMsg"),
            EnvironmentState.EnvironmentMessageReceived => resourceLoader.GetString("StateNotUpToDateEnvironmentMessageReceivedMsg"),
            EnvironmentState.ProfileNotApplicable => resourceLoader.GetString("StateProfileNotApplicableMsg"),
            _ => throw new NotImplementedException(),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
