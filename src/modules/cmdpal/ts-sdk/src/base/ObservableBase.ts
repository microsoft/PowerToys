// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import type { ObservablePropertyName } from '../types.js';
import { sendNotification } from '../runtime/notifications.js';

/** Shared property-change notification support for observable SDK models. */
export abstract class ObservableBase {
  protected abstract readonly notificationId: string;

  /**
   * Tells the host that one of this object's ABI properties changed.
   * The current value is included so the host can update without a round trip.
   */
  protected notifyPropChanged(propertyName: ObservablePropertyName): void {
    sendNotification('command/propChanged', {
      commandId: this.notificationId,
      properties: { [propertyName]: Reflect.get(this, propertyName) },
    });
  }
}
