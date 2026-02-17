import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import fs from "fs/promises";
import path from "path";
import { execFile } from "child_process";

const server = new McpServer({
  name: "issue-images",
  version: "0.1.0"
});

const GH_API_PER_PAGE = 100;
const MAX_TEXT_FILE_BYTES = 100000;
// Limit to common text files to avoid binary blobs, huge payloads, and non-UTF8 noise.
const TEXT_EXTENSIONS = new Set([
  ".txt", ".log", ".json", ".xml", ".yaml", ".yml", ".md", ".csv",
  ".ini", ".config", ".conf", ".bat", ".ps1", ".sh", ".reg", ".etl"
]);

function extractImageUrls(markdownOrHtml) {
  const urls = new Set();

  // Markdown images: ![alt](url)
  for (const m of markdownOrHtml.matchAll(/!\[[^\]]*?\]\((https?:\/\/[^\s)]+)\)/g)) {
    urls.add(m[1]);
  }

  // HTML <img src="...">
  for (const m of markdownOrHtml.matchAll(/<img[^>]+src="(https?:\/\/[^">]+)"/g)) {
    urls.add(m[1]);
  }

  return [...urls];
}

function extractZipUrls(markdownOrHtml) {
  const urls = new Set();

  // Markdown links to .zip files: [text](url.zip)
  for (const m of markdownOrHtml.matchAll(/\[[^\]]*?\]\((https?:\/\/[^\s)]+\.zip)\)/gi)) {
    urls.add(m[1]);
  }

  // Plain URLs ending in .zip
  for (const m of markdownOrHtml.matchAll(/(https?:\/\/[^\s<>"]+\.zip)/gi)) {
    urls.add(m[1]);
  }

  return [...urls];
}

async function fetchJson(url, token) {
  const res = await fetch(url, {
    headers: {
      "Accept": "application/vnd.github+json",
      ...(token ? { "Authorization": `Bearer ${token}` } : {}),
      "X-GitHub-Api-Version": "2022-11-28",
      "User-Agent": "issue-images-mcp"
    }
  });
  if (!res.ok) throw new Error(`GitHub API failed: ${res.status} ${res.statusText}`);
  return await res.json();
}

async function downloadBytes(url, token) {
  const res = await fetch(url, {
    headers: {
      ...(token ? { "Authorization": `Bearer ${token}` } : {}),
      "User-Agent": "issue-images-mcp"
    }
  });
  if (!res.ok) throw new Error(`Image download failed: ${res.status} ${res.statusText}`);
  const buf = new Uint8Array(await res.arrayBuffer());
  const ct = res.headers.get("content-type") || "image/png";
  return { buf, mimeType: ct };
}

async function downloadZipBytes(url, token) {
  const zipUrl = url.includes("?") ? url : `${url}?download=1`;

  const tryFetch = async (useAuth) => {
    const res = await fetch(zipUrl, {
      headers: {
        "Accept": "application/octet-stream",
        ...(useAuth && token ? { "Authorization": `Bearer ${token}` } : {}),
        "User-Agent": "issue-images-mcp"
      },
      redirect: "follow"
    });

    if (!res.ok) throw new Error(`ZIP download failed: ${res.status} ${res.statusText}`);

    const contentType = (res.headers.get("content-type") || "").toLowerCase();
    const buf = new Uint8Array(await res.arrayBuffer());

    return { buf, contentType };
  };

  let { buf, contentType } = await tryFetch(true);
  const isZip = buf.length >= 4 && buf[0] === 0x50 && buf[1] === 0x4b;

  if (!isZip) {
    ({ buf, contentType } = await tryFetch(false));
  }

  const isZipRetry = buf.length >= 4 && buf[0] === 0x50 && buf[1] === 0x4b;

  if (!isZipRetry || contentType.includes("text/html") || buf.length < 100) {
    throw new Error("ZIP download returned HTML or invalid data. Check permissions or rate limits.");
  }

  return buf;
}

function execFileAsync(file, args) {
  return new Promise((resolve, reject) => {
    execFile(file, args, (error, stdout, stderr) => {
      if (error) {
        reject(new Error(stderr || error.message));
      } else {
        resolve(stdout);
      }
    });
  });
}

async function extractZipToFolder(zipPath, extractPath) {
  if (process.platform === "win32") {
    await execFileAsync("powershell", [
      "-NoProfile",
      "-NonInteractive",
      "-Command",
      `$ProgressPreference='SilentlyContinue'; Expand-Archive -Path \"${zipPath}\" -DestinationPath \"${extractPath}\" -Force -ErrorAction Stop | Out-Null`
    ]);
    return;
  }

  await execFileAsync("unzip", ["-o", zipPath, "-d", extractPath]);
}

async function listFilesRecursively(dir) {
  const entries = await fs.readdir(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...await listFilesRecursively(fullPath));
    } else {
      files.push(fullPath);
    }
  }
  return files;
}

async function pathExists(p) {
  try {
    await fs.access(p);
    return true;
  } catch {
    return false;
  }
}

async function fetchAllComments(owner, repo, issueNumber, token) {
  let comments = [];
  let page = 1;

  while (true) {
    const pageComments = await fetchJson(
      `https://api.github.com/repos/${owner}/${repo}/issues/${issueNumber}/comments?per_page=${GH_API_PER_PAGE}&page=${page}`,
      token
    );
    comments = comments.concat(pageComments);
    if (pageComments.length < GH_API_PER_PAGE) break;
    page++;
  }

  return comments;
}

async function fetchIssueAndComments(owner, repo, issueNumber, token) {
  const issue = await fetchJson(`https://api.github.com/repos/${owner}/${repo}/issues/${issueNumber}`, token);
  const comments = issue.comments > 0 ? await fetchAllComments(owner, repo, issueNumber, token) : [];
  return { issue, comments };
}

function buildBlobs(issue, comments) {
  return [issue.body || "", ...comments.map(c => c.body || "")].join("\n\n---\n\n");
}

server.registerTool(
  "github_issue_images",
  {
    title: "GitHub Issue Images",
    description: `Download and return images from a GitHub issue or pull request.

USE THIS TOOL WHEN:
- User asks about a GitHub issue/PR that contains screenshots, images, or visual content
- User wants to understand a bug report with attached images
- User asks to analyze, describe, or review images in an issue/PR
- User references a GitHub issue/PR URL and the context suggests images are relevant
- User asks about UI bugs, visual glitches, design issues, or anything visual in nature

WHAT IT DOES:
- Fetches all images from the issue/PR body and all comments
- Returns actual image data (not just URLs) so the LLM can see and analyze the images
- Supports PNG, JPEG, GIF, and other common image formats

EXAMPLES OF WHEN TO USE:
- "What does the bug in issue #123 look like?"
- "Can you see the screenshot in this PR?"
- "Analyze the images in microsoft/PowerToys#25595"
- "What UI problem is shown in this issue?"`,
    inputSchema: {
      owner: z.string(),
      repo: z.string(),
      issueNumber: z.number(),
      maxImages: z.number().min(1).max(20).optional()
    },
    outputSchema: {
      images: z.number(),
      comments: z.number()
    }
  },
  async ({ owner, repo, issueNumber, maxImages = 20 }) => {
    try {
      const token = process.env.GITHUB_TOKEN;
      const { issue, comments } = await fetchIssueAndComments(owner, repo, issueNumber, token);
      const blobs = buildBlobs(issue, comments);
      const urls = extractImageUrls(blobs).slice(0, maxImages);

      const content = [
        { type: "text", text: `Found ${urls.length} image(s) in issue #${issueNumber} (from ${comments.length} comments). Returning as image parts.` }
      ];

      for (const url of urls) {
        const { buf, mimeType } = await downloadBytes(url, token);
        const b64 = Buffer.from(buf).toString("base64");
        content.push({ type: "image", data: b64, mimeType });
        content.push({ type: "text", text: `Image source: ${url}` });
      }

      const output = { images: urls.length, comments: comments.length };
      return { content, structuredContent: output };
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      return { content: [{ type: "text", text: `Error: ${message}` }], isError: true };
    }
  }
);

server.registerTool(
  "github_issue_attachments",
  {
    title: "GitHub Issue Attachments",
    description: `Download and extract ZIP file attachments from a GitHub issue or pull request.

USE THIS TOOL WHEN:
- User asks about diagnostic logs, crash reports, or debug information in an issue
- Issue contains ZIP attachments like PowerToysReport_*.zip, logs.zip, debug.zip
- User wants to analyze log files, configuration files, or system info from an issue
- User asks about error logs, stack traces, or diagnostic data attached to an issue
- Issue mentions attached files that need to be examined

WHAT IT DOES:
- Finds all ZIP file attachments in the issue body and comments
- Downloads and extracts each ZIP to a local folder
- Returns file listing and contents of text files (logs, json, xml, txt, etc.)
- Each ZIP is extracted to: {extractFolder}/{zipFileName}/

EXAMPLES OF WHEN TO USE:
- "What's in the diagnostic report attached to issue #39476?"
- "Can you check the logs in the PowerToysReport zip?"
- "Analyze the crash dump attached to this issue"
- "What error is shown in the attached log files?"`,
    inputSchema: {
      owner: z.string(),
      repo: z.string(),
      issueNumber: z.number(),
      extractFolder: z.string(),
      maxFiles: z.number().min(1).optional()
    },
    outputSchema: {
      zips: z.number(),
      extracted: z.number(),
      extractedTo: z.array(z.string())
    }
  },
  async ({ owner, repo, issueNumber, extractFolder, maxFiles = 50 }) => {
    try {
      const token = process.env.GITHUB_TOKEN;
      const { issue, comments } = await fetchIssueAndComments(owner, repo, issueNumber, token);
      const blobs = buildBlobs(issue, comments);
      const zipUrls = extractZipUrls(blobs);

      if (zipUrls.length === 0) {
        return {
          content: [{ type: "text", text: `No ZIP attachments found in issue #${issueNumber}.` }],
          structuredContent: { zips: 0, extracted: 0, extractedTo: [] }
        };
      }

      await fs.mkdir(extractFolder, { recursive: true });

      const content = [
        { type: "text", text: `Found ${zipUrls.length} ZIP attachment(s) in issue #${issueNumber}. Extracting to: ${extractFolder}` }
      ];

      let totalFilesReturned = 0;
      const extractedPaths = [];
      let extractedCount = 0;

      for (const zipUrl of zipUrls) {
        try {
          const urlPath = new URL(zipUrl).pathname;
          const zipFileName = path.basename(urlPath, ".zip");
          const extractPath = path.join(extractFolder, zipFileName);
          const zipPath = path.join(extractFolder, `${zipFileName}.zip`);

          let extractedFiles = [];
          const extractPathExists = await pathExists(extractPath);

          if (extractPathExists) {
            extractedFiles = await listFilesRecursively(extractPath);
          }

          if (!extractPathExists || extractedFiles.length === 0) {
            const zipExists = await pathExists(zipPath);
            if (!zipExists) {
              const buf = await downloadZipBytes(zipUrl, token);
              await fs.writeFile(zipPath, buf);
            }

            await fs.mkdir(extractPath, { recursive: true });
            await extractZipToFolder(zipPath, extractPath);
            extractedFiles = await listFilesRecursively(extractPath);
          }

          extractedPaths.push(extractPath);
          extractedCount++;

          const fileList = [];
          const textContents = [];

          for (const fullPath of extractedFiles) {
            const relPath = path.relative(extractPath, fullPath).replace(/\\/g, "/");
            const ext = path.extname(relPath).toLowerCase();
            const stat = await fs.stat(fullPath);
            const sizeKB = Math.round(stat.size / 1024);
            fileList.push(`  ${relPath} (${sizeKB} KB)`);

            if (TEXT_EXTENSIONS.has(ext) && totalFilesReturned < maxFiles && stat.size < MAX_TEXT_FILE_BYTES) {
              try {
                const textContent = await fs.readFile(fullPath, "utf-8");
                textContents.push({ path: relPath, content: textContent });
                totalFilesReturned++;
              } catch {
                // Not valid UTF-8, skip
              }
            }
          }

          content.push({ type: "text", text: `\n📦 ${zipFileName}.zip extracted to: ${extractPath}\nFiles:\n${fileList.join("\n")}` });

          for (const { path: fPath, content: fContent } of textContents) {
            content.push({ type: "text", text: `\n--- ${fPath} ---\n${fContent}` });
          }
        } catch (err) {
          const message = err instanceof Error ? err.message : String(err);
          content.push({ type: "text", text: `❌ Failed to extract ${zipUrl}: ${message}` });
        }
      }

      const output = { zips: zipUrls.length, extracted: extractedCount, extractedTo: extractedPaths };
      return { content, structuredContent: output };
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      return { content: [{ type: "text", text: `Error: ${message}` }], isError: true };
    }
  }
);

const transport = new StdioServerTransport();
await server.connect(transport);
