export interface ValidationResult {
    /** Whether validation passed. */
    valid: boolean;
    /** Blocking errors that prevent installation. */
    errors: string[];
    /** Non-blocking warnings. */
    warnings: string[];
}
/**
 * Validate a downloaded Raycast extension for CmdPal compatibility.
 *
 * Checks:
 * 1. package.json exists and is parseable
 * 2. Required fields present (name, title, commands)
 * 3. Platform includes "Windows" (Raycast defaults to macOS-only)
 * 4. At least one command entry
 */
export declare function validateExtension(extensionDir: string): ValidationResult;
//# sourceMappingURL=stage-validate.d.ts.map