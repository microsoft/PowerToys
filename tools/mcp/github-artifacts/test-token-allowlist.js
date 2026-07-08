// Regression test for the GITHUB_TOKEN allowlist (security fix).
// Run with: node test-token-allowlist.js
//
// Proves that the bearer token is attached ONLY to allowlisted GitHub API hosts
// and is NEVER sent to arbitrary URLs extracted from untrusted issue content.

import assert from "node:assert/strict";
import { shouldSendGitHubToken, headersForUrl } from "./auth.js";

const TOKEN = "PT_MCP_TEST_FAKE_TOKEN";

let failures = 0;
function check(name, fn) {
  try {
    fn();
    console.log(`  ok  - ${name}`);
  } catch (err) {
    failures++;
    console.error(`  FAIL - ${name}: ${err.message}`);
  }
}

// Hosts that MUST receive the token.
const allowed = [
  "https://api.github.com/repos/microsoft/PowerToys/issues/1",
  "https://api.github.com/repos/microsoft/PowerToys/issues/1/comments?per_page=100&page=1"
];

// Hosts that MUST NOT receive the token (attacker-controlled or non-API hosts,
// including GitHub's own user-content hosts which never need the API token).
const denied = [
  "http://127.0.0.1:8000/capture.png",
  "http://127.0.0.1:8000/PowerToysReport.zip?download=1",
  "https://evil.example.com/capture.png",
  "https://evil.example.com/PowerToysReport.zip",
  "http://api.github.com/repos/microsoft/PowerToys/issues/1", // not HTTPS
  "https://api.github.com.attacker.com/x", // look-alike host
  "https://objects.githubusercontent.com/some/asset.zip",
  "https://user-images.githubusercontent.com/1/screenshot.png",
  "not a url"
];

for (const url of allowed) {
  check(`token sent to allowlisted host: ${url}`, () => {
    assert.equal(shouldSendGitHubToken(url), true);
    const h = headersForUrl(url, TOKEN);
    assert.equal(h["Authorization"], `Bearer ${TOKEN}`);
    assert.equal(h["User-Agent"], "issue-images-mcp");
  });
}

for (const url of denied) {
  check(`token withheld from untrusted host: ${url}`, () => {
    assert.equal(shouldSendGitHubToken(url), false);
    const h = headersForUrl(url, TOKEN);
    assert.equal("Authorization" in h, false, "Authorization header must not be present");
    assert.equal(h["User-Agent"], "issue-images-mcp");
  });
}

check("no token configured => no Authorization even for allowlisted host", () => {
  const h = headersForUrl("https://api.github.com/x", undefined);
  assert.equal("Authorization" in h, false);
});

check("extra headers are preserved", () => {
  const h = headersForUrl("https://api.github.com/x", TOKEN, { "Accept": "application/vnd.github+json" });
  assert.equal(h["Accept"], "application/vnd.github+json");
  assert.equal(h["Authorization"], `Bearer ${TOKEN}`);
});

console.log("");
if (failures > 0) {
  console.error(`${failures} test(s) failed.`);
  process.exit(1);
}
console.log("All token-allowlist regression tests passed.");
