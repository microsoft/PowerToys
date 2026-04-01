/**
 * generate-monaco-languages.js
 *
 * Headless replacement for the manual generateLanguagesJson.html workflow.
 * Serves the Monaco directory on a local HTTP server, then uses Playwright
 * to open generateLanguagesJson.html and capture the generated JSON.
 *
 * Monaco's AMD loader requires a real browser DOM, so we use Playwright
 * instead of trying to load it in Node.js directly.
 *
 * Usage: node generate-monaco-languages.js <path-to-src/Monaco>
 *
 * Prerequisites: npx playwright install chromium
 */

"use strict";

const path = require("path");
const fs = require("fs");
const http = require("http");

const monacoDir = process.argv[2];
if (!monacoDir) {
  console.error("Usage: node generate-monaco-languages.js <monaco-dir>");
  process.exit(1);
}

const absMonacoDir = path.resolve(monacoDir);
const outputPath = path.join(absMonacoDir, "monaco_languages.json");
const htmlPath = path.join(absMonacoDir, "generateLanguagesJson.html");

if (!fs.existsSync(htmlPath)) {
  console.error(`generateLanguagesJson.html not found at: ${htmlPath}`);
  process.exit(1);
}

// MIME types for serving Monaco files
const MIME_TYPES = {
  ".html": "text/html",
  ".js": "application/javascript",
  ".css": "text/css",
  ".json": "application/json",
  ".ttf": "font/ttf",
  ".woff": "font/woff",
  ".woff2": "font/woff2",
  ".svg": "image/svg+xml",
};

/**
 * Create a simple static file server rooted at absMonacoDir.
 */
function createServer() {
  return http.createServer((req, res) => {
    const url = new URL(req.url, "http://localhost");
    // Resolve the file path relative to absMonacoDir, preventing path traversal
    const requestedPath = decodeURIComponent(url.pathname);
    const filePath = path.resolve(absMonacoDir, requestedPath.replace(/^\/+/, ""));

    // Ensure the resolved path is within absMonacoDir
    if (!filePath.startsWith(absMonacoDir + path.sep) && filePath !== absMonacoDir) {
      res.writeHead(403);
      res.end("Forbidden");
      return;
    }

    if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
      res.writeHead(404);
      res.end("Not found");
      return;
    }

    const ext = path.extname(filePath).toLowerCase();
    const contentType = MIME_TYPES[ext] || "application/octet-stream";

    const content = fs.readFileSync(filePath);
    res.writeHead(200, { "Content-Type": contentType });
    res.end(content);
  });
}

async function main() {
  // Start local HTTP server
  const server = createServer();
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const port = server.address().port;
  const baseUrl = `http://127.0.0.1:${port}`;
  console.log(`Local server started at ${baseUrl}`);

  let browser;
  try {
    // Launch Playwright browser
    const { chromium } = require("playwright");
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({
      // Accept downloads so we can capture the generated file
      acceptDownloads: true,
    });
    const page = await context.newPage();

    // The generateLanguagesJson.html auto-triggers a download of the JSON.
    // We intercept that download event to capture the content.
    const downloadPromise = page.waitForEvent("download", { timeout: 30000 });

    console.log("Loading generateLanguagesJson.html in headless browser...");
    await page.goto(`${baseUrl}/generateLanguagesJson.html`, {
      waitUntil: "networkidle",
      timeout: 30000,
    });

    // Wait for the download to be triggered
    const download = await downloadPromise;
    console.log(`Download triggered: ${download.suggestedFilename()}`);

    // Save the downloaded file
    const downloadPath = await download.path();
    const content = fs.readFileSync(downloadPath, "utf-8");

    // Validate the JSON before writing
    const parsed = JSON.parse(content);
    if (!parsed.list || !Array.isArray(parsed.list) || parsed.list.length === 0) {
      throw new Error(
        "Generated JSON is invalid: missing or empty 'list' property"
      );
    }

    // Write to output
    fs.writeFileSync(outputPath, content, "utf-8");
    console.log(
      `monaco_languages.json written with ${parsed.list.length} languages.`
    );
  } finally {
    if (browser) {
      await browser.close();
    }
    server.close();
  }
}

main().catch((err) => {
  console.error("Error:", err.message || err);
  process.exit(1);
});
