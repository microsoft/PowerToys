// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal static class FixtureVerification
{
    public static int Run()
    {
        try
        {
            using var disposed = new ManualResetEvent(false);
            var extension = new Extension(disposed);
            var provider = (CompatibilityCommandsProvider)extension.GetProvider(ProviderType.Commands)!;
            Require(provider.DisplayName.Contains(Baseline.SdkVersion, StringComparison.Ordinal), "SDK label");
            Require(typeof(Extension).GUID.ToString().Equals(Baseline.Clsid, StringComparison.OrdinalIgnoreCase), "COM class ID");
            var home = (ListPage)provider.TopLevelCommands().Single().Command;
            Require(home.GetItems().Length == 6, "Six scenario pages");
            Require(home.GetItems().Select(item => item.Command.Id).Distinct().Count() == 6, "Distinct page IDs");

            var actions = new ActionsPage();
            var counter = actions.GetItems()[0];
            var rowChanges = 0;
            counter.PropChanged += (_, _) => rowChanges++;
            Require(Invoke(counter.Command).Kind == CommandResultKind.KeepOpen, "Increment keeps page open");
            Require(counter.Title == "Counter: 1" && rowChanges > 0, "Counter property notification");
            Invoke(((ICommandContextItem)counter.MoreCommands[0]).Command);
            Require(counter.Title == "Counter: 0", "Context reset");
            Invoke(counter.Command);
            var confirmation = Invoke(actions.GetItems()[1].Command);
            Require(confirmation.Kind == CommandResultKind.Confirm, "Confirmation result");
            Require(counter.Title == "Counter: 1", "Showing confirmation preserves counter");
            Invoke(((IConfirmationArgs)confirmation.Args).PrimaryCommand);
            Require(counter.Title == "Counter: 0", "Confirm resets counter");

            var search = new SearchPage();
            var itemChanges = 0;
            search.ItemsChanged += (_, _) => itemChanges++;
            Require(search.GetItems().Length == 5, "Initial search results");
            search.SearchText = "APP";
            Require(search.GetItems().Single().Title == "Apple", "Case-insensitive search");
            search.SearchText = "zzz";
            Require(search.GetItems().Length == 0 && search.EmptyContent is not null, "Search empty state");
            search.SearchText = string.Empty;
            search.Filters!.CurrentFilterId = "vegetable";
            Require(search.GetItems().Length == 2, "Vegetable filter");
            search.Filters.CurrentFilterId = "all";
            Require(search.GetItems().Length == 5 && itemChanges >= 5, "Search and filter notifications");

            var details = new DetailsPage();
            Require(details.ShowDetails && details.GetItems()[0].Details.Metadata.Length == 2, "Details metadata");
            Require(details.GetItems()[1].Details.Title == "Beta details", "Different details");
            Require(details.GetItems()[2].Details is null, "Missing details fixture");

            var markdown = new MarkdownPage();
            var content = (MarkdownContent)markdown.GetContent().Single();
            var contentChanges = 0;
            content.PropChanged += (_, _) => contentChanges++;
            Invoke(((ICommandContextItem)markdown.Commands.Single()).Command);
            Require(content.Body.Contains("Revision **1**", StringComparison.Ordinal) && contentChanges > 0, "Markdown update notification");

            var formPage = new FormPage();
            var form = (IFormContent)formPage.GetContent()[0];
            var result = (MarkdownContent)formPage.GetContent()[1];
            Require(form.SubmitForm("{\"name\":\"Ada\",\"color\":\"green\",\"enabled\":\"false\"}", string.Empty).Kind == CommandResultKind.KeepOpen, "Form result");
            Require(result.Body == "Submitted: Ada; color: green; enabled: false", "Form output");
            foreach (var invalid in new[] { "{", "[]", "null", "42", "\"text\"" })
            {
                Require(form.SubmitForm(invalid, string.Empty).Kind == CommandResultKind.ShowToast, "Malformed form input");
            }

            Require(form.SubmitForm("{\"name\":false}", string.Empty).Kind == CommandResultKind.KeepOpen, "Unexpected field type");
            var empty = new EmptyPage();
            Require(empty.GetItems().Length == 0 && empty.EmptyContent is not null, "Explicit empty page");
            extension.Dispose();
            extension.Dispose();
            Require(disposed.WaitOne(0), "Idempotent extension shutdown");
            Console.WriteLine($"PASS: SDK {Baseline.SdkVersion} fixture data, commands, notifications, forms and disposal");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: SDK {Baseline.SdkVersion}: {exception}");
            return 1;
        }
    }

    private static ICommandResult Invoke(ICommand command) => ((IInvokableCommand)command).Invoke(null!);

    private static void Require(bool condition, string scenario)
    {
        if (!condition)
        {
            throw new InvalidOperationException(scenario);
        }
    }
}
