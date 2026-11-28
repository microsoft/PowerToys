// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Media;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Raises UIA notification events so screen readers speak results that have no focus change
    /// (saved, deleted, filter counts, recorded keys, validation messages).
    /// </summary>
    internal static class AccessibilityAnnouncer
    {
        private const string ActivityId = "KeyboardManagerEditor";

        public static void Announce(UIElement? source, string? message, bool interrupt = true)
        {
            if (source == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                AutomationPeer? peer = FindPeer(source);
                if (peer == null)
                {
                    Logger.LogWarning("No automation peer available to raise an accessibility notification.");
                    return;
                }

                peer.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    interrupt ? AutomationNotificationProcessing.ImportantMostRecent : AutomationNotificationProcessing.ImportantAll,
                    message,
                    ActivityId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to raise accessibility notification: {ex.Message}");
            }
        }

        // Pages, UserControls and panels have no automation peer, so a notification raised from them is
        // silently dropped. Fall back to the nearest descendant that is exposed to UI Automation.
        private static AutomationPeer? FindPeer(UIElement source)
        {
            var pending = new Queue<DependencyObject>();
            pending.Enqueue(source);

            while (pending.Count > 0)
            {
                DependencyObject current = pending.Dequeue();
                if (current is UIElement element)
                {
                    AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(element)
                        ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
                    if (peer != null)
                    {
                        return peer;
                    }
                }

                int childCount = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < childCount; i++)
                {
                    pending.Enqueue(VisualTreeHelper.GetChild(current, i));
                }
            }

            return null;
        }
    }
}
