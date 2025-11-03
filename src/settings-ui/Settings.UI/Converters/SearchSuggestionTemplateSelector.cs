// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Converters;

public sealed partial class SearchSuggestionTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultSuggestionTemplate { get; set; }

    public DataTemplate NoResultsSuggestionTemplate { get; set; }

    public DataTemplate ShowAllSuggestionTemplate { get; set; }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is SuggestionItem suggestionItem)
        {
            if (suggestionItem.IsNoResults)
            {
                return NoResultsSuggestionTemplate;
            }

            if (suggestionItem.IsShowAll)
            {
                return ShowAllSuggestionTemplate ?? NoResultsSuggestionTemplate ?? DefaultSuggestionTemplate;
            }

            return DefaultSuggestionTemplate;
        }

        return DefaultSuggestionTemplate;
    }
}
