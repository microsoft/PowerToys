#!/usr/bin/env node
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Packs the SDK with `npm pack` and inspects the produced tarball to guarantee
// the published `cmdpal-bootstrap` bin is present and starts with a Node
// shebang. Without the shebang the bin fails to execute through `npm` on POSIX
// shells, so this guard runs in CI to catch a regression before publish.

import { execFileSync } from 'node:child_process';
import { rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const SHEBANG = '#!/usr/bin/env node';
const BIN_PATH = 'bin/cmdpal-bootstrap.mjs';
const BOOTSTRAP_PATH = 'dist/runtime/bootstrap.js';

const packageRoot = dirname(dirname(fileURLToPath(import.meta.url)));

function fail(message) {
  process.stderr.write(`verify-pack: ${message}\n`);
  process.exitCode = 1;
}

function run(command, args, options = {}) {
  return execFileSync(command, args, { cwd: packageRoot, encoding: 'utf8', ...options });
}

// Resolve npm without depending on shell resolution of `npm.cmd`, which Node
// refuses to launch through execFile and which mangles paths when forced
// through a Windows shell. When this script runs under an npm lifecycle script
// `npm_execpath` points at npm's own CLI entry, so drive it with the current
// Node binary. Fall back to the platform npm launcher otherwise.
function runNpm(args) {
  const npmExecpath = process.env.npm_execpath;
  if (npmExecpath) {
    return run(process.execPath, [npmExecpath, ...args]);
  }
  const launcher = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  return run(launcher, args, { shell: process.platform === 'win32' });
}

let tarball;
try {
  const output = runNpm(['pack', '--json']);
  const parsed = JSON.parse(output);
  const info = Array.isArray(parsed) ? parsed[0] : parsed;
  if (!info || typeof info.filename !== 'string') {
    fail('npm pack did not report a tarball filename.');
  } else {
    tarball = join(packageRoot, info.filename);

    const files = Array.isArray(info.files) ? info.files.map((entry) => entry.path) : [];
    for (const required of [BIN_PATH, BOOTSTRAP_PATH]) {
      if (!files.includes(required)) {
        fail(`tarball is missing required file "${required}".`);
      }
    }

    // Read the bin straight out of the tarball so the assertion reflects the
    // bytes that ship, not just the on-disk source. npm stores files under a
    // leading "package/" directory.
    const binContents = run('tar', ['-xzOf', tarball, `package/${BIN_PATH}`]);
    if (!binContents.startsWith(SHEBANG)) {
      fail(`packed "${BIN_PATH}" does not start with "${SHEBANG}".`);
    }
  }
} catch (error) {
  const message = error instanceof Error ? error.message : String(error);
  fail(`failed to pack or inspect the tarball: ${message}`);
} finally {
  if (tarball) {
    rmSync(tarball, { force: true });
  }
}

if (process.exitCode === 1) {
  process.stderr.write('verify-pack: FAILED\n');
} else {
  process.stdout.write(`verify-pack: OK (${BIN_PATH} present with shebang)\n`);
}
