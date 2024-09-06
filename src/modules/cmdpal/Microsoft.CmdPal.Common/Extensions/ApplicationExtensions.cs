// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.Common.Extensions;

/// <summary>
/// Extension class implementing extension methods for <see cref="Application"/>.
/// </summary>
public static class ApplicationExtensions
{
    /// <summary>
    /// Get registered services at the application level from anywhere in the
    /// application.
    ///
    /// Note:
    /// https://learn.microsoft.com/uwp/api/windows.ui.xaml.application.current?view=winrt-22621#windows-ui-xaml-application-current
    /// "Application is a singleton that implements the static Current property
    /// to provide shared access to the Application instance for the current
    /// application. The singleton pattern ensures that state managed by
    /// Application, including shared resources and properties, is available
    /// from a single, shared location."
    ///
    /// Example of usage:
    /// <code>
    /// Application.Current.GetService<T>()
    /// </code>
    /// </summary>
    /// <typeparam name="T">Service type.</typeparam>
    /// <param name="application">Current application.</param>
    /// <returns>Service reference.</returns>
    public static T GetService<T>(this Application application)
        where T : class
    {
        return (application as IApp)!.GetService<T>();
    }
}
