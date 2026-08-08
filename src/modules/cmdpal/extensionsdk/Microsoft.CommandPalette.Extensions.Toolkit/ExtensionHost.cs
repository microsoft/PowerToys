// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ExtensionHost
{
    public static IExtensionHost? Host { get; private set; }

    public static void Initialize(IExtensionHost host) => Host = host;

    /// <summary>
    /// Fire-and-forget a log message to the Command Palette host app. Since
    /// the host is in another process, we do this in a try/catch in a
    /// background thread, as to not block the calling thread, nor explode if
    /// the host app is gone.
    /// </summary>
    /// <param name="message">The log message to send</param>
    public static void LogMessage(ILogMessage message)
    {
        if (Host is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.LogMessage(message);
                }
                catch (Exception)
                {
                }
            });
        }
    }

    public static void LogMessage(string message)
    {
        var logMessage = new LogMessage() { Message = message };
        LogMessage(logMessage);
    }

    public static void ShowStatus(IStatusMessage message, StatusContext context)
    {
        if (Host is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.ShowStatus(message, context);
                }
                catch (Exception)
                {
                }
            });
        }
    }

    public static void HideStatus(IStatusMessage message)
    {
        if (Host is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Host.HideStatus(message);
                }
                catch (Exception)
                {
                }
            });
        }
    }

    /// <summary>
    /// True when the connected host supports interactive authorization
    /// (i.e. it implements <see cref="IExtensionHost2"/>). Older Command Palette
    /// versions return false.
    /// </summary>
    public static bool SupportsAuthorization => Host is IExtensionHost2;

    /// <summary>
    /// Ask the Command Palette host to run an interactive authorization flow. The
    /// returned task completes when the redirect is captured (or the flow fails,
    /// is canceled, or times out). Prefer <see cref="OAuthClient"/> for the common
    /// Authorization Code + PKCE case.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// The connected host does not implement <see cref="IExtensionHost2"/>.
    /// </exception>
    public static async Task<IAuthorizationResult> RequestAuthorizationAsync(
        IAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Host is not IExtensionHost2 host2)
        {
            throw new NotSupportedException(
                "The Command Palette host does not support authentication. Update Command Palette to a newer version.");
        }

        var operation = host2.RequestAuthorizationAsync(request);
        using (cancellationToken.Register(() =>
        {
            try
            {
                operation.Cancel();
            }
            catch (Exception)
            {
            }
        }))
        {
            return await operation;
        }
    }

    /// <summary>
    /// True when the connected host supports host-driven navigation
    /// (i.e. it implements <see cref="IExtensionHost2"/>). This is the same
    /// capability that gates authorization. Older Command Palette versions
    /// return false.
    /// </summary>
    public static bool SupportsNavigation => Host is IExtensionHost2;

    /// <summary>
    /// Ask Command Palette to navigate to one of this extension's pages (for
    /// example, right after a successful sign-in). The host summons its window
    /// and navigates to the supplied page.
    /// </summary>
    /// <param name="page">The page to navigate to. Typically an <c>IPage</c>.</param>
    /// <param name="navigationMode">Controls navigation stack behavior.</param>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// The connected host does not implement <see cref="IExtensionHost2"/>.
    /// </exception>
    public static async Task GoToPageAsync(ICommand page, NavigationMode navigationMode = NavigationMode.Push)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (Host is not IExtensionHost2 host2)
        {
            throw new NotSupportedException(
                "The Command Palette host does not support navigation. Update Command Palette to a newer version.");
        }

        await host2.GoToPageAsync(page, navigationMode);
    }
}
