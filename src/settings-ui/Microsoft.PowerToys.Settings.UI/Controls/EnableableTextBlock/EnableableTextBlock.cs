using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    public class EnableableTextBlock : Control
    {
        private EnableableTextBlock _enableableTextBlock;

        public EnableableTextBlock()
        {
            this.DefaultStyleKey = typeof(EnableableTextBlock);
        }

        protected override void OnApplyTemplate()
        {
            IsEnabledChanged -= EnableableTextBlock_IsEnabledChanged;
            _enableableTextBlock = (EnableableTextBlock)this;
            Update();
            SetEnabledState();
            IsEnabledChanged += EnableableTextBlock_IsEnabledChanged;
            base.OnApplyTemplate();
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
           "Text",
           typeof(string),
           typeof(TextBlockControl),
           null);

        [Localizable(true)]
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private void Update()
        {
            if (_enableableTextBlock == null)
            {
                return;
            }
        }

        private void EnableableTextBlock_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetEnabledState();
        }

        private void SetEnabledState()
        {
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }
    }
}
