// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast → CmdPal manifest translator.
 *
 * Converts a Raycast extension package.json into a CmdPal cmdpal.json manifest
 * that the JavaScript Extension Service can discover and load.
 *
 * Usage:
 *   node translate-manifest.js <path-to-raycast-package.json> [--output <path>]
 *
 * Output defaults to cmdpal.json in the same directory as the input file.
 */

import * as fs from "fs";
import * as path from "path";

// ---------------------------------------------------------------------------
// Raycast manifest types (subset relevant to translation)
// ---------------------------------------------------------------------------

interface RaycastCommand {
  name: string;
  title?: string;
  description?: string;
  mode?: string; // "view" | "no-view" | "menu-bar"
  subtitle?: string;
  icon?: string;
  keywords?: string[];
}

interface RaycastPreference {
  name: string;
  title?: string;
  description?: string;
  type: string; // "textfield" | "password" | "checkbox" | "dropdown" | "appPicker" | "file"
  required?: boolean;
  default?: unknown;
  placeholder?: string;
  data?: Array<{ title: string; value: string }>;
}

interface RaycastManifest {
  name?: string;
  title?: string;
  description?: string;
  icon?: string;
  author?: string;
  owner?: string;
  version?: string;
  license?: string;
  commands?: RaycastCommand[];
  preferences?: RaycastPreference[];
  platforms?: string[];
  categories?: string[];
  dependencies?: Record<string, string>;
  devDependencies?: Record<string, string>;
}

// ---------------------------------------------------------------------------
// CmdPal manifest types (mirrors JSExtensionManifest.cs exactly)
// ---------------------------------------------------------------------------

interface CmdPalManifest {
  name: string;
  displayName?: string;
  version?: string;
  description?: string;
  icon?: string;
  main: string;
  publisher?: string;
  debug?: boolean;
  debugPort?: number;
  engines?: { node?: string };
  capabilities?: string[];
}

// Extra metadata preserved alongside the CmdPal manifest for runtime use.
interface RaycastCompatMetadata {
  raycastOriginalName: string;
  commands: RaycastCommand[];
  preferences: RaycastPreference[];
  platforms: string[];
}

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

interface ValidationResult {
  valid: boolean;
  errors: string[];
  warnings: string[];
}

function validateRaycastManifest(manifest: RaycastManifest): ValidationResult {
  const errors: string[] = [];
  const warnings: string[] = [];

  // Required fields
  if (!manifest.name || manifest.name.trim() === "") {
    errors.push("Missing required field: 'name'");
  }

  if (!manifest.title || manifest.title.trim() === "") {
    errors.push("Missing required field: 'title'");
  }

  if (!manifest.commands || manifest.commands.length === 0) {
    errors.push("Missing required field: 'commands' (must have at least one command)");
  }

  // Platform check — CRITICAL
  // Raycast defaults to ["macOS"] when platforms is absent.
  const platforms = manifest.platforms ?? ["macOS"];
  const hasWindows = platforms.some(
    (p) => p.toLowerCase() === "windows"
  );

  if (!hasWindows) {
    errors.push(
      `Platform rejection: extension supports [${platforms.join(", ")}] — ` +
      `'Windows' is required. If platforms field is absent, Raycast defaults to macOS-only.`
    );
  }

  // Warnings for optional but recommended fields
  if (!manifest.description) {
    warnings.push("Missing optional field: 'description'");
  }

  if (!manifest.author && !manifest.owner) {
    warnings.push("Missing optional field: 'author' (or 'owner')");
  }

  if (!manifest.icon) {
    warnings.push("Missing optional field: 'icon'");
  }

  if (!manifest.version) {
    warnings.push("Missing optional field: 'version' — defaulting to '1.0.0'");
  }

  // Validate individual commands
  if (manifest.commands) {
    for (let i = 0; i < manifest.commands.length; i++) {
      const cmd = manifest.commands[i];
      if (!cmd.name || cmd.name.trim() === "") {
        errors.push(`commands[${i}]: missing required field 'name'`);
      }
    }
  }

  return {
    valid: errors.length === 0,
    errors,
    warnings,
  };
}

// ---------------------------------------------------------------------------
// Translation
// ---------------------------------------------------------------------------

function mapCapabilities(commands: RaycastCommand[]): string[] {
  // CmdPal capabilities are simple string tags.
  // Every Raycast extension provides commands at minimum.
  const caps = new Set<string>(["commands"]);

  for (const cmd of commands) {
    // Raycast "view" mode maps to list/content pages; "no-view" stays as commands-only.
    if (cmd.mode === "view") {
      caps.add("listPages");
    }
  }

  return Array.from(caps);
}

function mapIconPath(iconField: string | undefined): string | undefined {
  if (!iconField) {
    return undefined;
  }

  // Raycast icons are typically just filenames ("icon.png") resolved relative
  // to the assets/ folder, or references like "command-icon.png".
  // CmdPal expects a relative path from the extension root.
  // We normalize to assets/<filename> if the icon looks like a bare filename.
  if (!iconField.includes("/") && !iconField.includes("\\")) {
    return `assets/${iconField}`;
  }

  return iconField;
}

function translate(raycast: RaycastManifest): {
  manifest: CmdPalManifest;
  metadata: RaycastCompatMetadata;
} {
  const commands = raycast.commands ?? [];
  const preferences = raycast.preferences ?? [];
  const platforms = raycast.platforms ?? ["macOS"];

  const cmdpalManifest: CmdPalManifest = {
    // Prefix with "raycast-" to avoid naming collisions with native CmdPal extensions
    name: `raycast-${raycast.name}`,
    displayName: raycast.title,
    version: raycast.version ?? "1.0.0",
    description: raycast.description,
    icon: mapIconPath(raycast.icon),
    // Entry point for the Raycast compat runtime shim
    main: "dist/index.js",
    publisher: raycast.author ?? raycast.owner,
    engines: { node: ">=18" },
    capabilities: mapCapabilities(commands),
  };

  const metadata: RaycastCompatMetadata = {
    raycastOriginalName: raycast.name ?? "",
    commands,
    preferences,
    platforms,
  };

  return { manifest: cmdpalManifest, metadata };
}

// ---------------------------------------------------------------------------
// File I/O
// ---------------------------------------------------------------------------

function readRaycastManifest(inputPath: string): RaycastManifest {
  const resolvedPath = path.resolve(inputPath);

  if (!fs.existsSync(resolvedPath)) {
    throw new Error(`Input file not found: ${resolvedPath}`);
  }

  const raw = fs.readFileSync(resolvedPath, "utf-8");

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    throw new Error(`Failed to parse JSON from: ${resolvedPath}`);
  }

  return parsed as RaycastManifest;
}

function writeOutput(
  outputPath: string,
  manifest: CmdPalManifest,
  metadata: RaycastCompatMetadata
): void {
  const resolvedPath = path.resolve(outputPath);
  const dir = path.dirname(resolvedPath);

  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }

  // Write the CmdPal manifest
  fs.writeFileSync(resolvedPath, JSON.stringify(manifest, null, 2) + "\n", "utf-8");

  // Write companion metadata for the Raycast compat runtime layer
  const metadataPath = path.join(dir, "raycast-compat.json");
  fs.writeFileSync(metadataPath, JSON.stringify(metadata, null, 2) + "\n", "utf-8");
}

// ---------------------------------------------------------------------------
// CLI entry point
// ---------------------------------------------------------------------------

function printUsage(): void {
  console.log(`
Raycast → CmdPal Manifest Translator

Usage:
  node translate-manifest.js <path-to-raycast-package.json> [--output <path>]

Options:
  --output, -o   Output path for cmdpal.json (default: cmdpal.json in same directory as input)
  --help, -h     Show this help message

Examples:
  node translate-manifest.js ./my-raycast-ext/package.json
  node translate-manifest.js ./package.json --output ./dist/cmdpal.json
`);
}

function main(): void {
  const args = process.argv.slice(2);

  if (args.length === 0 || args.includes("--help") || args.includes("-h")) {
    printUsage();
    process.exit(args.length === 0 ? 1 : 0);
  }

  // Parse arguments
  let inputPath: string | undefined;
  let outputPath: string | undefined;

  for (let i = 0; i < args.length; i++) {
    if (args[i] === "--output" || args[i] === "-o") {
      i++;
      if (i >= args.length) {
        console.error("Error: --output requires a path argument");
        process.exit(1);
      }
      outputPath = args[i];
    } else if (!args[i].startsWith("-")) {
      inputPath = args[i];
    } else {
      console.error(`Unknown option: ${args[i]}`);
      printUsage();
      process.exit(1);
    }
  }

  if (!inputPath) {
    console.error("Error: No input file specified");
    printUsage();
    process.exit(1);
    return; // unreachable, helps type narrowing
  }

  // Default output to cmdpal.json in the same directory as input
  if (!outputPath) {
    outputPath = path.join(path.dirname(path.resolve(inputPath)), "cmdpal.json");
  }

  // Read and validate
  console.log(`Reading Raycast manifest: ${path.resolve(inputPath)}`);
  const raycast = readRaycastManifest(inputPath);

  const validation = validateRaycastManifest(raycast);

  if (validation.warnings.length > 0) {
    console.log("\nWarnings:");
    for (const w of validation.warnings) {
      console.log(`  ⚠ ${w}`);
    }
  }

  if (!validation.valid) {
    console.error("\nValidation errors:");
    for (const e of validation.errors) {
      console.error(`  ✗ ${e}`);
    }
    console.error("\nTranslation aborted.");
    process.exit(1);
  }

  // Translate
  const { manifest, metadata } = translate(raycast);

  // Write output
  writeOutput(outputPath, manifest, metadata);

  console.log(`\n✓ CmdPal manifest written to: ${path.resolve(outputPath)}`);
  console.log(`✓ Compat metadata written to: ${path.resolve(path.join(path.dirname(outputPath), "raycast-compat.json"))}`);

  // Summary
  console.log(`\nTranslation summary:`);
  console.log(`  Name:         ${manifest.name}`);
  console.log(`  Display name: ${manifest.displayName}`);
  console.log(`  Publisher:    ${manifest.publisher ?? "(none)"}`);
  console.log(`  Version:      ${manifest.version}`);
  console.log(`  Entry point:  ${manifest.main}`);
  console.log(`  Capabilities: [${manifest.capabilities?.join(", ")}]`);
  console.log(`  Commands:     ${metadata.commands.length}`);
  console.log(`  Preferences:  ${metadata.preferences.length}`);
}

main();
