// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class SettingsPageControl : UserControl
    {
        public SettingsPageControl()
        {
            this.InitializeComponent();
            ModuleLinks = new ObservableCollection<SidePanelLink>();
            AttributionLinks = new ObservableCollection<SidePanelLink>();
        }

        public string ModuleTitle
        {
            get { return (string)GetValue(ModuleTitleProperty); }
            set { SetValue(ModuleTitleProperty, value); }
        }

        public string ModuleDescription
        {
            get => (string)GetValue(ModuleDescriptionProperty);
            set => SetValue(ModuleDescriptionProperty, value);
        }

        public string ModuleImageSource
        {
            get => (string)GetValue(ModuleImageSourceProperty);
            set => SetValue(ModuleImageSourceProperty, value);
        }

        public Uri ModuleImageLink
        {
            get => (Uri)GetValue(ModuleImageLinkProperty);
            set => SetValue(ModuleImageLinkProperty, value);
        }

#pragma warning disable CA2227 // Collection properties should be read only
        public ObservableCollection<SidePanelLink> ModuleLinks
#pragma warning restore CA2227 // Collection properties should be read only
        {
            get => (ObservableCollection<SidePanelLink>)GetValue(ModuleLinksProperty);
            set => SetValue(ModuleLinksProperty, value);
        }

#pragma warning disable CA2227 // Collection properties should be read only
        public ObservableCollection<SidePanelLink> AttributionLinks
#pragma warning restore CA2227 // Collection properties should be read only
        {
            get => (ObservableCollection<SidePanelLink>)GetValue(AttributionLinksProperty);
            set => SetValue(AttributionLinksProperty, value);
        }

        public object ModuleContent
        {
            get { return (object)GetValue(ModuleContentProperty); }
            set { SetValue(ModuleContentProperty, value); }
        }

        public static readonly DependencyProperty ModuleTitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty ModuleDescriptionProperty = DependencyProperty.Register("ModuleDescription", typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty ModuleImageSourceProperty = DependencyProperty.Register("ModuleImageSource", typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty ModuleImageLinkProperty = DependencyProperty.Register("ModuleImageLink", typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty ModuleLinksProperty = DependencyProperty.Register("ModuleLinks", typeof(ObservableCollection<SidePanelLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<SidePanelLink>()));
        public static readonly DependencyProperty AttributionLinksProperty = DependencyProperty.Register("AttributionLinks", typeof(ObservableCollection<SidePanelLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<SidePanelLink>()));
        public static readonly DependencyProperty ModuleContentProperty = DependencyProperty.Register("ModuleContent", typeof(object), typeof(SettingsPageControl), new PropertyMetadata(new Grid()));
    }
}
