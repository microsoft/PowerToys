import { existsSync } from "fs";
import { execSync } from "child_process";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
process.chdir(__dirname);

if (!existsSync("node_modules")) {
  console.log("[MCP] Installing dependencies...");
  execSync("npm install", { stdio: "inherit" });
}

import("./server.js");