import { type BundleExtensionResult } from '@cmdpal/raycast-bundler';
export interface BuildResult {
    /** Whether the build succeeded. */
    success: boolean;
    /** Bundler result details. */
    bundleResult: BundleExtensionResult;
    /** Path to the generated cmdpal.json. */
    manifestPath: string;
    /** Errors encountered during building. */
    errors: string[];
}
/**
 * Build a Raycast extension: bundle with esbuild + generate manifests.
 *
 * 1. Runs the bundler (esbuild with @raycast/api aliasing) on all commands
 * 2. Translates package.json → cmdpal.json
 * 3. Generates raycast-compat.json for the runtime compat layer
 * 4. Copies assets
 */
export declare function buildExtension(extensionDir: string, outputDir: string): Promise<BuildResult>;
//# sourceMappingURL=stage-build.d.ts.map