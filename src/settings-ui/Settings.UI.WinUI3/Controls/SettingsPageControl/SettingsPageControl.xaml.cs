// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.WinUI3.Controls
{
    public sealed partial class SettingsPageControl : UserControl
    {
        public SettingsPageControl()
        {
            this.InitializeComponent();
            PrimaryLinks = new ObservableCollection<PageLink>();
            SecondaryLinks = new ObservableCollection<PageLink>();
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

#pragma warning disable CA2227 // Collection properties should be read only
        public ObservableCollection<PageLink> PrimaryLinks
#pragma warning restore CA2227 // Collection properties should be read only
        {
            get => (ObservableCollection<PageLink>)GetValue(PrimaryLinksProperty);
            set => SetValue(PrimaryLinksProperty, value);
        }

        public string SecondaryLinksHeader
        {
            get { return (string)GetValue(SecondaryLinksHeaderProperty); }
            set { SetValue(SecondaryLinksHeaderProperty, value); }
        }

#pragma warning disable CA2227 // Collection properties should be read only
        public ObservableCollection<PageLink> SecondaryLinks
#pragma warning restore CA2227 // Collection properties should be read only
        {
            get => (ObservableCollection<PageLink>)GetValue(SecondaryLinksProperty);
            set => SetValue(SecondaryLinksProperty, value);
        }

        public object ModuleContent
        {
            get { return (object)GetValue(ModuleContentProperty); }
            set { SetValue(ModuleContentProperty, value); }
        }

        public static readonly DependencyProperty ModuleTitleProperty = DependencyProperty.Register("ModuleTitle", typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty ModuleDescriptionProperty = DependencyProperty.Register("ModuleDescription", typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty ModuleImageSourceProperty = DependencyProperty.Register("ModuleImageSource", typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty PrimaryLinksProperty = DependencyProperty.Register("PrimaryLinks", typeof(ObservableCollection<PageLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<PageLink>()));
        public static readonly DependencyProperty SecondaryLinksHeaderProperty = DependencyProperty.Register("SecondaryLinksHeader", typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty SecondaryLinksProperty = DependencyProperty.Register("SecondaryLinks", typeof(ObservableCollection<PageLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<PageLink>()));
        public static readonly DependencyProperty ModuleContentProperty = DependencyProperty.Register("ModuleContent", typeof(object), typeof(SettingsPageControl), new PropertyMetadata(new Grid()));

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            PrimaryLinksControl.Focus(FocusState.Programmatic);
        }
    }
}
