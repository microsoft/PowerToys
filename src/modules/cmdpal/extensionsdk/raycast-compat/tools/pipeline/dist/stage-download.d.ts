export interface DownloadResult {
    /** Temporary directory containing the downloaded extension source. */
    tempDir: string;
    /** List of files that were written. */
    files: string[];
}
/**
 * Download a Raycast extension's source files to a temporary directory.
 *
 * Uses the GitHub client's `downloadExtension()` which walks the Git tree
 * and fetches blobs in parallel batches.
 */
export declare function downloadExtension(extensionName: string, githubToken?: string): Promise<DownloadResult>;
//# sourceMappingURL=stage-download.d.ts.map