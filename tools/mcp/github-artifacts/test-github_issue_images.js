// Simple test script - run with: node test-github_issue_images.js
// Make sure GITHUB_TOKEN is set in environment

import { spawn } from "child_process";

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

// Send call tool request (test with a real issue)
setTimeout(() => {
  const callToolRequest = {
    jsonrpc: "2.0",
    id: 3,
    method: "tools/call",
    params: {
      name: "github_issue_images",
      arguments: {
        owner: "microsoft",
        repo: "PowerToys",
        issueNumber: 25595,  // 315 comments, many images - tests pagination!
        maxImages: 5
      }
    }
  };
  server.stdin.write(JSON.stringify(callToolRequest) + "\n");
}, 1000);

// Track summary
let summary = { images: 0, totalKB: 0, text: "" };
let gotToolResponse = false;
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
        gotToolResponse = true;
        let imageCount = 0;
        for (const item of response.result.content) {
          if (item.type === "text") {
            console.log("Text:", item.text);
            summary.text = item.text;
          } else if (item.type === "image") {
            imageCount++;
            const sizeKB = Math.round(item.data.length * 0.75 / 1024); // base64 to actual size
            console.log(`  [Image ${imageCount}] ${item.mimeType} - ${sizeKB} KB downloaded`);
            summary.images++;
            summary.totalKB += sizeKB;
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

// Exit after 60 seconds (more time for downloads)
setTimeout(() => {
  console.log("\n" + "=".repeat(50));
  console.log("=== Test Summary ===");
  console.log("=".repeat(50));
  if (summary.text) {
    console.log(summary.text);
  }
  if (summary.images > 0) {
    console.log(`Total images downloaded: ${summary.images}`);
    console.log(`Total size: ${summary.totalKB} KB`);
  } else if (!gotToolResponse) {
    console.log("No tool response received yet. The request may still be running or was rate-limited.");
  }
  console.log("=".repeat(50));
  console.log("Test complete!");
  server.kill();
  process.exit(0);
}, 60000);
