using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Bootstrapper.ViewModels.Util
{
  /// <summary>
  ///   This class contains methods for the CommandManager that help avoid memory leaks by
  ///   using weak references.
  /// </summary>
  internal static class CommandManagerHelper
  {
    internal static void CallWeakReferenceHandlers(List<WeakReference> handlers)
    {
      if (handlers == null)
        return;

      // Take a snapshot of the handlers before we call out to them since the handlers
      // could cause the array to me modified while we are reading it.

      var callees = new EventHandler[handlers.Count];
      var count = 0;

      for (var i = handlers.Count - 1; i >= 0; i--)
      {
        var reference = handlers[i];
        if (!(reference.Target is EventHandler handler))
        {
          // Clean up old handlers that have been collected
          handlers.RemoveAt(i);
        }
        else
        {
          callees[count] = handler;
          count++;
        }
      }

      // Call the handlers that we snapshot
      for (var i = 0; i < count; i++)
      {
        var handler = callees[i];
        handler(null, EventArgs.Empty);
      }
    }

    internal static void AddHandlersToRequerySuggested(IEnumerable<WeakReference> handlers)
    {
      if (handlers == null)
        return;

      foreach (var handlerRef in handlers)
      {
        if (handlerRef.Target is EventHandler handler)
          CommandManager.RequerySuggested += handler;
      }
    }

    internal static void RemoveHandlersFromRequerySuggested(IEnumerable<WeakReference> handlers)
    {
      if (handlers == null)
        return;

      foreach (var handlerRef in handlers)
      {
        if (handlerRef.Target is EventHandler handler)
          CommandManager.RequerySuggested -= handler;
      }
    }

    internal static void AddWeakReferenceHandler(ref List<WeakReference> handlers, EventHandler handler, int defaultListSize)
    {
      if (handlers == null)
      {
        if (defaultListSize > 0)
          handlers = new List<WeakReference>(defaultListSize);
        else
          handlers = new List<WeakReference>();
      }

      handlers.Add(new WeakReference(handler));
    }

    internal static void RemoveWeakReferenceHandler(List<WeakReference> handlers, EventHandler handler)
    {
      if (handlers == null)
        return;

      for (var i = handlers.Count - 1; i >= 0; i--)
      {
        var reference = handlers[i];
        if (!(reference.Target is EventHandler existingHandler) || existingHandler == handler)
        {
          // Clean up old handlers that have been collected
          // in addition to the handler that is to be removed.
          handlers.RemoveAt(i);
        }
      }
    }
  }
}