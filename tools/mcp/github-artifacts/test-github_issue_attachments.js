// Test script for github_issue_attachments tool
// Run with: node test-github_issue_attachments.js
// Make sure GITHUB_TOKEN is set in environment

import { spawn } from "child_process";
import fs from "fs/promises";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const extractFolder = path.join(__dirname, "test-extracts");

const server = spawn("node", ["server.js"], {
  stdio: ["pipe", "pipe", "inherit"],
  env: { ...process.env }
});

// Send initialize request
const initRequest = {
  jsonrpc: "2.0",
  id: 1,
  method: "initialize",
  params: {
    protocolVersion: "2024-11-05",
    capabilities: {},
    clientInfo: { name: "test-client", version: "1.0.0" }
  }
};

server.stdin.write(JSON.stringify(initRequest) + "\n");

// Send list tools request
setTimeout(() => {
  const listToolsRequest = {
    jsonrpc: "2.0",
    id: 2,
    method: "tools/list",
    params: {}
  };
  server.stdin.write(JSON.stringify(listToolsRequest) + "\n");
}, 500);

// Send call tool request - test with PowerToys issue that has ZIP attachments
setTimeout(() => {
  const callToolRequest = {
    jsonrpc: "2.0",
    id: 3,
    method: "tools/call",
    params: {
      name: "github_issue_attachments",
      arguments: {
        owner: "microsoft",
        repo: "PowerToys",
        issueNumber: 39476,  // Has PowerToysReport_*.zip attachment
        extractFolder: extractFolder,
        maxFiles: 20
      }
    }
  };
  server.stdin.write(JSON.stringify(callToolRequest) + "\n");
}, 1000);

// Track summary
let summary = { zips: 0, files: 0, extractPath: "" };
let buffer = "";

// Read responses
server.stdout.on("data", (data) => {
  buffer += data.toString();
  
  // Try to parse complete JSON objects from buffer
  const lines = buffer.split("\n");
  buffer = lines.pop() || ""; // Keep incomplete line in buffer
  
  for (const line of lines) {
    if (!line.trim()) continue;
    try {
      const response = JSON.parse(line);
      console.log("\n=== Response ===");
      console.log("ID:", response.id);
      if (response.result?.tools) {
        console.log("Tools:", response.result.tools.map(t => t.name));
      } else if (response.result?.content) {
        for (const item of response.result.content) {
          if (item.type === "text") {
            // Truncate long file contents for display
            const text = item.text;
            if (text.startsWith("---") && text.length > 500) {
              console.log(text.substring(0, 500) + "\n... [truncated]");
            } else {
              console.log(text);
            }
            
            // Track stats
            if (text.includes("ZIP attachment")) {
              const match = text.match(/Found (\d+) ZIP/);
              if (match) summary.zips = parseInt(match[1]);
            }
            if (text.includes("extracted to:")) {
              summary.extractPath = text.match(/extracted to: (.+)/)?.[1] || "";
            }
            if (text.includes("Files:")) {
              summary.files = (text.match(/  /g) || []).length;
            }
          }
        }
      } else {
        console.log("Result:", JSON.stringify(response.result, null, 2));
      }
    } catch (e) {
      // Likely incomplete JSON, will be in next chunk
    }
  }
});

// Exit after 60 seconds (ZIP download may take time)
setTimeout(() => {
  console.log("\n" + "=".repeat(50));
  console.log("=== Test Summary ===");
  console.log("=".repeat(50));
  console.log(`ZIP files found: ${summary.zips}`);
  console.log(`Files extracted: ${summary.files}`);
  if (summary.extractPath) {
    console.log(`Extract location: ${summary.extractPath}`);
  }
  console.log("=".repeat(50));
  console.log("Cleaning up extracted files...");
  fs.rm(extractFolder, { recursive: true, force: true })
    .then(() => {
      console.log("Cleanup done.");
    })
    .catch((err) => {
      console.log(`Cleanup failed: ${err.message}`);
    })
    .finally(() => {
      console.log("Test complete!");
      server.kill();
      process.exit(0);
    });
}, 60000);
