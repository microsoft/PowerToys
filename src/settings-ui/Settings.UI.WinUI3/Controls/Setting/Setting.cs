// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.WinUI3.Controls
{
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    [TemplatePart(Name = PartIconPresenter, Type = typeof(ContentPresenter))]
    [TemplatePart(Name = PartDescriptionPresenter, Type = typeof(ContentPresenter))]
    public class Setting : ContentControl
    {
        private const string PartIconPresenter = "IconPresenter";
        private const string PartDescriptionPresenter = "DescriptionPresenter";
        private ContentPresenter _iconPresenter;
        private ContentPresenter _descriptionPresenter;
        private Setting _setting;

        public Setting()
        {
            this.DefaultStyleKey = typeof(Setting);
        }

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
           "Header",
           typeof(string),
           typeof(Setting),
           new PropertyMetadata(default(string), OnHeaderChanged));

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            "Description",
            typeof(object),
            typeof(Setting),
            new PropertyMetadata(null, OnDescriptionChanged));

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            "Icon",
            typeof(object),
            typeof(Setting),
            new PropertyMetadata(default(string), OnIconChanged));

        public static readonly DependencyProperty ActionContentProperty = DependencyProperty.Register(
            "ActionContent",
            typeof(object),
            typeof(Setting),
            null);

        [Localizable(true)]
        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        [Localizable(true)]
        public object Description
        {
            get => (object)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public object Icon
        {
            get => (object)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public object ActionContent
        {
            get => (object)GetValue(ActionContentProperty);
            set => SetValue(ActionContentProperty, value);
        }

        protected override void OnApplyTemplate()
        {
            IsEnabledChanged -= Setting_IsEnabledChanged;
            _setting = (Setting)this;
            _iconPresenter = (ContentPresenter)_setting.GetTemplateChild(PartIconPresenter);
            _descriptionPresenter = (ContentPresenter)_setting.GetTemplateChild(PartDescriptionPresenter);
            Update();
            SetEnabledState();
            IsEnabledChanged += Setting_IsEnabledChanged;
            base.OnApplyTemplate();
        }

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Setting)d).Update();
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Setting)d).Update();
        }

        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Setting)d).Update();
        }

        private void Setting_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetEnabledState();
        }

        private void SetEnabledState()
        {
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }

        private void Update()
        {
            if (_setting == null)
            {
                return;
            }

            if (_setting.ActionContent != null)
            {
                if (_setting.ActionContent.GetType() != typeof(Button))
                {
                    // We do not want to override the default AutomationProperties.Name of a button. Its Content property already describes what it does.
                    if (!string.IsNullOrEmpty(_setting.Header))
                    {
                        AutomationProperties.SetName((UIElement)_setting.ActionContent, _setting.Header);
                    }
                }
            }

            if (_setting._iconPresenter != null)
            {
                if (_setting.Icon == null)
                {
                    _setting._iconPresenter.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _setting._iconPresenter.Visibility = Visibility.Visible;
                }
            }

            if (_setting.Description == null)
            {
                _setting._descriptionPresenter.Visibility = Visibility.Collapsed;
            }
            else
            {
                _setting._descriptionPresenter.Visibility = Visibility.Visible;
            }
        }
    }
}
