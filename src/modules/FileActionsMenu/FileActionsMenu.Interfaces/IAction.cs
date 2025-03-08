// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FileActionsMenu.Interfaces
{
    /// <summary>
    /// Interface that represents an action.
    /// </summary>
    public interface IAction
    {
        /// <summary>
        /// Gets or sets the SelectedItems property.
        /// When the plugin is loaded, the selected items are passed to this property.
        /// </summary>
        public string[] SelectedItems { get; set; }

        /// <summary>
        /// Gets the title of the action
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the type of the action.
        /// </summary>
        public ItemType Type { get; }

        /// <summary>
        /// Gets the sub menu items. Only has an effect if <seealso cref="Type"/> is <seealso cref="ItemType.HasSubMenu"/>.
        /// </summary>
        public IAction[]? SubMenuItems { get; }

        /// <summary>
        /// Gets the category of the action. Only applies for items in the top level menu.
        /// </summary>
        public int Category { get; }

        /// <summary>
        /// Gets the icon of the action displayed in the menu.
        /// </summary>
        public IconElement? Icon { get; }

        /// <summary>
        /// Gets a value indicating whether the action is visible or not.
        /// </summary>
        public bool IsVisible { get; }

        /// <summary>
        /// Executes the action.
        /// </summary>
        /// <param name="sender">MenuItem that invoked the action.</param>
        /// <param name="e">EventArgs of the click event.</param>
        /// <returns>(Awaitable) task that indicates when the File Action Menu should close.</returns>
        public Task Execute(object sender, RoutedEventArgs e);

        public enum ItemType
        {
            /// <summary>
            /// Single item action.
            /// </summary>
            SingleItem,

            /// <summary>
            /// Item with sub menu.
            /// </summary>
            HasSubMenu,

            /// <summary>
            /// Item is a separator. For simplicity <see cref="Interfaces.Separator" /> should be used.
            /// </summary>
            Separator,

            /// <summary>
            /// Item is checkable. For simplicity <see cref="ICheckableAction" /> should be used instead of <see cref="IAction"/>.
            /// </summary>
            Checkable,
        }
    }
}
