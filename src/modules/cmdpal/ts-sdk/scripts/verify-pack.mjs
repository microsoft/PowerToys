// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Packs the SDK into a tarball, installs that tarball into a throwaway consumer
// project, and type-checks a file that imports the public API the documentation
// uses. This proves an extension can build against the PACKED SDK (a
// registry-style install) rather than against the repo-local "file:../../ts-sdk"
// path, without needing the PowerToys repository present.
//
// Run with: npm run verify:pack

import { execFileSync } from 'node:child_process';
import {
  mkdtempSync,
  rmSync,
  writeFileSync,
  readdirSync,
  mkdirSync,
  existsSync,
  renameSync,
  cpSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const sdkRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

// On Windows, Node 20+ refuses to execute an "npm.cmd" shim through execFile
// without a shell, and running it through a shell invites quoting bugs. When
// this script runs under "npm run", npm sets npm_execpath to its own JS entry
// point, so npm and tsc are both invoked through the current Node executable
// instead. That works the same on every platform and needs no shell.
const npmExecpath = process.env.npm_execpath;
const useNodeForNpm = typeof npmExecpath === 'string' && npmExecpath.endsWith('.js');
const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';

function npmArgs(args) {
  return useNodeForNpm ? [process.execPath, [npmExecpath, ...args]] : [npmCommand, args];
}

function run(command, args, cwd) {
  execFileSync(command, args, { cwd, stdio: 'inherit' });
}

function runNpm(args, cwd) {
  const [command, resolvedArgs] = npmArgs(args);
  execFileSync(command, resolvedArgs, { cwd, stdio: 'inherit' });
}

function runNpmCapture(args, cwd) {
  const [command, resolvedArgs] = npmArgs(args);
  return execFileSync(command, resolvedArgs, { cwd, encoding: 'utf8' });
}

// Produces a registry-style tarball with a top-level "package/" directory that
// mirrors what "npm pack" would publish: the package.json "files" allowlist plus
// the files npm always includes (package.json and, when present, README/LICENSE).
// This is a deterministic fallback for environments where "npm pack" itself is
// broken (for example the tarball-corruption bug seen with some Node and npm
// pairings on Windows), so the packed-SDK smoke test still runs everywhere.
function packManually(workDir) {
  const stageRoot = join(workDir, 'stage');
  const packageDir = join(stageRoot, 'package');
  mkdirSync(packageDir, { recursive: true });

  cpSync(join(sdkRoot, 'package.json'), join(packageDir, 'package.json'));
  cpSync(join(sdkRoot, 'dist'), join(packageDir, 'dist'), { recursive: true });
  for (const optional of ['README.md', 'LICENSE', 'LICENSE.md', 'LICENSE.txt']) {
    const source = join(sdkRoot, optional);
    if (existsSync(source)) {
      cpSync(source, join(packageDir, optional));
    }
  }

  const tarballPath = join(workDir, 'microsoft-cmdpal-sdk-manual.tgz');
  // "tar" resolves to bsdtar on Windows 10 and later and to GNU tar elsewhere;
  // both accept these flags and both are real executables (not shims).
  execFileSync('tar', ['-czf', tarballPath, '-C', stageRoot, 'package'], { stdio: 'inherit' });
  return tarballPath;
}

function packWithNpm(workDir) {
  const packOutput = runNpmCapture(['pack'], sdkRoot);
  const tarball = packOutput
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.endsWith('.tgz'))
    .pop();
  if (!tarball) {
    throw new Error('npm pack did not report a tarball file name.');
  }
  const tarballPath = join(workDir, tarball);
  renameSync(join(sdkRoot, tarball), tarballPath);
  return tarballPath;
}

const workDir = mkdtempSync(join(tmpdir(), 'cmdpal-sdk-pack-'));
let failed = false;
try {
  console.log('[verify:pack] Building the SDK...');
  runNpm(['run', 'build'], sdkRoot);

  console.log('[verify:pack] Packing the SDK...');
  let tarballPath;
  try {
    tarballPath = packWithNpm(workDir);
  } catch (packError) {
    const reason = packError instanceof Error ? packError.message : String(packError);
    console.warn(
      `[verify:pack] npm pack was unavailable (${reason}); building the tarball directly.`,
    );
    tarballPath = packManually(workDir);
  }
  console.log(`[verify:pack] Packed ${tarballPath}`);

  const consumerDir = join(workDir, 'consumer');
  mkdirSync(join(consumerDir, 'src'), { recursive: true });

  writeFileSync(
    join(consumerDir, 'package.json'),
    JSON.stringify(
      {
        name: 'cmdpal-sdk-pack-consumer',
        version: '0.0.0',
        private: true,
        type: 'module',
      },
      null,
      2,
    ),
  );

  writeFileSync(
    join(consumerDir, 'tsconfig.json'),
    JSON.stringify(
      {
        compilerOptions: {
          target: 'ES2022',
          module: 'NodeNext',
          moduleResolution: 'NodeNext',
          strict: true,
          noEmit: true,
          skipLibCheck: true,
          types: [],
        },
        include: ['src/**/*.ts'],
      },
      null,
      2,
    ),
  );

  // Import the public API the documentation exercises and use each symbol in a
  // way that forces the type-checker to resolve it from the installed package.
  writeFileSync(
    join(consumerDir, 'src', 'check.ts'),
    `import {
  CommandProviderBase,
  CommandItemBase,
  ListPageBase,
  ContentPageBase,
  InvokableCommandBase,
  Separator,
  ExtensionHost,
  iconFromGlyph,
  run,
} from '@microsoft/cmdpal-sdk';
import type {
  ICommandItem,
  IListItem,
  Content,
  FormContent,
  CommandResult,
} from '@microsoft/cmdpal-sdk';

const separator = new Separator('Group');
void separator;

class DemoCommand extends InvokableCommandBase {
  readonly id = 'demo';
  readonly name = 'Demo';
  override invoke(): CommandResult {
    const statusId = ExtensionHost.showStatus('Working...', 'info', { isIndeterminate: true });
    ExtensionHost.updateStatus(statusId, 'Done', 'success');
    ExtensionHost.hideStatus(statusId);
    return { kind: 'keepOpen' };
  }
}

class DemoListPage extends ListPageBase {
  readonly id = 'demo-page';
  readonly name = 'Demo';
  readonly title = 'Demo';
  override getItems(): IListItem[] {
    const item = new CommandItemBase({
      command: new DemoCommand(),
      title: 'Demo',
      icon: iconFromGlyph('\\uE91B'),
    });
    return [item as unknown as IListItem];
  }
}

class DemoContentPage extends ContentPageBase {
  readonly id = 'demo-content';
  readonly name = 'Content';
  readonly title = 'Content';
  override getContent(): Content[] {
    const form: FormContent = {
      type: 'form',
      formId: 'demo-form',
      templateJson: '{}',
      dataJson: '{}',
      submitForm(): CommandResult {
        return { kind: 'goHome' };
      },
    };
    return [form];
  }
}

class DemoProvider extends CommandProviderBase {
  readonly id = 'demo-provider';
  readonly displayName = 'Demo';
  private readonly listPage = new DemoListPage();
  private readonly contentPage = new DemoContentPage();
  override topLevelCommands(): ICommandItem[] {
    return [
      new CommandItemBase({ command: this.listPage, title: 'Demo list' }),
      new CommandItemBase({ command: this.contentPage, title: 'Demo content' }),
    ];
  }
}

void (() => run(() => new DemoProvider()));
`,
  );

  console.log('[verify:pack] Installing the packed tarball into a throwaway consumer...');
  runNpm(['install', '--no-save', '--no-package-lock', tarballPath], consumerDir);

  console.log('[verify:pack] Type-checking the consumer against the packed SDK...');
  // Invoke the TypeScript compiler through Node using its JS entry point so no
  // platform-specific "tsc.cmd" shim is spawned.
  const tscBin = join(sdkRoot, 'node_modules', 'typescript', 'bin', 'tsc');
  if (!existsSync(tscBin)) {
    throw new Error(
      `TypeScript compiler was not found at '${tscBin}'. Run npm ci in ts-sdk first.`,
    );
  }

  run(process.execPath, [tscBin, '--project', join(consumerDir, 'tsconfig.json')], consumerDir);

  // Confirm the tarball actually shipped the built output and types.
  const installed = readdirSync(
    join(consumerDir, 'node_modules', '@microsoft', 'cmdpal-sdk', 'dist'),
  );
  if (!installed.includes('index.js') || !installed.includes('index.d.ts')) {
    throw new Error('Packed SDK is missing dist/index.js or dist/index.d.ts.');
  }

  console.log('[verify:pack] SUCCESS: the packed SDK installs and type-checks in a clean project.');
} catch (error) {
  failed = true;
  console.error('[verify:pack] FAILED:', error instanceof Error ? error.message : error);
} finally {
  rmSync(workDir, { recursive: true, force: true });
}

process.exit(failed ? 1 : 0);
