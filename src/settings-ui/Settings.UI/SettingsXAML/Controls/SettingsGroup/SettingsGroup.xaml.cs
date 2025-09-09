// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    /// <summary>
    /// Represents a control that can contain multiple settings (or other) controls
    /// </summary>
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    [TemplatePart(Name = PartDescriptionPresenter, Type = typeof(ContentPresenter))]
    public partial class SettingsGroup : ItemsControl
    {
        private const string PartDescriptionPresenter = "DescriptionPresenter";
        private ContentPresenter _descriptionPresenter;
        private SettingsGroup _settingsGroup;

        public SettingsGroup()
        {
            DefaultStyleKey = typeof(SettingsGroup);
            UpdateTabStop();
            
            // Handle focus state changes for visual feedback
            GotFocus += OnGotFocus;
            LostFocus += OnLostFocus;
        }

        [Localizable(true)]
        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            "Header",
            typeof(string),
            typeof(SettingsGroup),
            new PropertyMetadata(default(string), OnHeaderChanged));

        [Localizable(true)]
        public object Description
        {
            get => (object)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            "Description",
            typeof(object),
            typeof(SettingsGroup),
            new PropertyMetadata(null, OnDescriptionChanged));

        protected override void OnApplyTemplate()
        {
            IsEnabledChanged -= SettingsGroup_IsEnabledChanged;
            _settingsGroup = (SettingsGroup)this;
            _descriptionPresenter = (ContentPresenter)_settingsGroup.GetTemplateChild(PartDescriptionPresenter);
            SetEnabledState();
            IsEnabledChanged += SettingsGroup_IsEnabledChanged;
            base.OnApplyTemplate();
        }

        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsGroup)d).Update();
        }

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsGroup)d).UpdateTabStop();
        }

        private void SettingsGroup_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetEnabledState();
        }

        private void SetEnabledState()
        {
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }

        private void Update()
        {
            if (_settingsGroup == null)
            {
                return;
            }

            if (_settingsGroup.Description == null)
            {
                _settingsGroup._descriptionPresenter.Visibility = Visibility.Collapsed;
            }
            else
            {
                _settingsGroup._descriptionPresenter.Visibility = Visibility.Visible;
            }
        }

        private void UpdateTabStop()
        {
            // Make the SettingsGroup focusable only when it has a header
            // This allows keyboard navigation to group headers in search results
            IsTabStop = !string.IsNullOrEmpty(Header);
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (IsTabStop)
            {
                VisualStateManager.GoToState(this, "Focused", true);
            }
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "Unfocused", true);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new SettingsGroupAutomationPeer(this);
        }
    }
}
