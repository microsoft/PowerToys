// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Finds the snippet that a control in the snippet list stands for.
    /// </summary>
    internal static class TextExpanderRowSnippet
    {
        /// <summary>
        /// Gets the snippet from the control's Tag, which x:Bind keeps current as the list reuses
        /// its rows, or else from its DataContext. ItemsRepeater sets no DataContext on a row whose
        /// template uses x:Bind, so there a row's DataContext is the page's view model instead.
        /// </summary>
        /// <param name="tag">The control's Tag.</param>
        /// <param name="dataContext">The control's DataContext.</param>
        public static TextExpanderSnippetViewModel Resolve(object tag, object dataContext)
            => tag as TextExpanderSnippetViewModel ?? dataContext as TextExpanderSnippetViewModel;
    }
}
