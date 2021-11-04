let memoizeId = 0;
export function createMemoizer() {
    const memoizeKeyPrefix = `$memoize${memoizeId++}`;
    let self = undefined;
    const result = function memoize(target, key, descriptor) {
        let fnKey = null;
        let fn = null;
        if (typeof descriptor.value === 'function') {
            fnKey = 'value';
            fn = descriptor.value;
            if (fn.length !== 0) {
                console.warn('Memoize should only be used in functions with zero parameters');
            }
        }
        else if (typeof descriptor.get === 'function') {
            fnKey = 'get';
            fn = descriptor.get;
        }
        if (!fn) {
            throw new Error('not supported');
        }
        const memoizeKey = `${memoizeKeyPrefix}:${key}`;
        descriptor[fnKey] = function (...args) {
            self = this;
            if (!this.hasOwnProperty(memoizeKey)) {
                Object.defineProperty(this, memoizeKey, {
                    configurable: true,
                    enumerable: false,
                    writable: true,
                    value: fn.apply(this, args)
                });
            }
            return this[memoizeKey];
        };
    };
    result.clear = () => {
        if (typeof self === 'undefined') {
            return;
        }
        Object.getOwnPropertyNames(self).forEach(property => {
            if (property.indexOf(memoizeKeyPrefix) === 0) {
                delete self[property];
            }
        });
    };
    return result;
}
export function memoize(target, key, descriptor) {
    return createMemoizer()(target, key, descriptor);
}
