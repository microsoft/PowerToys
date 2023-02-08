// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// An interface so that pages can define refresh method to refresh their view models.
namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public interface IRefreshablePage
    {
        void RefreshEnabledState();
    }
}
