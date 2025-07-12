// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Implements a weak event listener that allows the owner to be garbage
/// collected if its only remaining link is an event handler.
/// </summary>
/// <typeparam name="TInstance">Type of instance listening for the event.</typeparam>
/// <typeparam name="TSource">Type of source for the event.</typeparam>
/// <typeparam name="TEventArgs">Type of event arguments for the event.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
internal sealed class WeakEventListener<TInstance, TSource, TEventArgs>
    where TInstance : class
{
    /// <summary>
    /// WeakReference to the instance listening for the event.
    /// </summary>
    private readonly WeakReference<TInstance> _weakInstance;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeakEventListener{TInstance, TSource, TEventArgs}"/> class.
    /// </summary>
    /// <param name="instance">Instance subscribing to the event.</param>
    /// <param name="onEventAction">Event handler executed when event is raised.</param>
    /// <param name="onDetachAction">Action to execute when instance was collected.</param>
    public WeakEventListener(
        TInstance instance,
        Action<TInstance, TSource, TEventArgs>? onEventAction = null,
        Action<WeakEventListener<TInstance, TSource, TEventArgs>>? onDetachAction = null)
    {
        ArgumentNullException.ThrowIfNull(instance);

        _weakInstance = new(instance);
        OnEventAction = onEventAction;
        OnDetachAction = onDetachAction;
    }

    /// <summary>
    /// Gets or sets the method to call when the event fires.
    /// </summary>
    public Action<TInstance, TSource, TEventArgs>? OnEventAction { get; set; }

    /// <summary>
    /// Gets or sets the method to call when detaching from the event.
    /// </summary>
    public Action<WeakEventListener<TInstance, TSource, TEventArgs>>? OnDetachAction { get; set; }

    /// <summary>
    /// Handler for the subscribed event calls OnEventAction to handle it.
    /// </summary>
    /// <param name="source">Event source.</param>
    /// <param name="eventArgs">Event arguments.</param>
    public void OnEvent(TSource source, TEventArgs eventArgs)
    {
        if (_weakInstance.TryGetTarget(out var target))
        {
            // Call registered action
            OnEventAction?.Invoke(target, source, eventArgs);
        }
        else
        {
            // Detach from event
            Detach();
        }
    }

    /// <summary>
    /// Detaches from the subscribed event.
    /// </summary>
    public void Detach()
    {
        OnDetachAction?.Invoke(this);
        OnDetachAction = null;
    }
}
