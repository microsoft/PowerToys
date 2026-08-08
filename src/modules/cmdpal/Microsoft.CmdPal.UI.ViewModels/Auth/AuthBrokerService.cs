// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Auth;

/// <summary>
/// The Command Palette side of the built-in authorization flow. Command Palette
/// acts as a thin, hardened redirect broker: it allocates the redirect target,
/// injects a cryptographically-random <c>state</c>, opens the browser, captures
/// the single redirect, validates <c>state</c>, and hands the raw response
/// parameters back to the extension. Command Palette never sees the PKCE
/// verifier and never stores third-party tokens; the Toolkit's OAuthClient does
/// the code exchange inside the extension's process.
/// </summary>
public sealed partial class AuthBrokerService : IDisposable
{
    /// <summary>The custom-scheme redirect target reused from the existing protocol wiring.</summary>
    public const string CustomSchemeRedirectUri = "x-cmdpal://auth/callback";

    private static readonly TimeSpan DefaultTimeoutValue = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxTimeoutValue = TimeSpan.FromSeconds(300);

    private static readonly Lazy<AuthBrokerService> LazyInstance = new(() =>
        new AuthBrokerService(new DefaultAuthBrokerPlatform(), new TcpLoopbackRedirectListenerFactory()));

    // Pending custom-scheme flows, keyed by their single-use state. A flow is
    // completed when a protocol activation carrying the matching state arrives.
    private readonly ConcurrentDictionary<string, TaskCompletionSource<IReadOnlyDictionary<string, string>>> _pendingCustomScheme = new(StringComparer.Ordinal);

    private readonly ILoopbackRedirectListenerFactory _loopbackFactory;
    private readonly TimeSpan _defaultTimeout;
    private readonly TimeSpan _maxTimeout;

    private IAuthBrokerPlatform _platform;

    // Canceled (and replaced) when the broker is torn down, cancelling every
    // in-flight flow. Nothing sensitive is stranded: the PKCE verifier lives in
    // the extension, not here.
    private CancellationTokenSource _shutdownCts = new();

    public AuthBrokerService(IAuthBrokerPlatform platform, ILoopbackRedirectListenerFactory loopbackFactory)
        : this(platform, loopbackFactory, DefaultTimeoutValue, MaxTimeoutValue)
    {
    }

    internal AuthBrokerService(
        IAuthBrokerPlatform platform,
        ILoopbackRedirectListenerFactory loopbackFactory,
        TimeSpan defaultTimeout,
        TimeSpan maxTimeout)
    {
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        _loopbackFactory = loopbackFactory ?? throw new ArgumentNullException(nameof(loopbackFactory));
        _defaultTimeout = defaultTimeout;
        _maxTimeout = maxTimeout;
    }

    /// <summary>The process-wide broker used by the running app.</summary>
    public static AuthBrokerService Instance => LazyInstance.Value;

    /// <summary>
    /// Replace the platform hooks (browser launch + re-foreground). Called once
    /// during host startup so the broker can drive the real window.
    /// </summary>
    public void SetPlatform(IAuthBrokerPlatform platform)
    {
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
    }

    /// <summary>
    /// Run an interactive authorization flow. The returned operation is
    /// cancelable; cancelling it abandons the wait and fails the flow.
    /// </summary>
    public IAsyncOperation<IAuthorizationResult> RequestAuthorizationAsync(IAuthorizationRequest request, AppExtensionHost? statusHost)
    {
        return AsyncInfo.Run(token => RunFlowAsync(request, statusHost, token));
    }

    /// <summary>
    /// Route a captured <c>x-cmdpal://auth/callback</c> protocol activation to
    /// the pending flow whose single-use <c>state</c> matches. Unknown state is
    /// ignored (returns false) so stray activations cannot complete a flow.
    /// </summary>
    public bool TryCompleteCustomSchemeRedirect(string uri)
    {
        if (string.IsNullOrEmpty(uri) || !Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        return TryCompleteCustomSchemeRedirect(parsed);
    }

    public bool TryCompleteCustomSchemeRedirect(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var query = uri.Query.StartsWith('?') ? uri.Query[1..] : uri.Query;
        var parameters = QueryStringParser.Parse(query);
        if (!parameters.TryGetValue("state", out var state) || string.IsNullOrEmpty(state))
        {
            return false;
        }

        // TryRemove enforces single-use: a second activation for the same state
        // finds nothing and is ignored.
        if (_pendingCustomScheme.TryRemove(state, out var tcs))
        {
            return tcs.TrySetResult(parameters);
        }

        return false;
    }

    /// <summary>
    /// Cancel every in-flight flow (e.g. on broker teardown) and reset so future
    /// flows can run.
    /// </summary>
    public void CancelAllFlows()
    {
        var old = Interlocked.Exchange(ref _shutdownCts, new CancellationTokenSource());
        try
        {
            old.Cancel();
        }
        catch (Exception)
        {
        }
        finally
        {
            old.Dispose();
        }

        foreach (var key in _pendingCustomScheme.Keys)
        {
            if (_pendingCustomScheme.TryRemove(key, out var tcs))
            {
                tcs.TrySetCanceled();
            }
        }
    }

    /// <summary>Cancel any in-flight flows and release the shutdown token source.</summary>
    public void Dispose()
    {
        CancelAllFlows();
        _shutdownCts.Dispose();
    }

    internal async Task<IAuthorizationResult> RunFlowAsync(IAuthorizationRequest request, AppExtensionHost? statusHost, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Failure("The authorization request was null.");
        }

        var endpoint = request.AuthorizationEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
        {
            return Failure("The authorization endpoint is missing or invalid.");
        }

        var state = GenerateState();
        var timeout = ResolveTimeout(request.TimeoutSeconds);

        var shutdownCts = _shutdownCts;
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token, shutdownCts.Token);

        IStatusMessage? status = null;
        try
        {
            status = ShowStatus(statusHost, request.DisplayName);

            IReadOnlyDictionary<string, string> captured;
            string redirectUri;

            if (request.RedirectKind == AuthorizationRedirectKind.CustomScheme)
            {
                (captured, redirectUri) = await RunCustomSchemeFlowAsync(request, endpoint, state, linked.Token).ConfigureAwait(false);
            }
            else
            {
                (captured, redirectUri) = await RunLoopbackFlowAsync(request, endpoint, state, linked.Token).ConfigureAwait(false);
            }

            if (!captured.TryGetValue("state", out var returnedState) || !FixedTimeEquals(returnedState, state))
            {
                return Failure("The authorization response state did not match the request.");
            }

            var response = StripState(captured);

            if (response.TryGetValue("error", out var providerError) && !string.IsNullOrEmpty(providerError))
            {
                return Failure(FormatProviderError(response));
            }

            // The redirect landed; bring Command Palette back to the foreground.
            _platform.BringToForeground();

            return Success(redirectUri, response);
        }
        catch (OperationCanceledException)
        {
            if (shutdownCts.IsCancellationRequested)
            {
                return Failure("The authorization flow was canceled because Command Palette is shutting down.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Failure("The authorization flow was canceled.");
            }

            return Failure("The authorization flow timed out before the browser returned.");
        }
        catch (Exception ex)
        {
            return Failure($"The authorization flow failed: {ex.Message}");
        }
        finally
        {
            HideStatus(statusHost, status);
        }
    }

    private async Task<(IReadOnlyDictionary<string, string> Captured, string RedirectUri)> RunLoopbackFlowAsync(
        IAuthorizationRequest request, string endpoint, string state, CancellationToken cancellationToken)
    {
        using var listener = _loopbackFactory.Create();
        var redirectUri = listener.RedirectUri;
        var parameters = ComposeParameters(request, redirectUri, state);
        _platform.LaunchBrowser(new Uri(BuildAuthorizationUrl(endpoint, parameters)));
        var captured = await listener.WaitForRedirectAsync(cancellationToken).ConfigureAwait(false);
        return (captured, redirectUri);
    }

    private async Task<(IReadOnlyDictionary<string, string> Captured, string RedirectUri)> RunCustomSchemeFlowAsync(
        IAuthorizationRequest request, string endpoint, string state, CancellationToken cancellationToken)
    {
        var redirectUri = CustomSchemeRedirectUri;
        var tcs = new TaskCompletionSource<IReadOnlyDictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingCustomScheme.TryAdd(state, tcs))
        {
            throw new InvalidOperationException("A duplicate authorization state was generated.");
        }

        try
        {
            var parameters = ComposeParameters(request, redirectUri, state);
            _platform.LaunchBrowser(new Uri(BuildAuthorizationUrl(endpoint, parameters)));

            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                var captured = await tcs.Task.ConfigureAwait(false);
                return (captured, redirectUri);
            }
        }
        finally
        {
            _pendingCustomScheme.TryRemove(state, out _);
        }
    }

    private TimeSpan ResolveTimeout(int timeoutSeconds)
    {
        if (timeoutSeconds <= 0)
        {
            return _defaultTimeout;
        }

        var requested = TimeSpan.FromSeconds(timeoutSeconds);
        return requested > _maxTimeout ? _maxTimeout : requested;
    }

    private IStatusMessage? ShowStatus(AppExtensionHost? statusHost, string? displayName)
    {
        if (statusHost is null)
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(displayName) ? "the extension" : displayName;
        var message = new StatusMessage
        {
            Message = $"Waiting for you to finish signing in to {name} in your browser...",
            State = MessageState.Info,
        };

        try
        {
            _ = statusHost.ShowStatus(message, StatusContext.Extension);
        }
        catch (Exception)
        {
            return null;
        }

        return message;
    }

    private static void HideStatus(AppExtensionHost? statusHost, IStatusMessage? status)
    {
        if (statusHost is null || status is null)
        {
            return;
        }

        try
        {
            _ = statusHost.HideStatus(status);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Generate a 256-bit, URL-safe, single-use state token.</summary>
    internal static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Copy the extension-supplied parameters and inject the host-owned
    /// <c>redirect_uri</c> and <c>state</c>. Any caller-supplied redirect_uri or
    /// state is overwritten; the host owns those.
    /// </summary>
    internal static Dictionary<string, string> ComposeParameters(IAuthorizationRequest request, string redirectUri, string state)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        if (request.Parameters is not null)
        {
            foreach (var pair in request.Parameters)
            {
                parameters[pair.Key] = pair.Value;
            }
        }

        parameters["redirect_uri"] = redirectUri;
        parameters["state"] = state;
        return parameters;
    }

    internal static string BuildAuthorizationUrl(string endpoint, IReadOnlyDictionary<string, string> parameters)
    {
        var builder = new StringBuilder(endpoint);
        builder.Append(endpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?');

        var first = true;
        foreach (var pair in parameters)
        {
            if (!first)
            {
                builder.Append('&');
            }

            first = false;
            builder.Append(Uri.EscapeDataString(pair.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(pair.Value));
        }

        return builder.ToString();
    }

    private static Dictionary<string, string> StripState(IReadOnlyDictionary<string, string> captured)
    {
        var response = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in captured)
        {
            if (!string.Equals(pair.Key, "state", StringComparison.Ordinal))
            {
                response[pair.Key] = pair.Value;
            }
        }

        return response;
    }

    private static string FormatProviderError(IReadOnlyDictionary<string, string> response)
    {
        var error = response.TryGetValue("error", out var e) ? e : "unknown_error";
        if (response.TryGetValue("error_description", out var description) && !string.IsNullOrEmpty(description))
        {
            return $"The identity provider returned an error: {error} ({description}).";
        }

        return $"The identity provider returned an error: {error}.";
    }

    private static bool FixedTimeEquals(string? a, string b)
    {
        if (a is null)
        {
            return false;
        }

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        if (aBytes.Length != bBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static IAuthorizationResult Success(string redirectUri, IReadOnlyDictionary<string, string> response) =>
        new AuthorizationResult
        {
            IsSuccessful = true,
            RedirectUri = redirectUri,
            ResponseParameters = response,
            Error = string.Empty,
        };

    private static IAuthorizationResult Failure(string error) =>
        new AuthorizationResult
        {
            IsSuccessful = false,
            RedirectUri = string.Empty,
            ResponseParameters = new Dictionary<string, string>(),
            Error = error,
        };
}
