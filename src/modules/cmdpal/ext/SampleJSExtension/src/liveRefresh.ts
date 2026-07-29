// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Drives periodic refresh notifications for the live-updating sample pages while
 * a page is actually being observed by the host.
 *
 * The JS protocol has no page unload signal, so a plain `setInterval` started in
 * `getItems()` would run for the life of the process and keep sending refresh
 * traffic long after the user navigated away. It would also leave one timer
 * behind for every page instance that was ever visited.
 *
 * This helper avoids both problems. A page calls {@link LiveRefresh.observe} at
 * the top of `getItems()`. That records the moment of the fetch and arms a
 * single timer if one is not already running. On each tick the timer checks how
 * long it has been since the last fetch: while the page is on screen the host
 * keeps re-fetching in response to the notifications, so the gap stays small and
 * the timer keeps ticking. Once the page is no longer visible the host stops
 * fetching, the gap grows past the idle threshold, and the timer stops itself.
 * The next `observe` call (when the user navigates back) arms it again. At most
 * one timer runs per page, and only while that page is being viewed.
 */
export class LiveRefresh {
  private timer: NodeJS.Timeout | undefined;
  private lastFetch = 0;

  /**
   * @param intervalMs How often to fire while the page is being observed.
   * @param onTick Invoked on each tick to update state and notify the host.
   */
  constructor(
    private readonly intervalMs: number,
    private readonly onTick: () => void,
  ) {}

  /** Marks the page as observed and arms the refresh timer if it is not running. */
  observe(): void {
    this.lastFetch = Date.now();
    if (this.timer !== undefined) {
      return;
    }

    // A missed fetch cycle means the host stopped asking for items, which happens
    // when the page is no longer visible. Allow a little slack before stopping.
    const idleThreshold = this.intervalMs * 3;

    this.timer = setInterval(() => {
      if (Date.now() - this.lastFetch > idleThreshold) {
        this.stop();
        return;
      }

      this.onTick();
    }, this.intervalMs);

    // Do not keep the Node.js process alive solely for this timer.
    this.timer.unref?.();
  }

  private stop(): void {
    if (this.timer !== undefined) {
      clearInterval(this.timer);
      this.timer = undefined;
    }
  }
}
