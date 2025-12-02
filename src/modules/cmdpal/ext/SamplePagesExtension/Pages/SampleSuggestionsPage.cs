// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace SamplePagesExtension.Pages;

#nullable enable

#pragma warning disable SA1402 // File may only contain a single type
internal sealed partial class SampleSuggestionsPage : DynamicListPage, IExtendedAttributesProvider
{
    private PeopleSearchPage _peopleSearchPage = new();
    private CommandsListPage _commandsListPage = new();
    private DynamicListPage? _suggestionPage;
    private List<MyTokenType> _pickedTokens = new();
    private int _lastPrefixPosition = -1;
    private ListItem _queryItem;

    // private int _lastCaretPosition = 0;
    private string _searchText = string.Empty;

    public override string SearchText
    {
        get => _searchText;
        set
        {
            var oldSearch = _searchText;
            if (value != oldSearch)
            {
                _searchText = value;
                UpdateSearch(oldSearch, new SearchUpdateArgs(value, null));
            }
        }
    }

    internal SampleSuggestionsPage()
    {
        _peopleSearchPage.SuggestionPicked += OnSuggestionPicked;
        _commandsListPage.SuggestionPicked += OnSuggestionPicked;
        Name = "Open";
        Title = "Sample prefixed search";
        PlaceholderText = "Type a query, and use '@' to add a person";
        Icon = new("\uE779");

        _queryItem = new ListItem(new NoOpCommand())
        {
            Title = string.Empty,
            Icon = new IconInfo("\uE8F2"), // ChatBubbles
        };
    }

    public override IListItem[] GetItems()
    {
        return _suggestionPage?.GetItems() ??
            (string.IsNullOrEmpty(this.SearchText) ?
                [] :
                [_queryItem]);
    }

    public void UpdateSearch(string oldSearchText, ISearchUpdateArgs args)
    {
        // if (args.GetProperties() is IDictionary<string, object> props)
        // {
        //     if (props.TryGetValue("CaretPosition", out var caretPosObj) && caretPosObj is int caretPos)
        //     {
        //         _lastCaretPosition = caretPos;
        //     }
        // }
        var newSearchText = args.NewSearchText;
        UpdateListItem(newSearchText);
        if (string.IsNullOrEmpty(newSearchText) != string.IsNullOrEmpty(oldSearchText))
        {
            RaiseItemsChanged();
        }

        if (newSearchText.Length < oldSearchText.Length)
        {
            HandleDeletion(oldSearchText, newSearchText);
            return;
        }

        this.SearchText = newSearchText;

        // We're not doing caret tracking in this sample.
        // Just assume caret is at end of text.
        var lastCaretPosition = newSearchText.Length;

        if (_suggestionPage == null)
        {
            var lastChar = newSearchText.Length > 0 && lastCaretPosition > 0 ?
                newSearchText[lastCaretPosition - 1] :
                '\0';

            if (lastChar == '@')
            {
                // User typed '@', switch to people suggestion page
                _lastPrefixPosition = lastCaretPosition - 1;
                UpdateSuggestionPage(_peopleSearchPage);
            }
            else if (lastChar == '/')
            {
                // User typed '/', switch to commands suggestion page
                _lastPrefixPosition = lastCaretPosition - 1;
                UpdateSuggestionPage(_commandsListPage);
            }
        }
        else if (_suggestionPage != null)
        {
            // figure out what part of the text applies to the current suggestion page
            var startOfSubSearch = _lastPrefixPosition + 1;
            var subString = _searchText.Substring(startOfSubSearch, lastCaretPosition - startOfSubSearch);
            _suggestionPage.SearchText = subString;

            // When the suggestion page updates its items, it should raise ItemsChanged event, which we will bubble through
        }
    }

    private void OnSuggestionPicked(object sender, MyTokenType suggestion)
    {
        _pickedTokens.Add(suggestion);
        UpdateSuggestionPage(null); // Clear suggestion page

        var displayText = suggestion.DisplayName;
        var tokenText = $"\u200B{displayText}\u200B "; // Add ZWSP before and after token, and a trailing space

        // remove the prefix character and any partial text after it
        if (_lastPrefixPosition >= 0 && _lastPrefixPosition < _searchText.Length)
        {
            _searchText = _searchText.Remove(_lastPrefixPosition);
        }

        // this.SearchText = this.SearchText.Insert(_lastCaretPosition, tokenText);
        this.SearchText = _searchText + tokenText;
        OnPropertyChanged(nameof(SearchText));
    }

    private void UpdateSuggestionPage(DynamicListPage? page)
    {
        if (_suggestionPage != null)
        {
            _suggestionPage.ItemsChanged -= OnSuggestedItemsChanged;
        }

        _suggestionPage = page;
        if (_suggestionPage != null)
        {
            _suggestionPage.SearchText = string.Empty; // reset search text
            _suggestionPage.ItemsChanged += OnSuggestedItemsChanged;
        }

        RaiseItemsChanged();
    }

    private void OnSuggestedItemsChanged(object sender, IItemsChangedEventArgs e)
    {
        RaiseItemsChanged();
    }

    private void HandleDeletion(string oldSearch, string newSearch)
    {
        var lastCaretPosition = newSearch.Length;

        if (_suggestionPage != null)
        {
            if (lastCaretPosition <= _lastPrefixPosition)
            {
                // User deleted back over the prefix character, so close the suggestion page
                UpdateSuggestionPage(null);
                _lastPrefixPosition = -1;
                return;
            }

            // figure out what part of the text applies to the current suggestion page
            var startOfSubSearch = _lastPrefixPosition + 1;
            if (lastCaretPosition <= _lastPrefixPosition)
            {
                // User deleted back over the prefix character, so close the suggestion page
                UpdateSuggestionPage(null);
                _lastPrefixPosition = -1;
            }
            else
            {
                var subString = newSearch.Substring(startOfSubSearch, lastCaretPosition - startOfSubSearch);
                _suggestionPage.SearchText = subString;
            }
        }
    }

    private void UpdateListItem(string newSearchText)
    {
        // Iterate over the search text.
        // Find all the strings that are surrounded by ZWSP characters.
        // Use those strings to find all the matching picked tokens.
        var index = 0;
        var tokenSpans = new List<(int Start, int End, MyTokenType? Token)>();
        while (index < newSearchText.Length)
        {
            var startIndex = newSearchText.IndexOf('\u200B', index);
            if (startIndex < 0)
            {
                break;
            }

            var endIndex = newSearchText.IndexOf('\u200B', startIndex + 1);
            if (endIndex < 0)
            {
                break;
            }

            var tokenText = newSearchText.Substring(startIndex + 1, endIndex - startIndex - 1);
            var token = _pickedTokens.Find(t => t.DisplayName == tokenText);
            tokenSpans.Add((startIndex, endIndex, token));

            index = endIndex + 1;
        }

        // for each span, construct a string like $"[{start}, {end}): {token.DisplayName} {token.Id}\n"
        var displayText = string.Empty;
        foreach (var (start, end, token) in tokenSpans)
        {
            if (token != null)
            {
                displayText += $"[{start}, {end}): {token.DisplayName} {token.Id}\n";
            }
        }

        _queryItem.Title = newSearchText;
        _queryItem.Subtitle = string.IsNullOrEmpty(displayText) ? "no tokens" : displayText;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // from DynamicListPage, not used
    }

    public IDictionary<string, object> GetProperties()
    {
        return new ValueSet()
        {
            { "TokenSearch", true },
        };
    }
}

internal interface ISearchUpdateArgs
{
    string NewSearchText { get; }
}

internal sealed partial class SearchUpdateArgs : ISearchUpdateArgs, IExtendedAttributesProvider
{
    public string NewSearchText { get; }

    private IDictionary<string, object> _properties;

    public SearchUpdateArgs(string newSearchText, IDictionary<string, object>? properties)
    {
        NewSearchText = newSearchText;
        _properties = properties ?? new Dictionary<string, object>();
    }

    public IDictionary<string, object> GetProperties() => _properties;
}

internal sealed partial class PeopleSearchPage : DynamicListPage
{
    internal event TypedEventHandler<object, MyTokenType>? SuggestionPicked;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // do nothing
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>();
        for (var i = 1; i <= 5; i++)
        {
            var name = $"Person {i}";
            var suggestion = new MyTokenType
            {
                DisplayName = name,
                Id = Guid.NewGuid().ToString(),
                Value = name,
            };
            items.Add(new ListItem(new PickSuggestionCommand(suggestion, SuggestionPicked))
            {
                Title = name,
                Subtitle = $"Email: person{i}@example.com",
            });
        }

        return items.ToArray();
    }
}

internal sealed partial class CommandsListPage : DynamicListPage
{
    internal event TypedEventHandler<object, MyTokenType>? SuggestionPicked;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // do nothing
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>();
        items.Add(new ListItem(new PickSuggestionCommand(new() { DisplayName = "Chat", Id = "chat" }, SuggestionPicked))
        {
            Title = "/chat",
            Subtitle = $"send a message",
        });
        items.Add(new ListItem(new PickSuggestionCommand(new() { DisplayName = "Status", Id = "status" }, SuggestionPicked))
        {
            Title = "/status",
            Subtitle = $"set your status",
        });

        return items.ToArray();
    }
}

internal sealed partial class MyTokenType
{
    public required string DisplayName { get; set; }

    public string Id { get; set; } = string.Empty;

    public object? Value { get; set; }
}

internal sealed partial class PickSuggestionCommand : InvokableCommand
{
    internal MyTokenType Suggestion { get; private set; }

    private TypedEventHandler<object, MyTokenType>? _pickedHandler;

    public PickSuggestionCommand(MyTokenType suggestion, TypedEventHandler<object, MyTokenType>? pickedHandler)
    {
        Suggestion = suggestion;
        _pickedHandler = pickedHandler;
        Name = $"Select";
    }

    public override CommandResult Invoke()
    {
        _pickedHandler?.Invoke(this, Suggestion);
        return CommandResult.KeepOpen();
    }
}

#pragma warning restore SA1402 // File may only contain a single type
#nullable disable
