// Centralized control over when the GitHub bearer token may be attached to an
// outgoing request.
//
// Image and ZIP URLs are extracted from untrusted issue/PR Markdown and can
// point to any host. The GitHub bearer token (GITHUB_TOKEN) must therefore only
// ever be sent to explicitly allowlisted GitHub API hosts, never to URLs that
// originate from issue content. Sending it elsewhere would leak the token to an
// attacker-controlled server.

export const AUTH_ALLOWED_HOSTS = new Set([
  "api.github.com"
]);

// Returns true only when it is safe to attach the GitHub bearer token to the
// given URL: the request must be HTTPS and target an allowlisted GitHub API host.
export function shouldSendGitHubToken(rawUrl) {
  let parsed;
  try {
    parsed = new URL(rawUrl);
  } catch {
    return false;
  }
  return parsed.protocol === "https:" && AUTH_ALLOWED_HOSTS.has(parsed.hostname.toLowerCase());
}

// Builds request headers, attaching Authorization only when the target URL is an
// allowlisted GitHub API host. Arbitrary image/ZIP URLs from issue content never
// receive the token.
export function headersForUrl(rawUrl, token, extra = {}) {
  return {
    ...extra,
    ...(token && shouldSendGitHubToken(rawUrl) ? { "Authorization": `Bearer ${token}` } : {}),
    "User-Agent": "issue-images-mcp"
  };
}
