// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Data;
using WorkspacesEditor.Helpers;

namespace WorkspacesEditor.Converters
{
    /// <summary>
    /// Converts a workspace name to a contextual button label like "Launch MyWorkspace".
    /// </summary>
    public sealed partial class LaunchButtonNameConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            string name = value as string ?? string.Empty;
            string launchStr = ResourceLoaderInstance.ResourceLoader?.GetString("Launch") ?? "Launch";
            return $"{launchStr} {name}";
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language)
        {
            throw new System.NotImplementedException();
        }
    }
}
