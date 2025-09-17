// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
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

        public Uri ModuleImageSource
        {
            get => (Uri)GetValue(ModuleImageSourceProperty);
            set => SetValue(ModuleImageSourceProperty, value);
        }

        public ObservableCollection<PageLink> PrimaryLinks
        {
            get => (ObservableCollection<PageLink>)GetValue(PrimaryLinksProperty);
            set => SetValue(PrimaryLinksProperty, value);
        }

        public string SecondaryLinksHeader
        {
            get { return (string)GetValue(SecondaryLinksHeaderProperty); }
            set { SetValue(SecondaryLinksHeaderProperty, value); }
        }

        public ObservableCollection<PageLink> SecondaryLinks
        {
            get => (ObservableCollection<PageLink>)GetValue(SecondaryLinksProperty);
            set => SetValue(SecondaryLinksProperty, value);
        }

        public object ModuleContent
        {
            get { return (object)GetValue(ModuleContentProperty); }
            set { SetValue(ModuleContentProperty, value); }
        }

        public static readonly DependencyProperty ModuleTitleProperty = DependencyProperty.Register(nameof(ModuleTitle), typeof(string), typeof(SettingsPageControl), new PropertyMetadata(defaultValue: null));
        public static readonly DependencyProperty ModuleDescriptionProperty = DependencyProperty.Register(nameof(ModuleDescription), typeof(string), typeof(SettingsPageControl), new PropertyMetadata(defaultValue: null));
        public static readonly DependencyProperty ModuleImageSourceProperty = DependencyProperty.Register(nameof(ModuleImageSource), typeof(Uri), typeof(SettingsPageControl), new PropertyMetadata(null));
        public static readonly DependencyProperty PrimaryLinksProperty = DependencyProperty.Register(nameof(PrimaryLinks), typeof(ObservableCollection<PageLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<PageLink>()));
        public static readonly DependencyProperty SecondaryLinksHeaderProperty = DependencyProperty.Register(nameof(SecondaryLinksHeader), typeof(string), typeof(SettingsPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty SecondaryLinksProperty = DependencyProperty.Register(nameof(SecondaryLinks), typeof(ObservableCollection<PageLink>), typeof(SettingsPageControl), new PropertyMetadata(new ObservableCollection<PageLink>()));
        public static readonly DependencyProperty ModuleContentProperty = DependencyProperty.Register(nameof(ModuleContent), typeof(object), typeof(SettingsPageControl), new PropertyMetadata(new Grid()));

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // PrimaryLinksControl.Focus(FocusState.Programmatic);
            var requestUrl = "https://raw.githubusercontent.com/MicrosoftDocs/windows-dev-docs/refs/heads/docs/hub/powertoys/advanced-paste.md";

            using var client = new HttpClient();
            var response = await client.GetAsync(requestUrl);
            string content = await response.Content.ReadAsStringAsync();
            docsTextBlock.Text = PreprocessMarkdown(content);
        }

        public static string PreprocessMarkdown(string markdown)
        {
            markdown = Regex.Replace(markdown, @"\A---\n[\s\S]*?---\n", string.Empty, RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^>\s*\[!IMPORTANT\]\s*> - Phi Silica is not available in China.\s*", string.Empty, RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^>\s*\[!IMPORTANT\]", "> **ℹ️ Important:**", RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^>\s*\[!NOTE\]", "> **❗ Note:**", RegexOptions.Multiline);
            markdown = Regex.Replace(markdown, @"^>\s*\[!TIP\]", "> **💡 Tip:**", RegexOptions.Multiline);

            return markdown;
        }
    }
}
