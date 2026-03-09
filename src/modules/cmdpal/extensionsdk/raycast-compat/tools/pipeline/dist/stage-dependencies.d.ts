export interface DependencyResult {
    /** Whether npm install succeeded. */
    success: boolean;
    /** Combined stdout from npm. */
    stdout: string;
    /** Combined stderr from npm. */
    stderr: string;
}
/**
 * Install npm dependencies for a Raycast extension.
 *
 * Shells out to `npm install --production` to install only runtime deps.
 * Assumes Node.js and npm are on PATH (the CmdPal store gate verifies this
 * before allowing Raycast extension installs).
 */
export declare function installDependencies(extensionDir: string): Promise<DependencyResult>;
//# sourceMappingURL=stage-dependencies.d.ts.map