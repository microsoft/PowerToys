// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class OOBEPageControl : UserControl
    {
        public OOBEPageControl()
        {
            this.InitializeComponent();
        }

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public ImageSource HeroImage
        {
            get => (ImageSource)GetValue(HeroImageProperty);
            set => SetValue(HeroImageProperty, value);
        }

        public double HeroImageHeight
        {
            get { return (double)GetValue(HeroImageHeightProperty); }
            set { SetValue(HeroImageHeightProperty, value); }
        }

        public object PageContent
        {
            get { return (object)GetValue(PageContentProperty); }
            set { SetValue(PageContentProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(OOBEPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register("Description", typeof(string), typeof(OOBEPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty HeroImageProperty = DependencyProperty.Register("HeroImage", typeof(ImageSource), typeof(OOBEPageControl), new PropertyMetadata(default(ImageSource)));
        public static readonly DependencyProperty PageContentProperty = DependencyProperty.Register("PageContent", typeof(object), typeof(OOBEPageControl), new PropertyMetadata(new Grid()));
        public static readonly DependencyProperty HeroImageHeightProperty = DependencyProperty.Register("HeroImageHeight", typeof(double), typeof(OOBEPageControl), new PropertyMetadata(280.0));
    }
}
