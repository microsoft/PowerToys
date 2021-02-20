import { Iterable } from './iterator.js';
/**
 * Enables logging of potentially leaked disposables.
 *
 * A disposable is considered leaked if it is not disposed or not registered as the child of
 * another disposable. This tracking is very simple an only works for classes that either
 * extend Disposable or use a DisposableStore. This means there are a lot of false positives.
 */
const TRACK_DISPOSABLES = false;
let disposableTracker = null;
if (TRACK_DISPOSABLES) {
    const __is_disposable_tracked__ = '__is_disposable_tracked__';
    disposableTracker = new class {
        trackDisposable(x) {
            const stack = new Error('Potentially leaked disposable').stack;
            setTimeout(() => {
                if (!x[__is_disposable_tracked__]) {
                    console.log(stack);
                }
            }, 3000);
        }
        markTracked(x) {
            if (x && x !== Disposable.None) {
                try {
                    x[__is_disposable_tracked__] = true;
                }
                catch (_a) {
                    // noop
                }
            }
        }
    };
}
function markTracked(x) {
    if (!disposableTracker) {
        return;
    }
    disposableTracker.markTracked(x);
}
export function trackDisposable(x) {
    if (!disposableTracker) {
        return x;
    }
    disposableTracker.trackDisposable(x);
    return x;
}
export class MultiDisposeError extends Error {
    constructor(errors) {
        super(`Encounter errors while disposing of store. Errors: [${errors.join(', ')}]`);
        this.errors = errors;
    }
}
export function isDisposable(thing) {
    return typeof thing.dispose === 'function' && thing.dispose.length === 0;
}
export function dispose(arg) {
    if (Iterable.is(arg)) {
        let errors = [];
        for (const d of arg) {
            if (d) {
                markTracked(d);
                try {
                    d.dispose();
                }
                catch (e) {
                    errors.push(e);
                }
            }
        }
        if (errors.length === 1) {
            throw errors[0];
        }
        else if (errors.length > 1) {
            throw new MultiDisposeError(errors);
        }
        return Array.isArray(arg) ? [] : arg;
    }
    else if (arg) {
        markTracked(arg);
        arg.dispose();
        return arg;
    }
}
export function combinedDisposable(...disposables) {
    disposables.forEach(markTracked);
    return toDisposable(() => dispose(disposables));
}
export function toDisposable(fn) {
    const self = trackDisposable({
        dispose: () => {
            markTracked(self);
            fn();
        }
    });
    return self;
}
export class DisposableStore {
    constructor() {
        this._toDispose = new Set();
        this._isDisposed = false;
    }
    /**
     * Dispose of all registered disposables and mark this object as disposed.
     *
     * Any future disposables added to this object will be disposed of on `add`.
     */
    dispose() {
        if (this._isDisposed) {
            return;
        }
        markTracked(this);
        this._isDisposed = true;
        this.clear();
    }
    /**
     * Dispose of all registered disposables but do not mark this object as disposed.
     */
    clear() {
        try {
            dispose(this._toDispose.values());
        }
        finally {
            this._toDispose.clear();
        }
    }
    add(t) {
        if (!t) {
            return t;
        }
        if (t === this) {
            throw new Error('Cannot register a disposable on itself!');
        }
        markTracked(t);
        if (this._isDisposed) {
            if (!DisposableStore.DISABLE_DISPOSED_WARNING) {
                console.warn(new Error('Trying to add a disposable to a DisposableStore that has already been disposed of. The added object will be leaked!').stack);
            }
        }
        else {
            this._toDispose.add(t);
        }
        return t;
    }
}
DisposableStore.DISABLE_DISPOSED_WARNING = false;
export class Disposable {
    constructor() {
        this._store = new DisposableStore();
        trackDisposable(this);
    }
    dispose() {
        markTracked(this);
        this._store.dispose();
    }
    _register(t) {
        if (t === this) {
            throw new Error('Cannot register a disposable on itself!');
        }
        return this._store.add(t);
    }
}
Disposable.None = Object.freeze({ dispose() { } });
/**
 * Manages the lifecycle of a disposable value that may be changed.
 *
 * This ensures that when the disposable value is changed, the previously held disposable is disposed of. You can
 * also register a `MutableDisposable` on a `Disposable` to ensure it is automatically cleaned up.
 */
export class MutableDisposable {
    constructor() {
        this._isDisposed = false;
        trackDisposable(this);
    }
    get value() {
        return this._isDisposed ? undefined : this._value;
    }
    set value(value) {
        if (this._isDisposed || value === this._value) {
            return;
        }
        if (this._value) {
            this._value.dispose();
        }
        if (value) {
            markTracked(value);
        }
        this._value = value;
    }
    clear() {
        this.value = undefined;
    }
    dispose() {
        this._isDisposed = true;
        markTracked(this);
        if (this._value) {
            this._value.dispose();
        }
        this._value = undefined;
    }
}
export class ImmortalReference {
    constructor(object) {
        this.object = object;
    }
    dispose() { }
}
