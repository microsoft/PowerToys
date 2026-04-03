/**
 * generate-monaco-languages.js
 *
 * Generates monaco_languages.json using Puppeteer to run the existing
 * generateLanguagesJson.html in a headless browser. This exactly mirrors
 * the manual process described in doc/devdocs/common/FilePreviewCommon.md.
 *
 * Usage: node generate-monaco-languages.js <path-to-src/Monaco>
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

async function main() {
  let server;
  let browser;

  try {
    // Step 1: Start a local HTTP server serving the Monaco directory.
    // The generateLanguagesJson.html must be served over HTTP because
    // browsers block ES module imports and AMD require from file:// URLs.
    server = await startServer(absMonacoDir);
    const port = server.address().port;
    console.log(`Local server started on port ${port}`);

    // Step 2: Launch headless browser via Puppeteer
    const puppeteer = require("puppeteer");
    browser = await puppeteer.launch({
      headless: true,
      args: ["--no-sandbox", "--disable-setuid-sandbox"],
    });

    const page = await browser.newPage();

    // Step 3: Set up download interception.
    // generateLanguagesJson.html creates an <a> element and clicks it to
    // trigger a download of monaco_languages.json. We intercept this
    // using CDP to redirect downloads to a temp directory.
    const downloadDir = path.join(
      absMonacoDir,
      ".monaco-download-tmp"
    );
    fs.mkdirSync(downloadDir, { recursive: true });

    try {
      const cdp = await browser.target().createCDPSession();
      await cdp.send("Browser.setDownloadBehavior", {
        behavior: "allow",
        downloadPath: downloadDir,
      });

      const pageCdp = await page.createCDPSession();
      await pageCdp.send("Page.setDownloadBehavior", {
        behavior: "allow",
        downloadPath: downloadDir,
      });
    } catch (err) {
      throw new Error(
        `Failed to configure download behavior via CDP: ${err.message}`
      );
    }

    // Step 4: Navigate to the generator page.
    // The page auto-loads Monaco, registers custom languages, calls
    // getLanguages(), and triggers a download of the JSON.
    console.log("Navigating to generateLanguagesJson.html...");
    await page.goto(`http://localhost:${port}/generateLanguagesJson.html`, {
      waitUntil: "networkidle0",
      timeout: 60000,
    });

    // Step 5: Wait for the download to complete.
    const downloadedFile = await waitForDownload(
      downloadDir,
      "monaco_languages.json",
      30000
    );

    // Step 6: Move the downloaded file to the target location.
    const downloadedContent = fs.readFileSync(downloadedFile, "utf-8");

    // Validate the content is valid JSON before writing
    const parsed = JSON.parse(downloadedContent);
    if (!parsed.list || !Array.isArray(parsed.list)) {
      throw new Error(
        "Downloaded JSON does not have the expected { list: [...] } structure"
      );
    }

    fs.writeFileSync(outputPath, downloadedContent, "utf-8");
    console.log(
      `monaco_languages.json written with ${parsed.list.length} languages.`
    );
  } catch (err) {
    console.error("Failed to generate monaco_languages.json:", err.message);
    process.exit(1);
  } finally {
    if (browser) {
      await browser.close().catch(() => {});
    }
    if (server) {
      server.close();
    }
    // Clean up temp download directory AFTER browser is closed
    const downloadDir = path.join(absMonacoDir, ".monaco-download-tmp");
    if (fs.existsSync(downloadDir)) {
      fs.rmSync(downloadDir, { recursive: true, force: true });
    }
  }
}

/**
 * Starts a simple HTTP server that serves static files from the given
 * directory. Supports .js, .html, .css, .json, .ttf MIME types.
 */
function startServer(rootDir) {
  return new Promise((resolve, reject) => {
    const mimeTypes = {
      ".html": "text/html",
      ".js": "application/javascript",
      ".mjs": "application/javascript",
      ".css": "text/css",
      ".json": "application/json",
      ".ttf": "font/ttf",
      ".woff": "font/woff",
      ".woff2": "font/woff2",
      ".svg": "image/svg+xml",
      ".png": "image/png",
    };

    const server = http.createServer((req, res) => {
      const urlPath = decodeURIComponent(req.url.split("?")[0]);
      const filePath = path.join(rootDir, urlPath);

      // Security: ensure we don't serve files outside rootDir
      const resolvedRoot = path.resolve(rootDir);
      const resolvedPath = path.resolve(rootDir, urlPath);
      if (
        resolvedPath !== resolvedRoot &&
        !resolvedPath.startsWith(resolvedRoot + path.sep)
      ) {
        res.writeHead(403);
        res.end("Forbidden");
        return;
      }

      if (!fs.existsSync(resolvedPath) || fs.statSync(resolvedPath).isDirectory()) {
        res.writeHead(404);
        res.end("Not Found");
        return;
      }

      const ext = path.extname(resolvedPath).toLowerCase();
      const contentType = mimeTypes[ext] || "application/octet-stream";

      const content = fs.readFileSync(resolvedPath);
      res.writeHead(200, { "Content-Type": contentType });
      res.end(content);
    });

    server.listen(0, "127.0.0.1", () => {
      resolve(server);
    });

    server.on("error", reject);
  });
}

/**
 * Waits for a file to appear in the download directory.
 * Puppeteer downloads may have a .crdownload suffix while in progress.
 */
function waitForDownload(downloadDir, expectedFilename, timeoutMs) {
  return new Promise((resolve, reject) => {
    const startTime = Date.now();

    const check = () => {
      const files = fs.readdirSync(downloadDir);

      // Check for the expected file (not a .crdownload partial)
      const targetFile = files.find(
        (f) => f === expectedFilename && !f.endsWith(".crdownload")
      );

      if (targetFile) {
        const filePath = path.join(downloadDir, targetFile);
        // Ensure file has content (not still being written)
        const stat = fs.statSync(filePath);
        if (stat.size > 0) {
          resolve(filePath);
          return;
        }
      }

      if (Date.now() - startTime > timeoutMs) {
        reject(
          new Error(
            `Timed out waiting for ${expectedFilename} download after ${timeoutMs}ms. ` +
              `Files in download dir: ${files.join(", ") || "(empty)"}`
          )
        );
        return;
      }

      setTimeout(check, 500);
    };

    check();
  });
}

main();
