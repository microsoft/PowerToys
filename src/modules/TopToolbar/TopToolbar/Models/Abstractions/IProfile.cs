// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace TopToolbar.Models.Abstractions
{
    public interface IProfile
    {
        string Id { get; }

        string Name { get; }

        IReadOnlyList<IProfileGroup> Groups { get; }

        IEnumerable<IProfileGroup> GetActiveGroups();
    }

    public interface IProfileGroup
    {
        string Id { get; }

        string Name { get; }

        bool IsEnabled { get; }

        int SortOrder { get; }

        IReadOnlyList<IProfileAction> Actions { get; }

        IEnumerable<IProfileAction> GetActiveActions();
    }

    public interface IProfileAction
    {
        string Id { get; }

        string Name { get; }

        string Description { get; }

        bool IsEnabled { get; }

        string IconGlyph { get; }
    }
}
