// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class SettingsSearchPage : Page
{
    private static readonly CompositeFormat ResultsQueryFormat = CompositeFormat.Parse(RS_.GetString("SettingsWindow_SearchResultsQuery"));
    private Action<OpenSettingsMessage>? _navigate;

    internal string Query { get; private set; } = string.Empty;

    public SettingsSearchPage()
    {
        InitializeComponent();
    }

    internal void ShowResults(string query, SettingsSearchResult[] results, Action<OpenSettingsMessage> navigate)
    {
        _navigate = navigate;
        Query = query;
        QueryText.Text = string.Format(CultureInfo.CurrentCulture, ResultsQueryFormat, query);
        ResultsRepeater.ItemsSource = results;
        EmptyState.Visibility = results.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Result_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SettingsSearchResult { Destination: { } destination } })
        {
            _navigate?.Invoke(destination);
        }
    }
}
