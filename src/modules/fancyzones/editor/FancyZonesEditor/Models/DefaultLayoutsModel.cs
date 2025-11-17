// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FancyZonesEditor.Models
{
    public class DefaultLayoutsModel : INotifyPropertyChanged
    {
        private static int Count { get; } = Enum.GetValues(typeof(MonitorConfigurationType)).Length;

        public Dictionary<MonitorConfigurationType,  LayoutModel> Layouts { get; } = new Dictionary<MonitorConfigurationType, LayoutModel>(Count);

        public DefaultLayoutsModel()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Reset(MonitorConfigurationType type)
        {
            switch (type)
            {
                case MonitorConfigurationType.Horizontal:
                    Set(MainWindowSettingsModel.TemplateModels[(int)LayoutType.PriorityGrid], type);
                    break;
                case MonitorConfigurationType.Vertical:
                    Set(MainWindowSettingsModel.TemplateModels[(int)LayoutType.Rows], type);
                    break;
            }
        }

        public void Reset(string uuid)
        {
            if (Layouts[MonitorConfigurationType.Horizontal].Uuid == uuid)
            {
                Set(MainWindowSettingsModel.TemplateModels[(int)LayoutType.PriorityGrid], MonitorConfigurationType.Horizontal);
            }

            if (Layouts[MonitorConfigurationType.Vertical].Uuid == uuid)
            {
                Set(MainWindowSettingsModel.TemplateModels[(int)LayoutType.Rows], MonitorConfigurationType.Vertical);
            }
        }

        public void Set(LayoutModel layout, MonitorConfigurationType type)
        {
            Layouts[type] = layout;

            FirePropertyChanged();
        }

        public void Restore(Dictionary<MonitorConfigurationType, LayoutModel> layouts)
        {
            foreach (var monitorConfigurationType in layouts.Keys)
            {
                Set(layouts[monitorConfigurationType], monitorConfigurationType);
            }
        }

        protected virtual void FirePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
