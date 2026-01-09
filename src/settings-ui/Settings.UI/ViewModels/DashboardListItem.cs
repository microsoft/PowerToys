// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.UI;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class DashboardListItem : ModuleListItem
    {
        private bool _visible;

        public string ToolTip { get; set; }

        public new ModuleType Tag
        {
            get => (ModuleType)base.Tag!;
            set => base.Tag = value;
        }

        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<DashboardModuleItem> DashboardModuleItems { get; set; } = new ObservableCollection<DashboardModuleItem>();
    }
}
