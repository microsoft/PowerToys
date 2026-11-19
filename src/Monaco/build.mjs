// Builds the PowerToys Monaco bundle into dist/ from the pinned monaco-editor npm package.
// Invoked from src/Monaco.props during the normal build. Safe to run concurrently (file lock)
// and cheap when nothing changed (content-hash stamp).
import { execSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const distDir = path.join(root, 'dist');
const stampFile = path.join(distDir, '.stamp');
const lockFile = path.join(root, '.build.lock');
const depsMarker = path.join(root, 'node_modules', '.pt-deps-hash');

const depsInputs = ['package.json', 'package-lock.json', '.npmrc'];
const bundleInputs = [...depsInputs, 'build.mjs', 'src'];

function listFiles(relPath) {
    const full = path.join(root, relPath);
    if (!fs.existsSync(full)) {
        return [];
    }

    if (fs.statSync(full).isFile()) {
        return [relPath];
    }

    return fs.readdirSync(full, { recursive: true, withFileTypes: true })
        .filter((entry) => entry.isFile())
        .map((entry) => path.relative(root, path.join(entry.parentPath, entry.name)));
}

function hashInputs(inputs) {
    const hash = createHash('sha256');
    hash.update(process.version.split('.')[0]);
    for (const file of inputs.flatMap(listFiles).sort()) {
        hash.update(file.replaceAll('\\', '/'));
        hash.update(fs.readFileSync(path.join(root, file)));
    }

    return hash.digest('hex');
}

function readText(file) {
    return fs.existsSync(file) ? fs.readFileSync(file, 'utf8').trim() : '';
}

function isProcessAlive(pid) {
    try {
        process.kill(pid, 0);
        return true;
    } catch {
        return false;
    }
}

async function withLock(action) {
    const deadline = Date.now() + 15 * 60 * 1000;
    for (;;) {
        try {
            const fd = fs.openSync(lockFile, 'wx');
            fs.writeSync(fd, String(process.pid));
            fs.closeSync(fd);
            break;
        } catch (error) {
            if (error.code !== 'EEXIST') {
                throw error;
            }

            const owner = Number.parseInt(readText(lockFile), 10);
            if (Number.isInteger(owner) && !isProcessAlive(owner)) {
                fs.rmSync(lockFile, { force: true });
                continue;
            }

            if (Date.now() > deadline) {
                throw new Error(`Timed out waiting for ${lockFile} (held by process ${owner}).`);
            }

            await new Promise((resolve) => setTimeout(resolve, 250));
        }
    }

    try {
        return await action();
    } finally {
        fs.rmSync(lockFile, { force: true });
    }
}

function restoreDependencies() {
    const depsHash = hashInputs(depsInputs);
    if (readText(depsMarker) === depsHash) {
        return;
    }

    console.log('Monaco: npm ci');
    execSync('npm ci', { cwd: root, stdio: 'inherit' });
    fs.writeFileSync(depsMarker, depsHash);
}

function parseVersion(text) {
    return text.split('.').map((part) => Number.parseInt(part, 10));
}

function compareVersions(a, b) {
    const [x, y] = [parseVersion(a), parseVersion(b)];
    for (let i = 0; i < Math.max(x.length, y.length); i++) {
        const diff = (x[i] ?? 0) - (y[i] ?? 0);
        if (diff !== 0) {
            return diff;
        }
    }

    return 0;
}

// monaco-editor 0.57.0 ships an unmodified copy of DOMPurify 3.4.15, which has security advisories fixed in 3.4.17.
// package.json pins the patched dompurify package, and this redirects Monaco's import of its own copy to it.
// Remove both once monaco-editor bundles a fixed DOMPurify (0.57.1 bundles 3.4.17).
function useDomPurifyPackage() {
    const vendoredPath = path.join(root, 'node_modules', 'monaco-editor', 'esm', 'vs', 'base', 'browser', 'dompurify', 'dompurify.js');
    const vendoredHeader = fs.readFileSync(vendoredPath, 'utf8').slice(0, 200);
    const vendored = /DOMPurify (\d+\.\d+\.\d+)/.exec(vendoredHeader)?.[1];
    const pinned = JSON.parse(fs.readFileSync(path.join(root, 'node_modules', 'dompurify', 'package.json'), 'utf8')).version;
    if (!vendored || compareVersions(vendored, pinned) > 0) {
        throw new Error(`monaco-editor now bundles DOMPurify ${vendored ?? '(unknown version)'}, newer than the pinned ${pinned}. Remove the dompurify dependency and override from package.json and useDomPurifyPackage from build.mjs.`);
    }

    return {
        name: 'use-dompurify-package',
        setup(b) {
            b.onResolve({ filter: /[\\/]dompurify[\\/]dompurify\.js$/ }, (args) =>
                b.resolve('dompurify', { kind: args.kind, resolveDir: root }));
        },
    };
}

async function bundle() {
    const { build } = await import('esbuild');
    fs.rmSync(distDir, { recursive: true, force: true });

    const common = {
        absWorkingDir: root,
        bundle: true,
        minify: true,
        target: 'es2022',
        outdir: distDir,
        entryNames: '[name]',
        assetNames: '[name]',
        logLevel: 'warning',
        plugins: [useDomPurifyPackage()],
    };

    // The editor bundle is an ES module so host pages can import it from the WebView2 virtual host.
    await build({
        ...common,
        entryPoints: { monaco: 'src/main.js' },
        format: 'esm',
        loader: { '.ttf': 'file' },
    });

    // The worker is a classic script created by src/main.js (MonacoEnvironment.getWorker).
    await build({
        ...common,
        entryPoints: { 'editor.worker': 'monaco-editor/editor/editor.worker' },
        format: 'iife',
    });
}

await withLock(async () => {
    restoreDependencies();

    const bundleHash = hashInputs(bundleInputs);
    if (readText(stampFile) === bundleHash) {
        // Refresh the timestamp so MSBuild's incremental check sees the bundle as current.
        const now = new Date();
        fs.utimesSync(stampFile, now, now);
        return;
    }

    console.log('Monaco: bundling');
    await bundle();
    fs.writeFileSync(stampFile, bundleHash);
});
