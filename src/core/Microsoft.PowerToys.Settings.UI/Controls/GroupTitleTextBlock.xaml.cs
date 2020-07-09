using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class GroupTitleTextBlock : UserControl
    {
        public string _text;

        public string Text
        {
            get
            {
                return _text;
            }

            set
            {
                _text = value;
                CustomTextBlock.Text = value;
            }
        }

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                "IsActive",
                typeof(string),
                typeof(GroupTitleTextBlock),
                null);

        private string _isActive = "False";

        public string IsActive
        {
            get
            {
                return _isActive;
            }

            set
            {
                SetValue(IsActiveProperty, value.ToString());
                _isActive = value.ToString();
                CustomTextBlock.Tag = value.ToString();
            }
        }

        public GroupTitleTextBlock()
        {
            this.InitializeComponent();
            DataContext = this;
            CustomTextBlock.Tag = "False";
        }
    }
}
