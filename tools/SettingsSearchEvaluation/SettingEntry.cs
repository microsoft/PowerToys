// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common.Search;

namespace SettingsSearchEvaluation;

internal enum EntryType
{
    SettingsPage,
    SettingsCard,
    SettingsExpander,
}

internal readonly record struct SettingEntry(
    EntryType Type,
    string Header,
    string PageTypeName,
    string ElementName,
    string ElementUid,
    string ParentElementName = "",
    string Description = "",
    string Icon = "") : ISearchable
{
    public string Id => ElementUid ?? $"{PageTypeName}|{ElementName}";

    public string SearchableText => Header ?? string.Empty;

    public string? SecondarySearchableText => Description;
}
