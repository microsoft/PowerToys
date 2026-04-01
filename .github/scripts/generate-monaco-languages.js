/**
 * generate-monaco-languages.js
 *
 * Headless Node.js replacement for generateLanguagesJson.html.
 * Loads the Monaco AMD loader natively, registers built-in and custom
 * languages, then writes monaco_languages.json.
 *
 * Usage: node generate-monaco-languages.js <path-to-src/Monaco>
 */

"use strict";

const path = require("path");
const fs = require("fs");

const monacoDir = process.argv[2];
if (!monacoDir) {
  console.error("Usage: node generate-monaco-languages.js <monaco-dir>");
  process.exit(1);
}

const absMonacoDir = path.resolve(monacoDir);
const monacoSrcMin = path.join(absMonacoDir, "monacoSRC", "min");
const loaderPath = path.join(monacoSrcMin, "vs", "loader.js");
const outputPath = path.join(absMonacoDir, "monaco_languages.json");
const specialLangPath = path.join(absMonacoDir, "monacoSpecialLanguages.js");
const customLangDir = path.join(absMonacoDir, "customLanguages");

if (!fs.existsSync(loaderPath)) {
  console.error(`loader.js not found at: ${loaderPath}`);
  process.exit(1);
}

// Load the AMD loader into the global scope.
// We must use eval (via Function) so that `this` inside the loader IIFE
// resolves to `globalThis`, not `module.exports` (which `require()` would do).
const loaderCode = fs.readFileSync(loaderPath, "utf-8");
new Function(loaderCode).call(globalThis);

// The AMD loader installs `require` and `define` on globalThis
const amdRequire = globalThis.require;
if (!amdRequire || typeof amdRequire.config !== "function") {
  console.error("AMD loader did not install correctly on globalThis");
  process.exit(1);
}

// Configure the AMD require to find Monaco modules
amdRequire.config({
  paths: {
    vs: path.join(monacoSrcMin, "vs").replace(/\\/g, "/"),
  },
});

// Load vs/editor/editor.main which registers all built-in languages
amdRequire(
  ["vs/editor/editor.main"],
  function (monaco) {
    // Register the custom / additional languages from monacoSpecialLanguages.js
    registerCustomLanguages(monaco);

    // Write the JSON
    const languages = monaco.languages.getLanguages();
    const output = JSON.stringify({ list: languages });

    fs.writeFileSync(outputPath, output + "\n", "utf-8");
    console.log(
      `monaco_languages.json written with ${languages.length} languages.`
    );
  },
  function (err) {
    console.error("Failed to load Monaco editor:", err);
    process.exit(1);
  }
);

// ─── Custom language registration ────────────────────────────────────

function registerCustomLanguages(monaco) {
  if (!fs.existsSync(specialLangPath)) {
    console.warn(
      "monacoSpecialLanguages.js not found, skipping custom languages"
    );
    return;
  }

  const specialContent = fs.readFileSync(specialLangPath, "utf-8");

  // Parse registerAdditionalLanguage calls (existing language extensions)
  const addLangRegex =
    /registerAdditionalLanguage\(\s*"([^"]+)"\s*,\s*\[([^\]]*)\]\s*,\s*"([^"]+)"/g;
  let match;
  while ((match = addLangRegex.exec(specialContent)) !== null) {
    const id = match[1];
    const extensionsStr = match[2];
    const extensions = extensionsStr
      .split(",")
      .map((e) => e.trim().replace(/"/g, ""))
      .filter((e) => e.length > 0);

    monaco.languages.register({ id, extensions });
  }

  // Parse registerAdditionalNewLanguage calls (brand new languages)
  const newLangRegex =
    /registerAdditionalNewLanguage\(\s*"([^"]+)"\s*,\s*\[([^\]]*)\]/g;
  while ((match = newLangRegex.exec(specialContent)) !== null) {
    const id = match[1];
    const extensionsStr = match[2];
    const extensions = extensionsStr
      .split(",")
      .map((e) => e.trim().replace(/"/g, ""))
      .filter((e) => e.length > 0);

    monaco.languages.register({ id, extensions });
  }
}
