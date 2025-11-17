// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor.Models
{
    public static class MonitorConfigurationTypeEnumExtensions
    {
        private const string HorizontalJsonTag = "horizontal";
        private const string VerticalJsonTag = "vertical";

        public static string TypeToString(this MonitorConfigurationType value)
        {
            switch (value)
            {
                case MonitorConfigurationType.Horizontal:
                    return HorizontalJsonTag;
                case MonitorConfigurationType.Vertical:
                    return VerticalJsonTag;
            }

            return HorizontalJsonTag;
        }

        public static MonitorConfigurationType TypeFromString(string value)
        {
            switch (value)
            {
                case HorizontalJsonTag:
                    return MonitorConfigurationType.Horizontal;
                case VerticalJsonTag:
                    return MonitorConfigurationType.Vertical;
            }

            return MonitorConfigurationType.Horizontal;
        }
    }
}
