// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace TopToolbar.Providers
{
    /// <summary>
    /// Optional interface a provider can implement to publish fine-grained change notifications.
    /// Implement this when the provider's underlying data can change at runtime (file watch, external service, user edits)
    /// and you want the toolbar/UI to refresh incrementally instead of forcing a full rebuild.
    /// <para>
    /// Guidance:
    /// <list type="bullet">
    /// <item><description>Use <see cref="ProviderChangeKind.ActionsUpdated"/> when existing actions' metadata (title, enabled state, icon) changed but ids are stable.</description></item>
    /// <item><description>Use <see cref="ProviderChangeKind.ActionsAdded"/> / <see cref="ProviderChangeKind.ActionsRemoved"/> for structural additions/removals.</description></item>
    /// <item><description>Use <see cref="ProviderChangeKind.GroupUpdated"/> when group level properties (name, layout) changed without removing all buttons.</description></item>
    /// <item><description>Use <see cref="ProviderChangeKind.BulkRefresh"/> only when you cannot compute a diff; UI will rebuild all groups/actions for this provider.</description></item>
    /// <item><description>Use <see cref="ProviderChangeKind.Reset"/> for catastrophic changes (e.g. provider lost state) – runtime should drop caches then rediscover.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Threading: Events may be raised from background threads; the runtime assigns a monotonically increasing version and forwards on a thread-pool thread.
    /// UI code must marshal to the UI thread (the window already does this). Providers should avoid long blocking work inside event invocation.
    /// </para>
    /// <para>
    /// Factory helpers on <see cref="ProviderChangedEventArgs"/> (<c>ActionsUpdated</c>, <c>ActionsAdded</c>, etc.) cover common patterns; use a custom instance only when you need a payload.
    /// </para>
    /// </summary>
    public interface IChangeNotifyingActionProvider
    {
        /// <summary>
        /// Raised whenever the provider's exposed groups or actions change.
        /// Provide only the impacted ids to enable minimal UI refresh. Null lists mean "unspecified"; empty lists mean "known none".
        /// </summary>
        event EventHandler<ProviderChangedEventArgs> ProviderChanged;
    }
}
