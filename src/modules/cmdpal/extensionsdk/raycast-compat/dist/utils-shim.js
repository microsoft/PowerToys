"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.useFetch = exports.useCachedPromise = void 0;
exports.usePromise = usePromise;
exports.useCachedState = useCachedState;
exports.useExec = useExec;
exports.useSQL = useSQL;
exports.showFailureToast = showFailureToast;
exports.getFavicon = getFavicon;
exports.getProgressIcon = getProgressIcon;
exports.useFrecencySorting = useFrecencySorting;
exports.useForm = useForm;
exports.useLocalStorage = useLocalStorage;
exports.runAppleScript = runAppleScript;
/**
 * @raycast/utils compatibility shim.
 *
 * Raycast extensions import utility hooks from `@raycast/utils`.
 * This module re-exports compatible implementations from our api-stubs
 * and provides minimal stubs for the rest. The esbuild bundler aliases
 * `@raycast/utils` → this file.
 */
const react_1 = require("react");
const hooks_1 = require("./api-stubs/hooks");
Object.defineProperty(exports, "useCachedPromise", { enumerable: true, get: function () { return hooks_1.useCachedPromise; } });
Object.defineProperty(exports, "useFetch", { enumerable: true, get: function () { return hooks_1.useFetch; } });
const toast_1 = require("./api-stubs/toast");
// ── usePromise ─────────────────────────────────────────────────────────
/**
 * Similar to useCachedPromise but without caching semantics.
 * Delegates to useCachedPromise internally.
 */
function usePromise(fn, args, options) {
    return (0, hooks_1.useCachedPromise)(fn, args, options);
}
// ── useCachedState ─────────────────────────────────────────────────────
/**
 * Persisted state hook. Simplified: wraps useState (no cross-session
 * persistence in CmdPal compat).
 */
function useCachedState(_key, initialValue) {
    return (0, react_1.useState)(initialValue);
}
// ── useExec ────────────────────────────────────────────────────────────
/**
 * Execute a shell command. Stub — shell execution is not supported
 * in the CmdPal compat layer for security.
 */
function useExec(command, _args, _options) {
    return {
        data: undefined,
        isLoading: false,
        error: new Error(`[useExec] Shell execution not supported in CmdPal compat layer: ${command}`),
        revalidate: () => { },
    };
}
// ── useSQL ─────────────────────────────────────────────────────────────
/**
 * Execute SQL queries. Stub — not supported in CmdPal.
 */
function useSQL(_databasePath, _query) {
    return {
        data: undefined,
        isLoading: false,
        error: new Error('[useSQL] SQL queries not supported in CmdPal compat layer'),
        revalidate: () => { },
    };
}
// ── showFailureToast ───────────────────────────────────────────────────
/**
 * Convenience wrapper for error toasts.
 */
async function showFailureToast(error, options) {
    const message = error instanceof Error ? error.message : String(error);
    await (0, toast_1.showToast)({
        style: toast_1.Toast.Style.Failure,
        title: options?.title ?? 'Error',
        message: options?.message ?? message,
    });
}
// ── getFavicon ─────────────────────────────────────────────────────────
/**
 * Returns a favicon URL for a given domain.
 */
function getFavicon(url, options) {
    try {
        const domain = new URL(url).hostname;
        return `https://www.google.com/s2/favicons?domain=${domain}&sz=${options?.size ?? 32}`;
    }
    catch {
        return options?.fallback ?? '';
    }
}
// ── getProgressIcon ────────────────────────────────────────────────────
/**
 * Returns a progress indicator character.
 */
function getProgressIcon(progress, _color) {
    if (progress <= 0)
        return '○';
    if (progress >= 1)
        return '●';
    return '◐';
}
// ── useFrecencySorting ─────────────────────────────────────────────────
/**
 * Sort items by frequency + recency of access.
 * Stub: returns data as-is with no-op tracking functions.
 */
function useFrecencySorting(data, options) {
    void options;
    return {
        data: data ?? [],
        visitItem: () => { },
        resetRanking: () => { },
    };
}
// ── useForm ────────────────────────────────────────────────────────────
/**
 * Form state management hook.
 * Stub: provides basic form value tracking with no validation.
 */
function useForm(options) {
    const values = (options?.initialValues ?? {});
    const [formValues, setFormValues] = (0, react_1.useState)(values);
    const [errors, setErrors] = (0, react_1.useState)({});
    const itemProps = new Proxy({}, {
        get(_target, prop) {
            return {
                value: formValues[prop],
                onChange: (v) => {
                    setFormValues((prev) => ({ ...prev, [prop]: v }));
                },
                error: errors[prop],
            };
        },
    });
    return {
        handleSubmit: (vals) => options?.onSubmit?.(vals),
        itemProps,
        values: formValues,
        setValidationError: (id, error) => {
            setErrors((prev) => ({ ...prev, [id]: error }));
        },
        setValue: (id, value) => {
            setFormValues((prev) => ({ ...prev, [id]: value }));
        },
        reset: (initial) => {
            setFormValues((initial ?? options?.initialValues ?? {}));
            setErrors({});
        },
        focus: () => { },
    };
}
// ── useLocalStorage ────────────────────────────────────────────────────
/**
 * Persistent local storage hook.
 * Delegates to useCachedState (no true persistence across sessions).
 */
function useLocalStorage(key, initialValue) {
    const [value, setVal] = useCachedState(key, initialValue);
    return {
        value,
        setValue: setVal,
        removeValue: () => setVal(undefined),
        isLoading: false,
    };
}
// ── runAppleScript ─────────────────────────────────────────────────────
/**
 * Run an AppleScript. Stub: not supported on Windows.
 */
async function runAppleScript(script, _options) {
    void script;
    console.warn('[Utils] runAppleScript() is not supported on Windows');
    return '';
}
//# sourceMappingURL=utils-shim.js.map