// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Actions;

internal sealed partial class AgentsTestPage : ListPage
{
    private readonly List<ListItem> _agentItems;
    private readonly List<AgentInfo> _agentInfos =
    [
        new AgentInfo("@copilot", "Ask Windows Copilot anything...", "Windows Copilot", Icons.CopilotPng),
        new AgentInfo("@m365", "Ask Microsoft 365 Copilot...", "Microsoft 365 Copilot", Icons.CopilotSvg),
        new AgentInfo("@settings", "Open Windows Settings...", "Windows Settings", Icons.Settings),
        new AgentInfo("@google", "Ask Google...", "Google", Icons.Google),
        new AgentInfo("@bing", "Ask Bing...", "Bing", Icons.Bing),
        new AgentInfo("@google-calendar", "Ask Google Calendar...", "Google Calendar", Icons.GoogleCalendar)
    ];

    public AgentsTestPage()
    {
        Icon = Icons.CopilotSvg;
        Title = "Agents";
        Name = "Open";

        _agentItems = _agentInfos.ConvertAll(agentInfo =>
            new ListItem(new AgentQueryPage(agentInfo))
            {
                Title = agentInfo.Handle,
                Subtitle = agentInfo.Subtitle,
                Icon = agentInfo.Icon,
            });
    }

    public override IListItem[] GetItems()
    {
        return _agentItems.ToArray();
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
internal sealed class AgentInfo
{
    public string Handle { get; }

    public string Prompt { get; }

    public string Subtitle { get; }

    public IconInfo Icon { get; }

    public AgentInfo(string handle, string prompt, string subtitle, IconInfo icon)
    {
        Handle = handle;
        Prompt = prompt;
        Subtitle = subtitle;
        Icon = icon;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
internal sealed partial class AgentQueryPage : DynamicListPage
{
    private readonly List<ListItem> _items = [];
    private readonly string _agentSubtitle;

    private readonly AgentInfo _agentInfo;

    public AgentQueryPage(AgentInfo agentInfo)
    {
        _agentInfo = agentInfo;
        Icon = agentInfo.Icon;
        Title = agentInfo.Handle;
        Name = "Open";
        _agentSubtitle = agentInfo.Subtitle;
        EmptyContent = new CommandItem()
        {
            Command = new NoOpCommand(),
            Title = agentInfo.Prompt,
            Subtitle = _agentSubtitle,
            Icon = agentInfo.Icon,
        };
    }

    public override IListItem[] GetItems()
    {
        return _items.ToArray();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // If the new search text is empty, we can reset the page to its initial state.
        if (string.IsNullOrEmpty(newSearch))
        {
            _items.Clear();
            RaiseItemsChanged();
            return;
        }

        // If we don't have an item yet, create it. Otherwise, update the existing item.
        if (_items.Count == 0)
        {
            _items.Add(new ListItem(new AgentQueryContentPage(_agentInfo, newSearch))
            {
                Title = newSearch,
                Subtitle = _agentSubtitle,
                Icon = this.Icon,
            });
            RaiseItemsChanged();
        }
        else
        {
            _items[0].Title = newSearch;
            (_items[0].Command as AgentQueryContentPage)?.UpdateQuery(newSearch);
        }
    }
}

// now, a content page that will be used to display the results of the agent query
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
internal sealed partial class AgentQueryContentPage : ContentPage
{
    private readonly List<IContent> _content = [];
    private readonly AgentInfo _agentInfo;
    private readonly List<string> _responses =
    [
        "This is a response from the agent.",
        "You can ask more questions or refine your query.",
        "Here are some suggestions based on your query.",
        "Would you like to know more about this topic?",
        "Feel free to ask anything else!"
    ];

    private bool _started;
    private Task? _contentGenerationTask;

    private int _responsesGenerated;

    private string _query = string.Empty;

    public AgentQueryContentPage(AgentInfo agentInfo, string query)
    {
        Title = agentInfo.Handle;
        Icon = agentInfo.Icon;
        Name = "Ask Agent";
        IsLoading = true;
        _query = query;
        _agentInfo = agentInfo;
    }

    public void UpdateQuery(string query)
    {
        _query = query;
        _responsesGenerated = 0;
        _content.Clear();
        _started = false;
        Title = $"{_agentInfo.Handle}";

        // RaiseItemsChanged();
    }

    public override IContent[] GetContent()
    {
        if (!_started)
        {
            _content.Add(new MarkdownContent($"## {_agentInfo.Handle}\n\n{_query}"));

            // Kick off a task to generate the content if it hasn't been started yet.
            _contentGenerationTask = Task.Run(() =>
            {
                for (var i = 0; i < _responses.Count; i++)
                {
                    // Simulate generating a response.
                    Task.Delay(1000).Wait();
                    _content.Add(new MarkdownContent(_responses[i]));
                    _responsesGenerated++;
                    Title = $"{_agentInfo.Handle} ({_responsesGenerated})";
                    RaiseItemsChanged();
                }

                IsLoading = false;

                var toast = new ToastStatusMessage(new StatusMessage
                {
                    Message = $"Did the thing",
                    State = MessageState.Success,
                });
                toast.Show();
            });

            _started = true;
            _contentGenerationTask.ConfigureAwait(false);
        }
        else
        {
        }

        return _content.ToArray();
    }
}
