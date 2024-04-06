// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileActionsMenu.Interfaces
{
    /// <summary>
    /// Abstract class that represents a checkable action.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1623:Property summary documentation should match accessors", Justification = "To inherit descriptions")]
    public abstract class ICheckableAction : IAction
    {
        public IAction.ItemType Type => IAction.ItemType.Checkable;

        public IAction[]? SubMenuItems => null;

        public int Category => 0;

        public Task Execute(object sender, RoutedEventArgs e)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the action is checked or not.
        /// </summary>
        public abstract bool IsChecked { get; set; }

        /// <summary>
        /// Gets a value indicating whether the action is checked by default. One and only one item in the same group should be checked by default.
        /// </summary>
        public abstract bool IsCheckedByDefault { get; }

        /// <summary>
        /// Gets a uuid that identifies the group of checkable actions. Only one item in the group can be checked at a time.
        /// </summary>
        public abstract string? CheckableGroupUUID { get; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public abstract string[] SelectedItems { get; set; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public abstract string Title { get; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public abstract IconElement? Icon { get; }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public abstract bool IsVisible { get; }
    }
}
