// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Unit tests for the validate stage.
 */

import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { validateExtension } from '../src/stage-validate';

describe('stage-validate', () => {
  let tempDir: string;

  beforeEach(() => {
    tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'pipeline-test-validate-'));
  });

  afterEach(() => {
    fs.rmSync(tempDir, { recursive: true, force: true });
  });

  function writePackageJson(content: object): void {
    fs.writeFileSync(
      path.join(tempDir, 'package.json'),
      JSON.stringify(content, null, 2),
    );
  }

  it('passes for a valid Windows-compatible extension', () => {
    writePackageJson({
      name: 'test-ext',
      title: 'Test Extension',
      description: 'A test',
      author: 'tester',
      icon: 'icon.png',
      version: '1.0.0',
      commands: [{ name: 'index', title: 'Main', mode: 'view' }],
      platforms: ['macOS', 'Windows'],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });

  it('fails when package.json is missing', () => {
    const result = validateExtension(tempDir);
    expect(result.valid).toBe(false);
    expect(result.errors).toContain('package.json not found in extension directory');
  });

  it('fails when package.json is invalid JSON', () => {
    fs.writeFileSync(path.join(tempDir, 'package.json'), '{ not json !!!');
    const result = validateExtension(tempDir);
    expect(result.valid).toBe(false);
    expect(result.errors[0]).toContain('Failed to parse package.json');
  });

  it('fails when name is missing', () => {
    writePackageJson({
      title: 'Test',
      commands: [{ name: 'index' }],
      platforms: ['Windows'],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining("'name'"));
  });

  it('fails when title is missing', () => {
    writePackageJson({
      name: 'test-ext',
      commands: [{ name: 'index' }],
      platforms: ['Windows'],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining("'title'"));
  });

  it('fails when commands array is empty', () => {
    writePackageJson({
      name: 'test-ext',
      title: 'Test',
      commands: [],
      platforms: ['Windows'],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining("'commands'"));
  });

  it('fails when commands is missing', () => {
    writePackageJson({
      name: 'test-ext',
      title: 'Test',
      platforms: ['Windows'],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining("'commands'"));
  });

  it('rejects extensions without Windows platform', () => {
    writePackageJson({
      name: 'test-ext',
      title: 'Test',
      commands: [{ name: 'index' }],
      platforms: ['macOS'],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining('Platform rejection'));
  });

  it('rejects when platforms field is absent (defaults to macOS)', () => {
    writePackageJson({
      name: 'test-ext',
      title: 'Test',
      commands: [{ name: 'index' }],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining('Platform rejection'));
  });

  it('accepts case-insensitive "windows" in platforms', () => {
    writePackageJson({
      name: 'test-ext',
      title: 'Test',
      commands: [{ name: 'index' }],
      platforms: ['windows'],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(true);
  });

  it('warns about missing optional fields', () => {
    writePackageJson({
      name: 'test-ext',
      title: 'Test',
      commands: [{ name: 'index' }],
      platforms: ['Windows'],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(true);
    expect(result.warnings.length).toBeGreaterThan(0);
    expect(result.warnings).toContainEqual(expect.stringContaining('description'));
    expect(result.warnings).toContainEqual(expect.stringContaining('author'));
    expect(result.warnings).toContainEqual(expect.stringContaining('icon'));
    expect(result.warnings).toContainEqual(expect.stringContaining('version'));
  });

  it('detects commands with missing name field', () => {
    writePackageJson({
      name: 'test-ext',
      title: 'Test',
      commands: [{ name: 'good' }, { name: '' }],
      platforms: ['Windows'],
    });

    const result = validateExtension(tempDir);
    expect(result.valid).toBe(false);
    expect(result.errors).toContainEqual(expect.stringContaining('commands[1]'));
  });
});
