// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.Contacts;

namespace Microsoft.CmdPal.Ext.Actions;

internal sealed partial class ContactsListPage : ListPage
{
    private List<ListItem> _contactItems = [];
    private bool _hasLoadedContacts;

    public ContactsListPage()
    {
        Icon = Icons.ContactInput;
        Title = "Contacts";
        Name = "Contacts";
    }

    public override IListItem[] GetItems()
    {
        if (!_hasLoadedContacts)
        {
            _ = LoadContactsAsync();
        }

        return _contactItems.ToArray();
    }

    private async Task LoadContactsAsync()
    {
        if (_hasLoadedContacts)
        {
            return;
        }

        try
        {
            _hasLoadedContacts = true;

            // Request access to contacts
            var accessStatus = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AllContactsReadOnly);
            if (accessStatus == null)
            {
                // No access to contacts
                EmptyContent = new CommandItem(new NoOpCommand())
                {
                    Title = "Unable to access contacts",
                    Subtitle = "Contact access is not available",
                    Icon = Icons.ContactInput,
                };
                RaiseItemsChanged(_contactItems.Count);
                return;
            }

            // Get all contacts
            var contacts = await accessStatus.FindContactsAsync();

            _contactItems.Clear();

            if (contacts == null || !contacts.Any())
            {
                EmptyContent = new CommandItem(new NoOpCommand())
                {
                    Title = "No Contacts Found",
                    Subtitle = "You have no contacts available.",
                    Icon = Icons.ContactInput,
                };
                RaiseItemsChanged(_contactItems.Count);
                return;
            }

            foreach (var contact in contacts)
            {
                var contactName = GetContactDisplayName(contact);
                var contactEmail = GetContactEmail(contact);

                if (!string.IsNullOrEmpty(contactName))
                {
                    var contactCommand = new ContactCommand(contact);

                    _contactItems.Add(new ListItem(contactCommand)
                    {
                        Title = contactName,
                        Subtitle = contactEmail ?? string.Empty,
                        Icon = Icons.ContactInput,
                    });
                }
            }

            // Sort contacts by name
            _contactItems = _contactItems.OrderBy(item => item.Title).ToList();

            // Notify that items have changed
            RaiseItemsChanged(_contactItems.Count);
        }
        catch (Exception ex)
        {
            _contactItems.Clear();
            EmptyContent = new CommandItem(new NoOpCommand())
            {
                Title = "Error loading contacts",
                Subtitle = $"Failed to load contacts: {ex.Message}",
                Icon = Icons.ContactInput,
            };
            RaiseItemsChanged(_contactItems.Count);
        }
    }

    private static string GetContactDisplayName(Contact contact)
    {
        if (!string.IsNullOrEmpty(contact.DisplayName))
        {
            return contact.DisplayName;
        }

        if (!string.IsNullOrEmpty(contact.Name))
        {
            return contact.Name;
        }

        // Try to get name from first and last name
        if (!string.IsNullOrEmpty(contact.FirstName) || !string.IsNullOrEmpty(contact.LastName))
        {
            return $"{contact.FirstName} {contact.LastName}".Trim();
        }

        return "Unknown Contact";
    }

    private static string? GetContactEmail(Contact contact)
    {
        var email = contact.Emails?.FirstOrDefault();
        return email?.Address;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Helper command class")]
public partial class ContactCommand : InvokableCommand
{
    private readonly Contact _contact;

    public ContactCommand(Contact contact)
    {
        _contact = contact;
        Name = "View Contact";
        Icon = Icons.ContactInput;
    }

    public override ICommandResult Invoke()
    {
        var contactName = !string.IsNullOrEmpty(_contact.DisplayName)
            ? _contact.DisplayName
            : !string.IsNullOrEmpty(_contact.Name)
                ? _contact.Name
                : "Unknown Contact";

        var message = new ToastStatusMessage($"Selected contact: {contactName}");
        message.Show();

        return CommandResult.Dismiss();
    }
}
