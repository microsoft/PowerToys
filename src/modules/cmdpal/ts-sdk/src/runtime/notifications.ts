// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Extension to Host notification channel. The runtime installs a sink when the
 * JSON-RPC server starts; before then, notifications are silently dropped so
 * that calling SDK code never throws when running outside a host (for example
 * in unit tests).
 */

export type NotificationSink = (method: string, params?: unknown) => void;

let sink: NotificationSink | null = null;

/** Installs the sink used by {@link sendNotification}. Internal. */
export function setNotificationSink(next: NotificationSink | null): void {
  sink = next;
}

/**
 * Sends an Extension to Host JSON-RPC notification, such as
 * `listPage/itemsChanged` or `command/propChanged`.
 */
export function sendNotification(method: string, params?: unknown): void {
  sink?.(method, params);
}
