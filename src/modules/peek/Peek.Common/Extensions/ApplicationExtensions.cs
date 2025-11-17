// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Peek.Common.Extensions
{
    public static class ApplicationExtensions
    {
        /// <summary>
        /// Get registered services at the application level from anywhere
        public static T GetService<T>(this Application application)
            where T : class
        {
            return (application as IApp)!.GetService<T>();
        }
    }
}
