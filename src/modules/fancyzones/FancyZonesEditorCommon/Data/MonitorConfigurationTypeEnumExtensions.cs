// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditorCommon.Data
{
    public enum MonitorConfigurationType
    {
        Horizontal = 0,
        Vertical,
    }

    public static class MonitorConfigurationTypeEnumExtensions
    {
        private const string HorizontalJsonTag = "horizontal";
        private const string VerticalJsonTag = "vertical";

        public static string MonitorConfigurationTypeToString(this MonitorConfigurationType value)
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

        public static MonitorConfigurationType GetTypeFromString(string value)
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
