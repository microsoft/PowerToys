# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [string[]]$InputReports = @(
        ".\tools\SettingsSearchEvaluation\artifacts\report.basic.direct.json",
        ".\tools\SettingsSearchEvaluation\artifacts\report.semantic.aumid.json"
    ),
    [string]$OutputHtml = ".\tools\SettingsSearchEvaluation\artifacts\search-profile-dashboard.html"
)

$ErrorActionPreference = "Stop"

function Get-EngineName {
    param([int]$EngineCode)
    switch ($EngineCode) {
        0 { "Basic" }
        1 { "Semantic" }
        default { "Engine$EngineCode" }
    }
}

$runRows = @()
foreach ($path in $InputReports) {
    if (-not (Test-Path $path)) {
        Write-Warning "Skipping missing report: $path"
        continue
    }

    $resolvedPath = (Resolve-Path $path).Path
    $report = Get-Content -Path $resolvedPath -Raw | ConvertFrom-Json
    $engines = @($report.Engines)

    foreach ($engine in $engines) {
        $caseResults = @($engine.CaseResults)
        $hitCount = ($caseResults | Where-Object { $_.HitAtK }).Count
        $missCount = ($caseResults | Where-Object { -not $_.HitAtK }).Count

        $runRows += [ordered]@{
            sourceFile = [System.IO.Path]::GetFileName($resolvedPath)
            reportPath = $resolvedPath
            generatedAtUtc = [string]$report.GeneratedAtUtc
            engineCode = [int]$engine.Engine
            engineName = Get-EngineName -EngineCode ([int]$engine.Engine)
            isAvailable = [bool]$engine.IsAvailable
            availabilityError = [string]$engine.AvailabilityError
            capabilities = [string]$engine.CapabilitiesSummary
            caseCount = [int]$report.CaseCount
            queryCount = [int]$engine.QueryCount
            hitCount = [int]$hitCount
            missCount = [int]$missCount
            recallAtK = [double]$engine.RecallAtK
            mrr = [double]$engine.Mrr
            indexingTimeMs = [double]$engine.IndexingTimeMs
            latency = $engine.SearchLatencyMs
            caseResults = $caseResults
        }
    }
}

if ($runRows.Count -eq 0) {
    throw "No report data found. Provide valid -InputReports paths."
}

$runRowsJson = $runRows | ConvertTo-Json -Depth 30 -Compress

$htmlTemplate = @'
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Settings Search Profiling Dashboard</title>
  <style>
    :root {
      --bg: #f6f8fb;
      --card: #ffffff;
      --text: #1f2937;
      --muted: #6b7280;
      --line: #dbe2ea;
      --ok: #0f766e;
      --bad: #b42318;
      --bar: #2563eb;
      --bar2: #0ea5e9;
    }
    body {
      margin: 0;
      padding: 24px;
      background: var(--bg);
      color: var(--text);
      font-family: Segoe UI, Arial, sans-serif;
    }
    h1, h2, h3 { margin: 0 0 10px 0; }
    .sub { color: var(--muted); margin-bottom: 16px; }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 14px;
      margin-bottom: 18px;
    }
    .card {
      background: var(--card);
      border: 1px solid var(--line);
      border-radius: 10px;
      padding: 14px;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }
    th, td {
      border-bottom: 1px solid var(--line);
      padding: 7px 8px;
      text-align: left;
      vertical-align: top;
    }
    th { color: var(--muted); font-weight: 600; }
    .hit { color: var(--ok); font-weight: 600; }
    .miss { color: var(--bad); font-weight: 600; }
    .bar-row { margin: 8px 0; }
    .bar-label {
      display: flex;
      justify-content: space-between;
      font-size: 12px;
      margin-bottom: 3px;
    }
    .track {
      height: 10px;
      border-radius: 7px;
      background: #e7edf4;
      overflow: hidden;
    }
    .fill {
      height: 100%;
      background: linear-gradient(90deg, var(--bar), var(--bar2));
    }
    .mono { font-family: Consolas, "Courier New", monospace; }
  </style>
</head>
<body>
  <h1>Settings Search Profiling Dashboard</h1>
  <div class="sub" id="meta"></div>

  <div class="card" style="margin-bottom: 18px;">
    <h2>Run Summary</h2>
    <table>
      <thead>
        <tr>
          <th>Run</th>
          <th>Recall@K</th>
          <th>MRR</th>
          <th>Hits</th>
          <th>Misses</th>
          <th>Indexing (ms)</th>
          <th>P50 (ms)</th>
          <th>P95 (ms)</th>
        </tr>
      </thead>
      <tbody id="summaryRows"></tbody>
    </table>
  </div>

  <div class="grid" id="metricCards"></div>

  <div class="card" style="margin-top: 18px;">
    <h2>Per-Query Comparison</h2>
    <table>
      <thead id="queryHead"></thead>
      <tbody id="queryBody"></tbody>
    </table>
  </div>

  <div class="grid" id="missCards" style="margin-top: 18px;"></div>

  <script>
    const runs = __RUN_DATA__;

    function esc(value) {
      return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;");
    }

    function pct(v) { return `${(Number(v || 0) * 100).toFixed(1)}%`; }
    function ms(v) { return Number(v || 0).toFixed(2); }

    const runLabel = (r) => `${r.engineName} (${r.sourceFile})`;

    document.getElementById("meta").textContent =
      `${runs.length} run(s) loaded. Generated from ${runs.map(r => r.sourceFile).join(", ")}`;

    const summaryHtml = runs.map(r => `
      <tr>
        <td>${esc(runLabel(r))}</td>
        <td>${pct(r.recallAtK)}</td>
        <td>${pct(r.mrr)}</td>
        <td>${r.hitCount}/${r.caseCount}</td>
        <td>${r.missCount}</td>
        <td>${ms(r.indexingTimeMs)}</td>
        <td>${ms(r.latency?.P50Ms)}</td>
        <td>${ms(r.latency?.P95Ms)}</td>
      </tr>
    `).join("");
    document.getElementById("summaryRows").innerHTML = summaryHtml;

    function metricCard(title, getValue, formatValue, widthMode) {
      const values = runs.map(getValue);
      const max = Math.max(1, ...values);
      const rows = runs.map((r, i) => {
        const value = values[i];
        const width = widthMode === "fraction" ? Math.max(0, Math.min(100, value * 100)) : Math.max(0, Math.min(100, (value / max) * 100));
        return `
          <div class="bar-row">
            <div class="bar-label">
              <span>${esc(runLabel(r))}</span>
              <span>${formatValue(value)}</span>
            </div>
            <div class="track"><div class="fill" style="width:${width.toFixed(1)}%"></div></div>
          </div>
        `;
      }).join("");

      return `<div class="card"><h3>${esc(title)}</h3>${rows}</div>`;
    }

    const metricCards = [
      metricCard("Recall@K", r => Number(r.recallAtK || 0), v => pct(v), "fraction"),
      metricCard("MRR", r => Number(r.mrr || 0), v => pct(v), "fraction"),
      metricCard("P95 Latency (ms)", r => Number(r.latency?.P95Ms || 0), v => ms(v), "relative"),
      metricCard("Indexing Time (ms)", r => Number(r.indexingTimeMs || 0), v => ms(v), "relative")
    ].join("");
    document.getElementById("metricCards").innerHTML = metricCards;

    const allQueries = new Map();
    runs.forEach((r, runIndex) => {
      (r.caseResults || []).forEach(c => {
        if (!allQueries.has(c.Query)) {
          allQueries.set(c.Query, {
            query: c.Query,
            expected: (c.ExpectedIds || []).join(", "),
            values: Array(runs.length).fill(null)
          });
        }
        allQueries.get(c.Query).values[runIndex] = c;
      });
    });

    const queryHeadHtml =
      `<tr><th>Query</th><th>Expected</th>${runs.map(r => `<th>${esc(runLabel(r))}</th>`).join("")}</tr>`;
    document.getElementById("queryHead").innerHTML = queryHeadHtml;

    const queryRows = Array.from(allQueries.values())
      .sort((a, b) => a.query.localeCompare(b.query))
      .map(row => {
        const cells = row.values.map(v => {
          if (!v) {
            return `<td class="mono">-</td>`;
          }
          const cls = v.HitAtK ? "hit" : "miss";
          const rank = Number(v.BestRank || 0);
          const top = (v.TopResultIds || []).join(", ");
          return `<td><div class="${cls}">${v.HitAtK ? "hit" : "miss"} (rank ${rank})</div><div class="mono">${esc(top || "(none)")}</div></td>`;
        }).join("");
        return `<tr><td>${esc(row.query)}</td><td class="mono">${esc(row.expected)}</td>${cells}</tr>`;
      }).join("");
    document.getElementById("queryBody").innerHTML = queryRows;

    const missCards = runs.map(r => {
      const misses = (r.caseResults || []).filter(c => !c.HitAtK);
      const rows = misses.length === 0
        ? "<div class='hit'>No misses.</div>"
        : `<table><thead><tr><th>Query</th><th>Expected</th><th>Top Results</th></tr></thead><tbody>${
            misses.map(m => `
              <tr>
                <td>${esc(m.Query)}</td>
                <td class="mono">${esc((m.ExpectedIds || []).join(", "))}</td>
                <td class="mono">${esc((m.TopResultIds || []).join(", ") || "(none)")}</td>
              </tr>
            `).join("")
          }</tbody></table>`;
      return `<div class="card"><h3>Misses: ${esc(runLabel(r))}</h3>${rows}</div>`;
    }).join("");
    document.getElementById("missCards").innerHTML = missCards;
  </script>
</body>
</html>
'@

$dashboardHtml = $htmlTemplate.Replace("__RUN_DATA__", $runRowsJson)

$outputPath = [System.IO.Path]::GetFullPath($OutputHtml)
$outputDirectory = [System.IO.Path]::GetDirectoryName($outputPath)
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

Set-Content -Path $outputPath -Value $dashboardHtml -Encoding UTF8
Write-Host "Dashboard written to: $outputPath"
