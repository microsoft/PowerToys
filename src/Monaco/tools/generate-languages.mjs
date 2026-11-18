// Regenerates monaco_languages.json from the built bundle (dist/monaco.js), so the list matches exactly
// what the previewers register: monaco-editor's languages plus the PowerToys layer in src/languages.js.
// The runner, installer, and preview handlers read that file to decide which extensions Monaco handles.
//
//   node tools/generate-languages.mjs          rewrite monaco_languages.json
//   node tools/generate-languages.mjs --check  exit 1 if monaco_languages.json is out of date
//
// Monaco needs a DOM, so this loads the bundle in headless Microsoft Edge (--dump-dom). Set
// PT_EDGE_PATH to use a specific msedge.exe.
import { execFile, execFileSync } from 'node:child_process';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const distDir = path.join(root, 'dist');
const outputFile = path.join(root, 'monaco_languages.json');
const check = process.argv.includes('--check');

function findEdge() {
    const candidates = [
        process.env.PT_EDGE_PATH,
        path.join(process.env['ProgramFiles(x86)'] ?? '', 'Microsoft', 'Edge', 'Application', 'msedge.exe'),
        path.join(process.env.ProgramFiles ?? '', 'Microsoft', 'Edge', 'Application', 'msedge.exe'),
        path.join(process.env.LOCALAPPDATA ?? '', 'Microsoft', 'Edge', 'Application', 'msedge.exe'),
    ];
    return candidates.find((candidate) => candidate && fs.existsSync(candidate));
}

const page = `<!doctype html>
<meta charset="utf-8">
<body>
<script type="module">
    import { monaco } from './monaco.js';
    const list = monaco.languages.getLanguages();
    const json = JSON.stringify({ list });
    // Base64 keeps the payload intact through --dump-dom's HTML serialization.
    const bytes = new TextEncoder().encode(json);
    let binary = '';
    for (const b of bytes) binary += String.fromCharCode(b);
    const out = document.createElement('pre');
    out.id = 'pt-languages';
    out.textContent = btoa(binary);
    document.body.append(out);
</script>
</body>`;

function serve() {
    const server = http.createServer((req, res) => {
        const name = decodeURIComponent(new URL(req.url, 'http://localhost').pathname).replace(/^\/+/, '');
        if (name === 'languages.html') {
            res.writeHead(200, { 'Content-Type': 'text/html' }).end(page);
            return;
        }

        const file = path.join(distDir, path.basename(name));
        if (!fs.existsSync(file)) {
            res.writeHead(404).end();
            return;
        }

        const type = { '.js': 'text/javascript', '.css': 'text/css', '.ttf': 'font/ttf' }[path.extname(file)] ?? 'application/octet-stream';
        res.writeHead(200, { 'Content-Type': type });
        fs.createReadStream(file).pipe(res);
    });
    return new Promise((resolve) => server.listen(0, '127.0.0.1', () => resolve(server)));
}

function dumpDom(edge, url) {
    const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'pt-monaco-edge-'));
    const args = [
        '--headless=new',
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        '--disable-extensions',
        `--user-data-dir=${profile}`,
        '--virtual-time-budget=15000',
        '--dump-dom',
        url,
    ];
    return new Promise((resolve, reject) => {
        execFile(edge, args, { maxBuffer: 32 * 1024 * 1024, timeout: 120000, windowsHide: true }, (error, stdout) => {
            fs.rmSync(profile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
            if (error) {
                reject(error);
            } else {
                resolve(stdout);
            }
        });
    });
}

execFileSync(process.execPath, [path.join(root, 'build.mjs')], { cwd: root, stdio: 'inherit' });

const edge = findEdge();
if (!edge) {
    console.error('Microsoft Edge (msedge.exe) was not found. Install Edge or set PT_EDGE_PATH.');
    process.exit(2);
}

const server = await serve();
let dom;
try {
    dom = await dumpDom(edge, `http://127.0.0.1:${server.address().port}/languages.html`);
} finally {
    server.close();
}

const match = /<pre id="pt-languages">([A-Za-z0-9+/=]+)<\/pre>/.exec(dom);
if (!match) {
    console.error('Edge did not produce the language list. Is dist/monaco.js loadable?');
    process.exit(1);
}

const generated = `${Buffer.from(match[1], 'base64').toString('utf8')}\r\n`;
const current = fs.existsSync(outputFile) ? fs.readFileSync(outputFile, 'utf8').replace(/\r?\n/g, '\r\n') : '';

if (check) {
    if (generated !== current) {
        console.error('src/Monaco/monaco_languages.json is out of date. Run "npm run gen:languages" in src/Monaco and commit the result.');
        process.exit(1);
    }

    console.log('monaco_languages.json is up to date.');
} else {
    fs.writeFileSync(outputFile, generated);
    console.log(`Wrote ${path.relative(process.cwd(), outputFile)} (${JSON.parse(generated).list.length} languages).`);
}
