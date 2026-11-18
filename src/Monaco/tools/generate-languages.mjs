// Regenerates monaco_languages.json from src/languages.js, which holds every language registration in the
// bundle: monaco-editor's built-in languages plus the PowerToys layer. The runner, installer, and preview
// handlers read that file to decide which extensions Monaco handles.
//
//   node tools/generate-languages.mjs          rewrite monaco_languages.json
//   node tools/generate-languages.mjs --check  exit 1 if monaco_languages.json is out of date
//
// Registering languages needs Monaco's language registry but not the editor or a DOM. esbuild bundles
// src/languages.js for Node with monaco-editor's editor API replaced by a stand-in whose register() and
// getLanguages() use monaco-editor's own ModesRegistry, exactly as the standalone editor does at runtime.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const outputFile = path.join(root, 'monaco_languages.json');
const check = process.argv.includes('--check');

let build;
try {
    ({ build } = await import('esbuild'));
} catch {
    console.error('esbuild is not installed. Run "npm ci" in src/Monaco first.');
    process.exit(2);
}

const editorApiStandIn = `
import { ModesRegistry } from 'monaco-editor/editor/common/languages/modesRegistry';

// Everything other than register/getLanguages (tokens providers, language configuration, enums used by
// grammar configurations) is irrelevant to the language list.
const ignored = new Proxy(function () {}, {
    get: (_target, prop) => (prop === Symbol.toPrimitive ? () => 0 : ignored),
    apply: () => ignored,
    construct: () => ignored,
});

export const languages = new Proxy({
    register: (language) => ModesRegistry.registerLanguage(language),
    getLanguages: () => ModesRegistry.getLanguages(),
}, { get: (target, prop) => (prop in target ? target[prop] : ignored) });

export {
    ignored as CancellationTokenSource, ignored as Emitter, ignored as KeyCode, ignored as KeyMod,
    ignored as MarkerSeverity, ignored as MarkerTag, ignored as Position, ignored as Range, ignored as Selection,
    ignored as SelectionDirection, ignored as Token, ignored as Uri, ignored as editor,
};
`;

const entry = `
import { languages } from 'monaco-editor/editor';
import { registerPowerToysLanguages } from './src/languages.js';
registerPowerToysLanguages();
export const list = languages.getLanguages();
`;

const result = await build({
    absWorkingDir: root,
    stdin: { contents: entry, resolveDir: root, sourcefile: 'generate-languages-entry.js', loader: 'js' },
    bundle: true,
    platform: 'node',
    format: 'esm',
    write: false,
    logLevel: 'error',
    plugins: [{
        name: 'monaco-editor-api-stand-in',
        setup(b) {
            b.onResolve({ filter: /^monaco-editor\/editor$|\/editor\/editor\.api\.js$/ }, () => ({ path: 'editor-api', namespace: 'pt-stand-in' }));
            b.onLoad({ filter: /.*/, namespace: 'pt-stand-in' }, () => ({ contents: editorApiStandIn, resolveDir: root, loader: 'js' }));
        },
    }],
});

const bundle = Buffer.from(result.outputFiles[0].contents).toString('base64');
const { list } = await import(`data:text/javascript;base64,${bundle}`);
const generated = `${JSON.stringify({ list })}\r\n`;
const current = fs.existsSync(outputFile) ? fs.readFileSync(outputFile, 'utf8').replace(/\r?\n/g, '\r\n') : '';

if (check) {
    if (generated !== current) {
        console.error('src/Monaco/monaco_languages.json is out of date. Run "npm run gen:languages" in src/Monaco and commit the result.');
        process.exit(1);
    }

    console.log(`monaco_languages.json is up to date (${list.length} languages).`);
} else {
    fs.writeFileSync(outputFile, generated);
    console.log(`Wrote ${path.relative(process.cwd(), outputFile)} (${list.length} languages).`);
}
