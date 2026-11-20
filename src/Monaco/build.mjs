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

// The checks below fail the build when a Monaco update needs a manual step.
const updateDoc = 'doc/devdocs/common/monaco-editor.md#update-process';

// Third-party libraries bundled inside monaco-editor. NOTICE.md lists them under each Monaco Editor entry.
const noticedMonacoDependencies = ['dompurify', 'marked'];

// The installer lists the bundle files by name (installer/PowerToysSetupVNext/generateAllFileComponents.ps1).
const expectedOutputs = ['codicon.ttf', 'editor.worker.js', 'monaco.css', 'monaco.js'];

function updateError(message) {
    return new Error(`${message} See ${updateDoc}.`);
}

function listFiles(relPath) {
    const full = path.join(root, relPath);
    if (!fs.existsSync(full)) {
        return [];
    }

    if (fs.statSync(full).isFile()) {
        return [relPath];
    }

    // Walk directories by hand: readdirSync's recursive option needs Node 20.1 and Dirent.parentPath needs 20.12,
    // but package.json accepts any Node 20.
    return fs.readdirSync(full, { withFileTypes: true })
        .flatMap((entry) => listFiles(path.join(relPath, entry.name)));
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

// Installing through a feed can write its URLs and SHA-1 hashes into the lockfile.
function checkLockfile() {
    const lock = JSON.parse(readText(path.join(root, 'package-lock.json')));
    for (const [name, entry] of Object.entries(lock.packages ?? {})) {
        if (!name || entry.link) {
            continue;
        }

        if (!entry.resolved?.startsWith('https://registry.npmjs.org/') || !entry.integrity?.startsWith('sha512-')) {
            throw updateError(`package-lock.json entry "${name}" must resolve from https://registry.npmjs.org/ with a sha512 integrity hash.`);
        }
    }
}

function checkBundledLibraries() {
    const monacoPackage = JSON.parse(readText(path.join(root, 'node_modules', 'monaco-editor', 'package.json')));
    const bundled = Object.keys(monacoPackage.dependencies ?? {}).sort();
    if (bundled.join() !== noticedMonacoDependencies.join()) {
        throw updateError(`monaco-editor now bundles ${bundled.join(', ') || 'no libraries'}. Update NOTICE.md and noticedMonacoDependencies in build.mjs.`);
    }
}

function checkOutputs() {
    const outputs = fs.readdirSync(distDir).sort();
    if (outputs.join() !== expectedOutputs.join()) {
        throw updateError(`The bundle now has ${outputs.join(', ')}. Update the installer file list and expectedOutputs in build.mjs.`);
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
// Remove both once monaco-editor bundles a fixed DOMPurify (0.57.1 bundles 3.4.17); the check below enforces that.
function useDomPurifyPackage() {
    const vendoredPath = path.join(root, 'node_modules', 'monaco-editor', 'esm', 'vs', 'base', 'browser', 'dompurify', 'dompurify.js');
    const vendoredHeader = fs.readFileSync(vendoredPath, 'utf8').slice(0, 200);
    const vendored = /DOMPurify (\d+\.\d+\.\d+)/.exec(vendoredHeader)?.[1];
    const pinned = JSON.parse(fs.readFileSync(path.join(root, 'node_modules', 'dompurify', 'package.json'), 'utf8')).version;
    if (!vendored || compareVersions(vendored, pinned) >= 0) {
        throw updateError(`monaco-editor now bundles DOMPurify ${vendored ?? '(unknown version)'}, so the pinned ${pinned} is no longer needed. Remove the dompurify dependency and override from package.json and useDomPurifyPackage from build.mjs.`);
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
    checkBundledLibraries();
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

    checkOutputs();
}

await withLock(async () => {
    checkLockfile();
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
