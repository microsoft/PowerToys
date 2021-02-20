/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { isFalsyOrWhitespace } from '../../../base/common/strings.js';
import { createDecorator } from '../../instantiation/common/instantiation.js';
import { userAgent, isMacintosh, isLinux, isWindows, isWeb } from '../../../base/common/platform.js';
let _userAgent = userAgent || '';
const STATIC_VALUES = new Map();
STATIC_VALUES.set('false', false);
STATIC_VALUES.set('true', true);
STATIC_VALUES.set('isMac', isMacintosh);
STATIC_VALUES.set('isLinux', isLinux);
STATIC_VALUES.set('isWindows', isWindows);
STATIC_VALUES.set('isWeb', isWeb);
STATIC_VALUES.set('isMacNative', isMacintosh && !isWeb);
STATIC_VALUES.set('isEdge', _userAgent.indexOf('Edg/') >= 0);
STATIC_VALUES.set('isFirefox', _userAgent.indexOf('Firefox') >= 0);
STATIC_VALUES.set('isChrome', _userAgent.indexOf('Chrome') >= 0);
STATIC_VALUES.set('isSafari', _userAgent.indexOf('Safari') >= 0);
STATIC_VALUES.set('isIPad', _userAgent.indexOf('iPad') >= 0);
const hasOwnProperty = Object.prototype.hasOwnProperty;
export class ContextKeyExpr {
    static has(key) {
        return ContextKeyDefinedExpr.create(key);
    }
    static equals(key, value) {
        return ContextKeyEqualsExpr.create(key, value);
    }
    static regex(key, value) {
        return ContextKeyRegexExpr.create(key, value);
    }
    static not(key) {
        return ContextKeyNotExpr.create(key);
    }
    static and(...expr) {
        return ContextKeyAndExpr.create(expr);
    }
    static or(...expr) {
        return ContextKeyOrExpr.create(expr);
    }
    static deserialize(serialized, strict = false) {
        if (!serialized) {
            return undefined;
        }
        return this._deserializeOrExpression(serialized, strict);
    }
    static _deserializeOrExpression(serialized, strict) {
        let pieces = serialized.split('||');
        return ContextKeyOrExpr.create(pieces.map(p => this._deserializeAndExpression(p, strict)));
    }
    static _deserializeAndExpression(serialized, strict) {
        let pieces = serialized.split('&&');
        return ContextKeyAndExpr.create(pieces.map(p => this._deserializeOne(p, strict)));
    }
    static _deserializeOne(serializedOne, strict) {
        serializedOne = serializedOne.trim();
        if (serializedOne.indexOf('!=') >= 0) {
            let pieces = serializedOne.split('!=');
            return ContextKeyNotEqualsExpr.create(pieces[0].trim(), this._deserializeValue(pieces[1], strict));
        }
        if (serializedOne.indexOf('==') >= 0) {
            let pieces = serializedOne.split('==');
            return ContextKeyEqualsExpr.create(pieces[0].trim(), this._deserializeValue(pieces[1], strict));
        }
        if (serializedOne.indexOf('=~') >= 0) {
            let pieces = serializedOne.split('=~');
            return ContextKeyRegexExpr.create(pieces[0].trim(), this._deserializeRegexValue(pieces[1], strict));
        }
        if (serializedOne.indexOf(' in ') >= 0) {
            let pieces = serializedOne.split(' in ');
            return ContextKeyInExpr.create(pieces[0].trim(), pieces[1].trim());
        }
        if (/^[^<=>]+>=[^<=>]+$/.test(serializedOne)) {
            const pieces = serializedOne.split('>=');
            return ContextKeyGreaterEqualsExpr.create(pieces[0].trim(), pieces[1].trim());
        }
        if (/^[^<=>]+>[^<=>]+$/.test(serializedOne)) {
            const pieces = serializedOne.split('>');
            return ContextKeyGreaterExpr.create(pieces[0].trim(), pieces[1].trim());
        }
        if (/^[^<=>]+<=[^<=>]+$/.test(serializedOne)) {
            const pieces = serializedOne.split('<=');
            return ContextKeySmallerEqualsExpr.create(pieces[0].trim(), pieces[1].trim());
        }
        if (/^[^<=>]+<[^<=>]+$/.test(serializedOne)) {
            const pieces = serializedOne.split('<');
            return ContextKeySmallerExpr.create(pieces[0].trim(), pieces[1].trim());
        }
        if (/^\!\s*/.test(serializedOne)) {
            return ContextKeyNotExpr.create(serializedOne.substr(1).trim());
        }
        return ContextKeyDefinedExpr.create(serializedOne);
    }
    static _deserializeValue(serializedValue, strict) {
        serializedValue = serializedValue.trim();
        if (serializedValue === 'true') {
            return true;
        }
        if (serializedValue === 'false') {
            return false;
        }
        let m = /^'([^']*)'$/.exec(serializedValue);
        if (m) {
            return m[1].trim();
        }
        return serializedValue;
    }
    static _deserializeRegexValue(serializedValue, strict) {
        if (isFalsyOrWhitespace(serializedValue)) {
            if (strict) {
                throw new Error('missing regexp-value for =~-expression');
            }
            else {
                console.warn('missing regexp-value for =~-expression');
            }
            return null;
        }
        let start = serializedValue.indexOf('/');
        let end = serializedValue.lastIndexOf('/');
        if (start === end || start < 0 /* || to < 0 */) {
            if (strict) {
                throw new Error(`bad regexp-value '${serializedValue}', missing /-enclosure`);
            }
            else {
                console.warn(`bad regexp-value '${serializedValue}', missing /-enclosure`);
            }
            return null;
        }
        let value = serializedValue.slice(start + 1, end);
        let caseIgnoreFlag = serializedValue[end + 1] === 'i' ? 'i' : '';
        try {
            return new RegExp(value, caseIgnoreFlag);
        }
        catch (e) {
            if (strict) {
                throw new Error(`bad regexp-value '${serializedValue}', parse error: ${e}`);
            }
            else {
                console.warn(`bad regexp-value '${serializedValue}', parse error: ${e}`);
            }
            return null;
        }
    }
}
function cmp(a, b) {
    return a.cmp(b);
}
export class ContextKeyFalseExpr {
    constructor() {
        this.type = 0 /* False */;
    }
    cmp(other) {
        return this.type - other.type;
    }
    equals(other) {
        return (other.type === this.type);
    }
    evaluate(context) {
        return false;
    }
    serialize() {
        return 'false';
    }
    keys() {
        return [];
    }
    negate() {
        return ContextKeyTrueExpr.INSTANCE;
    }
}
ContextKeyFalseExpr.INSTANCE = new ContextKeyFalseExpr();
export class ContextKeyTrueExpr {
    constructor() {
        this.type = 1 /* True */;
    }
    cmp(other) {
        return this.type - other.type;
    }
    equals(other) {
        return (other.type === this.type);
    }
    evaluate(context) {
        return true;
    }
    serialize() {
        return 'true';
    }
    keys() {
        return [];
    }
    negate() {
        return ContextKeyFalseExpr.INSTANCE;
    }
}
ContextKeyTrueExpr.INSTANCE = new ContextKeyTrueExpr();
export class ContextKeyDefinedExpr {
    constructor(key) {
        this.key = key;
        this.type = 2 /* Defined */;
    }
    static create(key) {
        const staticValue = STATIC_VALUES.get(key);
        if (typeof staticValue === 'boolean') {
            return staticValue ? ContextKeyTrueExpr.INSTANCE : ContextKeyFalseExpr.INSTANCE;
        }
        return new ContextKeyDefinedExpr(key);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return cmp1(this.key, other.key);
    }
    equals(other) {
        if (other.type === this.type) {
            return (this.key === other.key);
        }
        return false;
    }
    evaluate(context) {
        return (!!context.getValue(this.key));
    }
    serialize() {
        return this.key;
    }
    keys() {
        return [this.key];
    }
    negate() {
        return ContextKeyNotExpr.create(this.key);
    }
}
export class ContextKeyEqualsExpr {
    constructor(key, value) {
        this.key = key;
        this.value = value;
        this.type = 4 /* Equals */;
    }
    static create(key, value) {
        if (typeof value === 'boolean') {
            return (value ? ContextKeyDefinedExpr.create(key) : ContextKeyNotExpr.create(key));
        }
        const staticValue = STATIC_VALUES.get(key);
        if (typeof staticValue === 'boolean') {
            const trueValue = staticValue ? 'true' : 'false';
            return (value === trueValue ? ContextKeyTrueExpr.INSTANCE : ContextKeyFalseExpr.INSTANCE);
        }
        return new ContextKeyEqualsExpr(key, value);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return cmp2(this.key, this.value, other.key, other.value);
    }
    equals(other) {
        if (other.type === this.type) {
            return (this.key === other.key && this.value === other.value);
        }
        return false;
    }
    evaluate(context) {
        // Intentional ==
        // eslint-disable-next-line eqeqeq
        return (context.getValue(this.key) == this.value);
    }
    serialize() {
        return `${this.key} == '${this.value}'`;
    }
    keys() {
        return [this.key];
    }
    negate() {
        return ContextKeyNotEqualsExpr.create(this.key, this.value);
    }
}
export class ContextKeyInExpr {
    constructor(key, valueKey) {
        this.key = key;
        this.valueKey = valueKey;
        this.type = 10 /* In */;
    }
    static create(key, valueKey) {
        return new ContextKeyInExpr(key, valueKey);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return cmp2(this.key, this.valueKey, other.key, other.valueKey);
    }
    equals(other) {
        if (other.type === this.type) {
            return (this.key === other.key && this.valueKey === other.valueKey);
        }
        return false;
    }
    evaluate(context) {
        const source = context.getValue(this.valueKey);
        const item = context.getValue(this.key);
        if (Array.isArray(source)) {
            return (source.indexOf(item) >= 0);
        }
        if (typeof item === 'string' && typeof source === 'object' && source !== null) {
            return hasOwnProperty.call(source, item);
        }
        return false;
    }
    serialize() {
        return `${this.key} in '${this.valueKey}'`;
    }
    keys() {
        return [this.key, this.valueKey];
    }
    negate() {
        return ContextKeyNotInExpr.create(this);
    }
}
export class ContextKeyNotInExpr {
    constructor(_actual) {
        this._actual = _actual;
        this.type = 11 /* NotIn */;
        //
    }
    static create(actual) {
        return new ContextKeyNotInExpr(actual);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return this._actual.cmp(other._actual);
    }
    equals(other) {
        if (other.type === this.type) {
            return this._actual.equals(other._actual);
        }
        return false;
    }
    evaluate(context) {
        return !this._actual.evaluate(context);
    }
    serialize() {
        throw new Error('Method not implemented.');
    }
    keys() {
        return this._actual.keys();
    }
    negate() {
        return this._actual;
    }
}
export class ContextKeyNotEqualsExpr {
    constructor(key, value) {
        this.key = key;
        this.value = value;
        this.type = 5 /* NotEquals */;
    }
    static create(key, value) {
        if (typeof value === 'boolean') {
            if (value) {
                return ContextKeyNotExpr.create(key);
            }
            return ContextKeyDefinedExpr.create(key);
        }
        const staticValue = STATIC_VALUES.get(key);
        if (typeof staticValue === 'boolean') {
            const falseValue = staticValue ? 'true' : 'false';
            return (value === falseValue ? ContextKeyFalseExpr.INSTANCE : ContextKeyTrueExpr.INSTANCE);
        }
        return new ContextKeyNotEqualsExpr(key, value);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return cmp2(this.key, this.value, other.key, other.value);
    }
    equals(other) {
        if (other.type === this.type) {
            return (this.key === other.key && this.value === other.value);
        }
        return false;
    }
    evaluate(context) {
        // Intentional !=
        // eslint-disable-next-line eqeqeq
        return (context.getValue(this.key) != this.value);
    }
    serialize() {
        return `${this.key} != '${this.value}'`;
    }
    keys() {
        return [this.key];
    }
    negate() {
        return ContextKeyEqualsExpr.create(this.key, this.value);
    }
}
export class ContextKeyNotExpr {
    constructor(key) {
        this.key = key;
        this.type = 3 /* Not */;
    }
    static create(key) {
        const staticValue = STATIC_VALUES.get(key);
        if (typeof staticValue === 'boolean') {
            return (staticValue ? ContextKeyFalseExpr.INSTANCE : ContextKeyTrueExpr.INSTANCE);
        }
        return new ContextKeyNotExpr(key);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return cmp1(this.key, other.key);
    }
    equals(other) {
        if (other.type === this.type) {
            return (this.key === other.key);
        }
        return false;
    }
    evaluate(context) {
        return (!context.getValue(this.key));
    }
    serialize() {
        return `!${this.key}`;
    }
    keys() {
        return [this.key];
    }
    negate() {
        return ContextKeyDefinedExpr.create(this.key);
    }
}
export class ContextKeyGreaterExpr {
    constructor(key, value) {
        this.key = key;
        this.value = value;
        this.type = 12 /* Greater */;
    }
    static create(key, value) {
        return new ContextKeyGreaterExpr(key, value);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return cmp2(this.key, this.value, other.key, other.value);
    }
    equals(other) {
        if (other.type === this.type) {
            return (this.key === other.key && this.value === other.value);
        }
        return false;
    }
    evaluate(context) {
        return (parseFloat(context.getValue(this.key)) > parseFloat(this.value));
    }
    serialize() {
        return `${this.key} > ${this.value}`;
    }
    keys() {
        return [this.key];
    }
    negate() {
        return ContextKeySmallerEqualsExpr.create(this.key, this.value);
    }
}
export class ContextKeyGreaterEqualsExpr {
    constructor(key, value) {
        this.key = key;
        this.value = value;
        this.type = 13 /* GreaterEquals */;
    }
    static create(key, value) {
        return new ContextKeyGreaterEqualsExpr(key, value);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return cmp2(this.key, this.value, other.key, other.value);
    }
    equals(other) {
        if (other.type === this.type) {
            return (this.key === other.key && this.value === other.value);
        }
        return false;
    }
    evaluate(context) {
        return (parseFloat(context.getValue(this.key)) >= parseFloat(this.value));
    }
    serialize() {
        return `${this.key} >= ${this.value}`;
    }
    keys() {
        return [this.key];
    }
    negate() {
        return ContextKeySmallerExpr.create(this.key, this.value);
    }
}
export class ContextKeySmallerExpr {
    constructor(key, value) {
        this.key = key;
        this.value = value;
        this.type = 14 /* Smaller */;
    }
    static create(key, value) {
        return new ContextKeySmallerExpr(key, value);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return cmp2(this.key, this.value, other.key, other.value);
    }
    equals(other) {
        if (other.type === this.type) {
            return (this.key === other.key && this.value === other.value);
        }
        return false;
    }
    evaluate(context) {
        return (parseFloat(context.getValue(this.key)) < parseFloat(this.value));
    }
    serialize() {
        return `${this.key} < ${this.value}`;
    }
    keys() {
        return [this.key];
    }
    negate() {
        return ContextKeyGreaterEqualsExpr.create(this.key, this.value);
    }
}
export class ContextKeySmallerEqualsExpr {
    constructor(key, value) {
        this.key = key;
        this.value = value;
        this.type = 15 /* SmallerEquals */;
    }
    static create(key, value) {
        return new ContextKeySmallerEqualsExpr(key, value);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return cmp2(this.key, this.value, other.key, other.value);
    }
    equals(other) {
        if (other.type === this.type) {
            return (this.key === other.key && this.value === other.value);
        }
        return false;
    }
    evaluate(context) {
        return (parseFloat(context.getValue(this.key)) <= parseFloat(this.value));
    }
    serialize() {
        return `${this.key} <= ${this.value}`;
    }
    keys() {
        return [this.key];
    }
    negate() {
        return ContextKeyGreaterExpr.create(this.key, this.value);
    }
}
export class ContextKeyRegexExpr {
    constructor(key, regexp) {
        this.key = key;
        this.regexp = regexp;
        this.type = 7 /* Regex */;
        //
    }
    static create(key, regexp) {
        return new ContextKeyRegexExpr(key, regexp);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        if (this.key < other.key) {
            return -1;
        }
        if (this.key > other.key) {
            return 1;
        }
        const thisSource = this.regexp ? this.regexp.source : '';
        const otherSource = other.regexp ? other.regexp.source : '';
        if (thisSource < otherSource) {
            return -1;
        }
        if (thisSource > otherSource) {
            return 1;
        }
        return 0;
    }
    equals(other) {
        if (other.type === this.type) {
            const thisSource = this.regexp ? this.regexp.source : '';
            const otherSource = other.regexp ? other.regexp.source : '';
            return (this.key === other.key && thisSource === otherSource);
        }
        return false;
    }
    evaluate(context) {
        let value = context.getValue(this.key);
        return this.regexp ? this.regexp.test(value) : false;
    }
    serialize() {
        const value = this.regexp
            ? `/${this.regexp.source}/${this.regexp.ignoreCase ? 'i' : ''}`
            : '/invalid/';
        return `${this.key} =~ ${value}`;
    }
    keys() {
        return [this.key];
    }
    negate() {
        return ContextKeyNotRegexExpr.create(this);
    }
}
export class ContextKeyNotRegexExpr {
    constructor(_actual) {
        this._actual = _actual;
        this.type = 8 /* NotRegex */;
        //
    }
    static create(actual) {
        return new ContextKeyNotRegexExpr(actual);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        return this._actual.cmp(other._actual);
    }
    equals(other) {
        if (other.type === this.type) {
            return this._actual.equals(other._actual);
        }
        return false;
    }
    evaluate(context) {
        return !this._actual.evaluate(context);
    }
    serialize() {
        throw new Error('Method not implemented.');
    }
    keys() {
        return this._actual.keys();
    }
    negate() {
        return this._actual;
    }
}
export class ContextKeyAndExpr {
    constructor(expr) {
        this.expr = expr;
        this.type = 6 /* And */;
    }
    static create(_expr) {
        return ContextKeyAndExpr._normalizeArr(_expr);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        if (this.expr.length < other.expr.length) {
            return -1;
        }
        if (this.expr.length > other.expr.length) {
            return 1;
        }
        for (let i = 0, len = this.expr.length; i < len; i++) {
            const r = cmp(this.expr[i], other.expr[i]);
            if (r !== 0) {
                return r;
            }
        }
        return 0;
    }
    equals(other) {
        if (other.type === this.type) {
            if (this.expr.length !== other.expr.length) {
                return false;
            }
            for (let i = 0, len = this.expr.length; i < len; i++) {
                if (!this.expr[i].equals(other.expr[i])) {
                    return false;
                }
            }
            return true;
        }
        return false;
    }
    evaluate(context) {
        for (let i = 0, len = this.expr.length; i < len; i++) {
            if (!this.expr[i].evaluate(context)) {
                return false;
            }
        }
        return true;
    }
    static _normalizeArr(arr) {
        const expr = [];
        let hasTrue = false;
        for (const e of arr) {
            if (!e) {
                continue;
            }
            if (e.type === 1 /* True */) {
                // anything && true ==> anything
                hasTrue = true;
                continue;
            }
            if (e.type === 0 /* False */) {
                // anything && false ==> false
                return ContextKeyFalseExpr.INSTANCE;
            }
            if (e.type === 6 /* And */) {
                expr.push(...e.expr);
                continue;
            }
            expr.push(e);
        }
        if (expr.length === 0 && hasTrue) {
            return ContextKeyTrueExpr.INSTANCE;
        }
        if (expr.length === 0) {
            return undefined;
        }
        if (expr.length === 1) {
            return expr[0];
        }
        expr.sort(cmp);
        // We must distribute any OR expression because we don't support parens
        // OR extensions will be at the end (due to sorting rules)
        while (expr.length > 1) {
            const lastElement = expr[expr.length - 1];
            if (lastElement.type !== 9 /* Or */) {
                break;
            }
            // pop the last element
            expr.pop();
            // pop the second to last element
            const secondToLastElement = expr.pop();
            // distribute `lastElement` over `secondToLastElement`
            const resultElement = ContextKeyOrExpr.create(lastElement.expr.map(el => ContextKeyAndExpr.create([el, secondToLastElement])));
            if (resultElement) {
                expr.push(resultElement);
                expr.sort(cmp);
            }
        }
        if (expr.length === 1) {
            return expr[0];
        }
        return new ContextKeyAndExpr(expr);
    }
    serialize() {
        return this.expr.map(e => e.serialize()).join(' && ');
    }
    keys() {
        const result = [];
        for (let expr of this.expr) {
            result.push(...expr.keys());
        }
        return result;
    }
    negate() {
        let result = [];
        for (let expr of this.expr) {
            result.push(expr.negate());
        }
        return ContextKeyOrExpr.create(result);
    }
}
export class ContextKeyOrExpr {
    constructor(expr) {
        this.expr = expr;
        this.type = 9 /* Or */;
    }
    static create(_expr) {
        const expr = ContextKeyOrExpr._normalizeArr(_expr);
        if (expr.length === 0) {
            return undefined;
        }
        if (expr.length === 1) {
            return expr[0];
        }
        return new ContextKeyOrExpr(expr);
    }
    cmp(other) {
        if (other.type !== this.type) {
            return this.type - other.type;
        }
        if (this.expr.length < other.expr.length) {
            return -1;
        }
        if (this.expr.length > other.expr.length) {
            return 1;
        }
        for (let i = 0, len = this.expr.length; i < len; i++) {
            const r = cmp(this.expr[i], other.expr[i]);
            if (r !== 0) {
                return r;
            }
        }
        return 0;
    }
    equals(other) {
        if (other.type === this.type) {
            if (this.expr.length !== other.expr.length) {
                return false;
            }
            for (let i = 0, len = this.expr.length; i < len; i++) {
                if (!this.expr[i].equals(other.expr[i])) {
                    return false;
                }
            }
            return true;
        }
        return false;
    }
    evaluate(context) {
        for (let i = 0, len = this.expr.length; i < len; i++) {
            if (this.expr[i].evaluate(context)) {
                return true;
            }
        }
        return false;
    }
    static _normalizeArr(arr) {
        let expr = [];
        let hasFalse = false;
        if (arr) {
            for (let i = 0, len = arr.length; i < len; i++) {
                const e = arr[i];
                if (!e) {
                    continue;
                }
                if (e.type === 0 /* False */) {
                    // anything || false ==> anything
                    hasFalse = true;
                    continue;
                }
                if (e.type === 1 /* True */) {
                    // anything || true ==> true
                    return [ContextKeyTrueExpr.INSTANCE];
                }
                if (e.type === 9 /* Or */) {
                    expr = expr.concat(e.expr);
                    continue;
                }
                expr.push(e);
            }
            if (expr.length === 0 && hasFalse) {
                return [ContextKeyFalseExpr.INSTANCE];
            }
            expr.sort(cmp);
        }
        return expr;
    }
    serialize() {
        return this.expr.map(e => e.serialize()).join(' || ');
    }
    keys() {
        const result = [];
        for (let expr of this.expr) {
            result.push(...expr.keys());
        }
        return result;
    }
    negate() {
        let result = [];
        for (let expr of this.expr) {
            result.push(expr.negate());
        }
        const terminals = (node) => {
            if (node.type === 9 /* Or */) {
                return node.expr;
            }
            return [node];
        };
        // We don't support parens, so here we distribute the AND over the OR terminals
        // We always take the first 2 AND pairs and distribute them
        while (result.length > 1) {
            const LEFT = result.shift();
            const RIGHT = result.shift();
            const all = [];
            for (const left of terminals(LEFT)) {
                for (const right of terminals(RIGHT)) {
                    all.push(ContextKeyExpr.and(left, right));
                }
            }
            result.unshift(ContextKeyExpr.or(...all));
        }
        return result[0];
    }
}
export class RawContextKey extends ContextKeyDefinedExpr {
    constructor(key, defaultValue) {
        super(key);
        this._defaultValue = defaultValue;
    }
    bindTo(target) {
        return target.createKey(this.key, this._defaultValue);
    }
    getValue(target) {
        return target.getContextKeyValue(this.key);
    }
    toNegated() {
        return ContextKeyExpr.not(this.key);
    }
    isEqualTo(value) {
        return ContextKeyExpr.equals(this.key, value);
    }
}
export const IContextKeyService = createDecorator('contextKeyService');
export const SET_CONTEXT_COMMAND_ID = 'setContext';
function cmp1(key1, key2) {
    if (key1 < key2) {
        return -1;
    }
    if (key1 > key2) {
        return 1;
    }
    return 0;
}
function cmp2(key1, value1, key2, value2) {
    if (key1 < key2) {
        return -1;
    }
    if (key1 > key2) {
        return 1;
    }
    if (value1 < value2) {
        return -1;
    }
    if (value1 > value2) {
        return 1;
    }
    return 0;
}
