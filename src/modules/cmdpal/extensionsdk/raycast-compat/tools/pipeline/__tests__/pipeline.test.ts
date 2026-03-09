// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Unit tests for the pipeline orchestrator.
 * Mocks all stages to test orchestration logic without network/file I/O.
 */

import type { PipelineResult } from '../src/types';

// Mock all stage modules
jest.mock('../src/stage-download');
jest.mock('../src/stage-validate');
jest.mock('../src/stage-dependencies');
jest.mock('../src/stage-build');
jest.mock('../src/stage-install');
jest.mock('../src/stage-cleanup');

import { downloadExtension } from '../src/stage-download';
import { validateExtension } from '../src/stage-validate';
import { installDependencies } from '../src/stage-dependencies';
import { buildExtension } from '../src/stage-build';
import { installExtension, getDefaultInstallDir } from '../src/stage-install';
import { cleanup } from '../src/stage-cleanup';
import { installRaycastExtension } from '../src/pipeline';

const mockDownload = downloadExtension as jest.MockedFunction<typeof downloadExtension>;
const mockValidate = validateExtension as jest.MockedFunction<typeof validateExtension>;
const mockDeps = installDependencies as jest.MockedFunction<typeof installDependencies>;
const mockBuild = buildExtension as jest.MockedFunction<typeof buildExtension>;
const mockInstall = installExtension as jest.MockedFunction<typeof installExtension>;
const mockGetDefaultDir = getDefaultInstallDir as jest.MockedFunction<typeof getDefaultInstallDir>;
const mockCleanup = cleanup as jest.MockedFunction<typeof cleanup>;

describe('pipeline orchestrator', () => {
  beforeEach(() => {
    jest.resetAllMocks();
    mockGetDefaultDir.mockReturnValue('/fake/JSExtensions');
  });

  function setupHappyPath(): void {
    mockDownload.mockResolvedValue({
      tempDir: '/tmp/raycast-test-123',
      files: ['package.json', 'src/index.tsx'],
    });
    mockValidate.mockReturnValue({
      valid: true,
      errors: [],
      warnings: [],
    });
    mockDeps.mockResolvedValue({
      success: true,
      stdout: 'added 5 packages',
      stderr: '',
    });
    mockBuild.mockResolvedValue({
      success: true,
      bundleResult: { success: true, commands: [{ name: 'index', result: { success: true, errors: [], warnings: [], outfile: 'dist/index.js' } }] },
      manifestPath: '/tmp/build/cmdpal.json',
      errors: [],
    });
    mockInstall.mockReturnValue({
      success: true,
      extensionPath: '/fake/JSExtensions/raycast-test',
      errors: [],
    });
    mockCleanup.mockImplementation(() => {});
  }

  it('runs all stages in order on happy path', async () => {
    setupHappyPath();

    const result = await installRaycastExtension({
      extensionName: 'test-ext',
      outputDir: '/fake/JSExtensions',
    });

    expect(result.success).toBe(true);
    expect(result.extensionPath).toBe('/fake/JSExtensions/raycast-test');
    expect(result.stages).toHaveLength(6);
    expect(result.stages.map((s) => s.name)).toEqual([
      'download',
      'validate',
      'dependencies',
      'build',
      'install',
      'cleanup',
    ]);
    expect(result.stages.every((s) => s.status === 'success')).toBe(true);
  });

  it('invokes onProgress callback for each stage', async () => {
    setupHappyPath();
    const progressCalls: Array<[string, string]> = [];

    await installRaycastExtension({
      extensionName: 'test-ext',
      outputDir: '/fake/JSExtensions',
      onProgress: (stage, detail) => progressCalls.push([stage, detail]),
    });

    const stageNames = progressCalls.map(([stage]) => stage);
    expect(stageNames).toContain('download');
    expect(stageNames).toContain('validate');
    expect(stageNames).toContain('dependencies');
    expect(stageNames).toContain('build');
    expect(stageNames).toContain('install');
    expect(stageNames).toContain('cleanup');
    expect(stageNames).toContain('complete');
  });

  it('stops and cleans up when download fails', async () => {
    mockDownload.mockRejectedValue(new Error('Network error'));

    const result = await installRaycastExtension({
      extensionName: 'test-ext',
      outputDir: '/fake/JSExtensions',
    });

    expect(result.success).toBe(false);
    expect(result.error).toContain('Network error');
    expect(result.stages[0].status).toBe('failed');
    // Remaining stages should be skipped
    const skipped = result.stages.filter((s) => s.status === 'skipped');
    expect(skipped.length).toBeGreaterThanOrEqual(4);
  });

  it('stops when validation fails (platform rejection)', async () => {
    mockDownload.mockResolvedValue({
      tempDir: '/tmp/raycast-test-123',
      files: ['package.json'],
    });
    mockValidate.mockReturnValue({
      valid: false,
      errors: ['Platform rejection: macOS only'],
      warnings: [],
    });

    const result = await installRaycastExtension({
      extensionName: 'macos-only-ext',
      outputDir: '/fake/JSExtensions',
    });

    expect(result.success).toBe(false);
    expect(result.error).toContain('Platform rejection');
    expect(result.stages[0].status).toBe('success');
    expect(result.stages[1].status).toBe('failed');
    expect(mockCleanup).toHaveBeenCalled();
  });

  it('stops when npm install fails', async () => {
    mockDownload.mockResolvedValue({
      tempDir: '/tmp/raycast-test-123',
      files: ['package.json'],
    });
    mockValidate.mockReturnValue({
      valid: true,
      errors: [],
      warnings: [],
    });
    mockDeps.mockResolvedValue({
      success: false,
      stdout: '',
      stderr: 'npm ERR! 404',
    });

    const result = await installRaycastExtension({
      extensionName: 'test-ext',
      outputDir: '/fake/JSExtensions',
    });

    expect(result.success).toBe(false);
    expect(result.error).toContain('npm install failed');
    expect(mockCleanup).toHaveBeenCalled();
  });

  it('stops when build fails', async () => {
    mockDownload.mockResolvedValue({
      tempDir: '/tmp/raycast-test-123',
      files: ['package.json'],
    });
    mockValidate.mockReturnValue({ valid: true, errors: [], warnings: [] });
    mockDeps.mockResolvedValue({ success: true, stdout: '', stderr: '' });
    mockBuild.mockResolvedValue({
      success: false,
      bundleResult: { success: false, commands: [] },
      manifestPath: '',
      errors: ['Build error: syntax error in source'],
    });

    const result = await installRaycastExtension({
      extensionName: 'test-ext',
      outputDir: '/fake/JSExtensions',
    });

    expect(result.success).toBe(false);
    expect(result.error).toContain('Build error');
  });

  it('stops when install stage fails', async () => {
    mockDownload.mockResolvedValue({
      tempDir: '/tmp/raycast-test-123',
      files: ['package.json'],
    });
    mockValidate.mockReturnValue({ valid: true, errors: [], warnings: [] });
    mockDeps.mockResolvedValue({ success: true, stdout: '', stderr: '' });
    mockBuild.mockResolvedValue({
      success: true,
      bundleResult: { success: true, commands: [] },
      manifestPath: '/tmp/build/cmdpal.json',
      errors: [],
    });
    mockInstall.mockReturnValue({
      success: false,
      extensionPath: '',
      errors: ['Permission denied'],
    });

    const result = await installRaycastExtension({
      extensionName: 'test-ext',
      outputDir: '/fake/JSExtensions',
    });

    expect(result.success).toBe(false);
    expect(result.error).toContain('Permission denied');
  });

  it('records stage durations', async () => {
    setupHappyPath();

    const result = await installRaycastExtension({
      extensionName: 'test-ext',
      outputDir: '/fake/JSExtensions',
    });

    for (const stage of result.stages) {
      expect(typeof stage.duration).toBe('number');
      expect(stage.duration).toBeGreaterThanOrEqual(0);
    }
  });

  it('passes github token to download stage', async () => {
    setupHappyPath();

    await installRaycastExtension({
      extensionName: 'test-ext',
      outputDir: '/fake/JSExtensions',
      githubToken: 'ghp_test_token_123',
    });

    expect(mockDownload).toHaveBeenCalledWith('test-ext', 'ghp_test_token_123');
  });
});
