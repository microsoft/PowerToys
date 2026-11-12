
class MyPrefixCommandProvider : ICommandProvider
{
    private PeopleDynamicListPage _peopleCommand; // com.contoso.people
    private CommandsListPage _commandsCommand; // com.contoso.commands
    private AddFileCommand _addFileCommand; // com.contoso.addFile

    private PrefixSearchPage _prefixSearchPage;

    public MyPrefixCommandProvider()
    {
        _prefixSearchPage = new PrefixSearchPage();
        
        var tokenPickedHandler = _prefixSearchPage.HandleTokenPicked;

        _peopleCommand = new PeopleDynamicListPage(tokenPickedHandler);
        _commandsCommand = new CommandsListPage(tokenPickedHandler);
        _addFileCommand = new AddFileCommand(tokenPickedHandler);
    }

    public ICommandItem[] GetTopLevelCommands()
    {
        return new ICommandItem[] { _prefixSearchPage };
    }

    public ICommand GetCommand(String id)
    {
        if (id == "com.contoso.people")
        {
            return _peopleCommand;
        }
        else if (id == "com.contoso.commands")
        {
            return _commandsCommand;
        }
        else if (id == "com.contoso.addFile")
        {
            return _addFileCommand;
        }
        return null;
    }
}

class PrefixSearchPage : ListPage, IPrefixProvider
{
    public IDictionary<String, String> PrefixCommands => new Dictionary<String, String>
    {
        { "@", "com.contoso.people" },
        { "/", "com.contoso.commands" },
        { "+", "com.contoso.addFile" },
    };

    public event Windows.Foundation.TypedEventHandler<Object, ITokenPickedEventArgs> TokenAdded;

    public PrefixSearchPage()
    {
        // Initialize the page...
    }

    public void SendQuery(ISearchUpdateArgs args)
    {
        // Handle the search update, possibly updating the list of items based on the new search text
        var searchText = args.NewSearchText;
        var properties = args.GetProperties();
        var tokens = properties.TryLookup<object>("tokens") as ITokenPositions[];

        // Here you could use these tokens to update the commands in our own search results
        // Or just save them, and plumb them into the InvokableCommand the user eventually picks
    }
    
    public void HandleTokenPicked(object sender, ITokenPickedEventArgs args)
    {
        // Handle the token picked event, e.g., log it or update UI
        var token = args.Token;
        var text = token.DisplayText;
        // Do something with the picked token
    }

    // Other ListPage members...
}

public class PeopleDynamicListPage : DynamicListPage
{
    private Windows.Foundation.TypedEventHandler<Object, ITokenPickedEventArgs> _tokenPicked;

    public PeopleDynamicListPage(TypedEventHandler<Object, ITokenPickedEventArgs> onTokenPicked)
    {
        _tokenPicked = onTokenPicked;
    }

    public override IListItem[] GetItems()
    {
        // Return the list of people items
        return new IListItem[]
        {
            new PersonSuggestionItem("Alice", _tokenPicked),
            new PersonSuggestionItem("Bob", _tokenPicked),
            new PersonSuggestionItem("Charlie", _tokenPicked)
        };
    }

    // Other DynamicListPage members...
}
public class PersonSuggestionItem : SuggestionListItem { /* ... */ }
public class SuggestionListItem : SuggestionListItem
{
    private Windows.Foundation.TypedEventHandler<Object, ITokenPickedEventArgs> _tokenPicked;
    private object _tokenValue;

    public SuggestionListItem(object token, TypedEventHandler<Object, ITokenPickedEventArgs> onTokenPicked)
    {
        _tokenValue = token;
        _tokenPicked = onTokenPicked;
    }

    public override ICommandResult Invoke()
    {
        _tokenPicked?.Invoke(this, new TokenPickedEventArgs(_tokenValue));
        return CommandResult.KeepOpen;
    }
}