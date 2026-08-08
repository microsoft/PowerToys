// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Auth;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests.Auth;

[TestClass]
public sealed class AuthBrokerServiceTests
{
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(300);

    [TestMethod]
    public void GenerateState_IsUrlSafeAndUnique()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 200; i++)
        {
            var state = AuthBrokerService.GenerateState();

            // 32 bytes -> 43 base64url chars, comfortably above the 128-bit floor.
            Assert.IsTrue(state.Length >= 22, $"state too short: {state.Length}");
            foreach (var c in state)
            {
                var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                Assert.IsTrue(ok, $"non-url-safe char '{c}' in state");
            }

            Assert.IsTrue(seen.Add(state), "state was not unique");
        }
    }

    [TestMethod]
    public void ComposeParameters_InjectsRedirectAndState_AndOverridesCallerValues()
    {
        var request = new AuthorizationRequest
        {
            Parameters = new Dictionary<string, string>
            {
                ["client_id"] = "abc",
                ["scope"] = "openid profile",

                // These must be overwritten; the host owns them.
                ["redirect_uri"] = "http://evil.example/",
                ["state"] = "caller-supplied",
            },
        };

        var composed = AuthBrokerService.ComposeParameters(request, "http://127.0.0.1:5000/", "host-state");

        Assert.AreEqual("abc", composed["client_id"]);
        Assert.AreEqual("openid profile", composed["scope"]);
        Assert.AreEqual("http://127.0.0.1:5000/", composed["redirect_uri"]);
        Assert.AreEqual("host-state", composed["state"]);
    }

    [TestMethod]
    public void BuildAuthorizationUrl_AppendsQueryAndEscapes()
    {
        var noQuery = AuthBrokerService.BuildAuthorizationUrl(
            "https://idp.example/authorize",
            new Dictionary<string, string> { ["scope"] = "a b", ["state"] = "x/y" });
        Assert.IsTrue(noQuery.StartsWith("https://idp.example/authorize?", StringComparison.Ordinal));
        Assert.IsTrue(noQuery.Contains("scope=a%20b", StringComparison.Ordinal));
        Assert.IsTrue(noQuery.Contains("state=x%2Fy", StringComparison.Ordinal));

        var withQuery = AuthBrokerService.BuildAuthorizationUrl(
            "https://idp.example/authorize?foo=bar",
            new Dictionary<string, string> { ["state"] = "z" });
        Assert.IsTrue(withQuery.Contains("?foo=bar&state=z", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Loopback_HappyPath_ReturnsCode_StripsState_Foregrounds()
    {
        var platform = new RecordingPlatform();
        var listener = new FakeListener
        {
            OnWait = _ =>
            {
                var state = QueryValue(platform.LaunchedUri!, "state");
                return Task.FromResult<IReadOnlyDictionary<string, string>>(
                    new Dictionary<string, string> { ["code"] = "abc123", ["state"] = state });
            },
        };
        var broker = new AuthBrokerService(platform, new FakeFactory(listener), LongTimeout, MaxTimeout);

        var result = await broker.RunFlowAsync(LoopbackRequest(), null, CancellationToken.None);

        Assert.IsTrue(result.IsSuccessful, result.Error);
        Assert.AreEqual(listener.RedirectUri, result.RedirectUri);
        Assert.AreEqual("abc123", result.ResponseParameters["code"]);
        Assert.IsFalse(result.ResponseParameters.ContainsKey("state"), "state should be stripped");
        Assert.AreEqual(1, platform.ForegroundCount);
        Assert.IsTrue(listener.Disposed, "listener should be disposed");

        // The launched URL carries the host-injected redirect_uri.
        Assert.AreEqual(listener.RedirectUri, QueryValue(platform.LaunchedUri!, "redirect_uri"));
    }

    [TestMethod]
    public async Task Loopback_StateMismatch_Fails()
    {
        var platform = new RecordingPlatform();
        var listener = new FakeListener
        {
            OnWait = _ => Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string> { ["code"] = "abc", ["state"] = "not-the-right-state" }),
        };
        var broker = new AuthBrokerService(platform, new FakeFactory(listener), LongTimeout, MaxTimeout);

        var result = await broker.RunFlowAsync(LoopbackRequest(), null, CancellationToken.None);

        Assert.IsFalse(result.IsSuccessful);
        Assert.IsTrue(result.Error.Contains("state", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, platform.ForegroundCount);
    }

    [TestMethod]
    public async Task Loopback_ProviderError_Fails()
    {
        var platform = new RecordingPlatform();
        var listener = new FakeListener
        {
            OnWait = _ =>
            {
                var state = QueryValue(platform.LaunchedUri!, "state");
                return Task.FromResult<IReadOnlyDictionary<string, string>>(
                    new Dictionary<string, string> { ["error"] = "access_denied", ["state"] = state });
            },
        };
        var broker = new AuthBrokerService(platform, new FakeFactory(listener), LongTimeout, MaxTimeout);

        var result = await broker.RunFlowAsync(LoopbackRequest(), null, CancellationToken.None);

        Assert.IsFalse(result.IsSuccessful);
        Assert.IsTrue(result.Error.Contains("access_denied", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Loopback_Timeout_Fails()
    {
        var platform = new RecordingPlatform();
        var listener = new FakeListener
        {
            OnWait = async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new Dictionary<string, string>();
            },
        };
        var broker = new AuthBrokerService(platform, new FakeFactory(listener), TimeSpan.FromMilliseconds(150), MaxTimeout);

        var result = await broker.RunFlowAsync(LoopbackRequest(), null, CancellationToken.None);

        Assert.IsFalse(result.IsSuccessful);
        Assert.IsTrue(result.Error.Contains("timed out", StringComparison.OrdinalIgnoreCase), result.Error);
    }

    [TestMethod]
    public async Task Loopback_Cancel_Fails()
    {
        var platform = new RecordingPlatform();
        var listener = new FakeListener
        {
            OnWait = async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new Dictionary<string, string>();
            },
        };
        var broker = new AuthBrokerService(platform, new FakeFactory(listener), LongTimeout, MaxTimeout);

        using var cts = new CancellationTokenSource();
        var task = broker.RunFlowAsync(LoopbackRequest(), null, cts.Token);
        Assert.IsTrue(platform.LaunchedSignal.Wait(TimeSpan.FromSeconds(5)));
        cts.Cancel();

        var result = await task;

        Assert.IsFalse(result.IsSuccessful);
        Assert.IsTrue(result.Error.Contains("canceled", StringComparison.OrdinalIgnoreCase), result.Error);
    }

    [TestMethod]
    public async Task CancelAllFlows_CancelsInFlightLoopback()
    {
        var platform = new RecordingPlatform();
        var listener = new FakeListener
        {
            OnWait = async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new Dictionary<string, string>();
            },
        };
        var broker = new AuthBrokerService(platform, new FakeFactory(listener), LongTimeout, MaxTimeout);

        var task = broker.RunFlowAsync(LoopbackRequest(), null, CancellationToken.None);
        Assert.IsTrue(platform.LaunchedSignal.Wait(TimeSpan.FromSeconds(5)));
        broker.CancelAllFlows();

        var result = await task;

        Assert.IsFalse(result.IsSuccessful);
        Assert.IsTrue(result.Error.Contains("shutting down", StringComparison.OrdinalIgnoreCase), result.Error);
    }

    [TestMethod]
    public void CustomScheme_UnknownState_IsIgnored()
    {
        var broker = new AuthBrokerService(new RecordingPlatform(), new FakeFactory(new FakeListener()), LongTimeout, MaxTimeout);

        var handled = broker.TryCompleteCustomSchemeRedirect("x-cmdpal://auth/callback?code=x&state=does-not-exist");

        Assert.IsFalse(handled);
    }

    [TestMethod]
    public async Task CustomScheme_HappyPath_RoutesByState_AndIsSingleUse()
    {
        var platform = new RecordingPlatform();
        var broker = new AuthBrokerService(platform, new FakeFactory(new FakeListener()), LongTimeout, MaxTimeout);

        var task = broker.RunFlowAsync(CustomSchemeRequest(), null, CancellationToken.None);
        Assert.IsTrue(platform.LaunchedSignal.Wait(TimeSpan.FromSeconds(5)));

        var state = QueryValue(platform.LaunchedUri!, "state");
        Assert.AreEqual(AuthBrokerService.CustomSchemeRedirectUri, QueryValue(platform.LaunchedUri!, "redirect_uri"));

        var handled = broker.TryCompleteCustomSchemeRedirect($"x-cmdpal://auth/callback?code=xyz&state={state}");
        Assert.IsTrue(handled);

        var result = await task;
        Assert.IsTrue(result.IsSuccessful, result.Error);
        Assert.AreEqual("xyz", result.ResponseParameters["code"]);
        Assert.AreEqual(AuthBrokerService.CustomSchemeRedirectUri, result.RedirectUri);

        // Single-use: a second activation for the same state is ignored.
        var second = broker.TryCompleteCustomSchemeRedirect($"x-cmdpal://auth/callback?code=xyz&state={state}");
        Assert.IsFalse(second);
    }

    private static AuthorizationRequest LoopbackRequest() => new()
    {
        DisplayName = "Test Provider",
        AuthorizationEndpoint = "https://idp.example/authorize",
        RedirectKind = AuthorizationRedirectKind.Loopback,
        Parameters = new Dictionary<string, string> { ["client_id"] = "abc" },
    };

    private static AuthorizationRequest CustomSchemeRequest() => new()
    {
        DisplayName = "Test Provider",
        AuthorizationEndpoint = "https://idp.example/authorize",
        RedirectKind = AuthorizationRedirectKind.CustomScheme,
        Parameters = new Dictionary<string, string> { ["client_id"] = "abc" },
    };

    private static string QueryValue(Uri uri, string key)
    {
        var query = uri.Query.StartsWith('?') ? uri.Query[1..] : uri.Query;
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var k = eq < 0 ? pair : pair[..eq];
            if (string.Equals(Uri.UnescapeDataString(k), key, StringComparison.Ordinal))
            {
                return eq < 0 ? string.Empty : Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return string.Empty;
    }

    private sealed class RecordingPlatform : IAuthBrokerPlatform
    {
        public Uri? LaunchedUri { get; private set; }

        public int ForegroundCount { get; private set; }

        public ManualResetEventSlim LaunchedSignal { get; } = new(false);

        public void LaunchBrowser(Uri authorizationUri)
        {
            LaunchedUri = authorizationUri;
            LaunchedSignal.Set();
        }

        public void BringToForeground() => ForegroundCount++;
    }

    private sealed class FakeListener : ILoopbackRedirectListener
    {
        public string RedirectUri { get; set; } = "http://127.0.0.1:5000/";

        public bool Disposed { get; private set; }

        public Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>>? OnWait { get; set; }

        public Task<IReadOnlyDictionary<string, string>> WaitForRedirectAsync(CancellationToken cancellationToken)
            => OnWait is null
                ? Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>())
                : OnWait(cancellationToken);

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeFactory : ILoopbackRedirectListenerFactory
    {
        private readonly ILoopbackRedirectListener _listener;

        public FakeFactory(ILoopbackRedirectListener listener) => _listener = listener;

        public ILoopbackRedirectListener Create() => _listener;
    }
}
