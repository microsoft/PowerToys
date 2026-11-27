// PowerToys Monaco bundle entry. Built by build.mjs from the pinned monaco-editor package;
// everything PowerToys needs is layered on through public monaco-editor entry points.
import * as monaco from 'monaco-editor/editor';
import './features.js';
import { customTokenThemeRules } from './customTokenThemeRules.js';
import { registerPowerToysLanguages } from './languages.js';

// The editor worker computes links and word-based completions. Host pages are loaded from file:// or
// NavigateToString while the bundle is served from the WebView2 virtual host, so the worker script is
// cross-origin. A same-origin blob worker can still importScripts() it.
self.MonacoEnvironment = {
    getWorker(_workerId, label) {
        const script = new URL('editor.worker.js', import.meta.url).href;
        const bootstrap = URL.createObjectURL(new Blob([`importScripts(${JSON.stringify(script)});`], { type: 'text/javascript' }));
        return new Worker(bootstrap, { name: label });
    },
};

registerPowerToysLanguages();

// Defines the 'theme' editor theme on top of 'vs' or 'vs-dark' with PowerToys token colors.
export function definePowerToysTheme(baseTheme) {
    monaco.editor.defineTheme('theme', {
        base: baseTheme,
        inherit: true,
        rules: customTokenThemeRules,
        colors: {},
    });
}

// Hosts and the C# side (ExecuteScriptAsync) address the editor through the `monaco` global.
globalThis.monaco = monaco;

export { monaco };
