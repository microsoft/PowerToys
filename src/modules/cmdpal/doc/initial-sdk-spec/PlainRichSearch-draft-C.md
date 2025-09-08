---
author: Mike Griese
created on: 2025-09-08
last updated: 2025-09-08
issue id: n/a
---

## Addenda II-C: Plain Rich Search 

What if adding a whole bunch of new interfaces, we just _fake it_. 

We'll just use embedded zero-width space (ZWSP) characters in the search text to "bracket" tokens.

We'll add an extended attribute to the page - something like `TokenSearch`. If that's set to true, CmdPal will render the search box as a rich text box that can contain tokens. When we do that, we'll trezt text between ZWSP characters as tokens, and give them special UI treatment (like a link).

When the user types a special prefix (like `@`), the extension page will internally swap it's items with a list of suggestions. When the user picks one, the page will raise a search update event with the new search text. That new text will have the "token" embedded in it, bracketed by ZWSP characters.

> [!INFO] 
> 
> This is a draft `.idl` spec. Details are still subject to change. Overall
> concepts however will likely remain similar

```c# prefix search
interface ISearchUpdateArgs requires IExtendedAttributesProvider
{
    String NewSearchText { get; } // The text that the user has typed into the search box.

    // Extended attributes:
    // * CaretPosition (int): The current position of the cursor in the search text, maybe?
}

interface IDynamicListPage2 requires IDynamicListPage
{
    void UpdateSearch(ISearchUpdateArgs args);
}
```


```cs
class MySuggestionSearchPage : DynamicListPage, IDynamicListPage2
{
    private PeopleSearchPage _peopleSearchPage = new();
    private CommandsListPage _commandsListPage = new();
    private DynamicListPage? _suggestionPage = null;
    private List<MyTokenType> _pickedTokens = new();
    private int _lastCaretPosition = 0;

    MySuggestionSearchPage()
    {
        _peopleSearchPage.SuggestionPicked += OnSuggestionPicked;
        _commandsListPage.SuggestionPicked += OnSuggestionPicked;
    }

    public IListItem[] GetItems()
    {
        return _suggestionPage?.GetItems() ?? Array.Empty<IListItem>();
    }

    public void UpdateSearch(ISearchUpdateArgs args)
    {
        if (args.GetProperties() is IDictionary<string, object> props)
        {
            if (props.TryGetValue("CaretPosition", out var caretPosObj) && caretPosObj is int caretPos)
            {
                _lastCaretPosition = caretPos;
            }
        }

        var oldSearchText = this.SearchText;
        var newSearchText = args.NewSearchText;
        if (newSearchText.Length < oldSearchText.Length)
        {
            HandleDeletion(oldSearchText, newSearchText);
            return;
        }

        this.SearchText = newSearchText;

        if (_suggestionPage == null){
            var lastChar = newSearchText.Length > 0 && _lastCaretPosition > 0 ? 
                newSearchText[_lastCaretPosition - 1] : 
                '\0';
            
            if (lastChar == '@')
            {
                // User typed '@', switch to people suggestion page
                UpdateSuggestionPage(_peopleSearchPage);
            }
            else if (lastChar == '#')
            {
                // User typed '#', switch to commands suggestion page
                UpdateSuggestionPage(_commandsListPage);
            }

        }
        else if (_suggestionPage != null)
        {
            // figure out what part of the text applies to the current suggestion page
            var subString = /* omitted */;
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

        this.SearchText = this.SearchText.Insert(_lastCaretPosition, tokenText);
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
}
```



------------


None of this solves the "Command with parameters" problem. We'd still need to introduce a new page type for that. 

It needs to be a new page type, because we need agressively change the search box to only allow inputs into the parameter fields. We can't have users removing part of the command's display text - that doesn't make sense.

