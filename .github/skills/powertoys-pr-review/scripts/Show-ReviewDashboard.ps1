<#
.SYNOPSIS
    Serve a single-window HTML dashboard that acts as a live status tracker while a
    batch of PRs is being reviewed, then as an approval surface once the reviews
    conclude.
.DESCRIPTION
    Reads a review-data JSON file (one entry per PR) and serves a master-detail
    dashboard on http://localhost:<Port> using System.Net.HttpListener (no Node or
    Python required). A left sidebar lists every PR with a live status dot; the
    right pane shows full detail for the selected PR.

    Launch it at the START of a batch review. The page polls /status every few
    seconds and re-reads the data file, so as the agent updates each PR's phase
    (mirroring -> building -> Copilot review round N -> drafting -> ready/held) the
    sidebar and header update live and validated public items fill in. When reviews
    conclude the user sets a per-PR action and per-item post/hold subset, then
    clicks Submit; decisions are written to a JSON file the launching Copilot
    session resumes from.

    The page never calls GitHub. It only reads status and records the human
    decision. Posting stays with the agent so the Step 10 freshness re-check and
    exact payloads are preserved. The agent is the single writer of the data file
    (write atomically: write a .tmp then Move-Item -Force) and the page is a reader.
.PARAMETER DataPath
    Path to the review-data JSON (see references/approval-dashboard.md for schema).
.PARAMETER Port
    TCP port for the local server. Default 8787.
.PARAMETER DecisionsPath
    Where to write the decisions JSON. Defaults to review-decisions.json next to
    DataPath.
.PARAMETER NoBrowser
    Do not auto-open the default browser.
.PARAMETER RepoDir
    Optional. When set, the page shows a "Launch a Copilot session to run these now"
    option; on Submit the server opens a new terminal running
    `copilot -C <RepoDir> -i "<resume phrase>"` — a supervised interactive session
    that auto-executes the resume phrase (freshness re-check + posting still require
    your approval in that window). If omitted, only the manual resume-phrase banner
    is shown.
.EXAMPLE
    ./Show-ReviewDashboard.ps1 -DataPath .\review-data.json
.NOTES
    Resume phrase shown after submit: "pr-review: actions ready".
    Stop with Ctrl+C, or Stop-Process -Id <pid> if launched detached.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $DataPath,
    [int] $Port = 8787,
    [string] $DecisionsPath,
    [string] $RepoDir,
    [switch] $NoBrowser
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReviewPayload.Common.ps1')

if (-not (Test-Path $DataPath)) { throw "Review data file not found: $DataPath" }
$DataPath = (Resolve-Path $DataPath).Path
if (-not $DecisionsPath) { $DecisionsPath = Join-Path (Split-Path -Parent $DataPath) 'review-decisions.json' }
$initialData = Read-JsonFile -Path $DataPath
$initialErrors = @(Test-ReviewDataDocument -Document $initialData -AllowIncomplete)
if ($initialErrors.Count -gt 0) { throw ($initialErrors -join [Environment]::NewLine) }
if ($RepoDir) { if (Test-Path $RepoDir) { $RepoDir = (Resolve-Path $RepoDir).Path } else { Write-Warning "RepoDir not found: $RepoDir (launch-on-submit disabled)"; $RepoDir = '' } }
$launchEnabled = [bool]$RepoDir

$resumePhrase = 'pr-review: actions ready'

# Tolerant read for /status: the agent may be mid-write. Retry briefly, return last-good on failure.
$script:lastGood = Get-Content $DataPath -Raw
function Read-DataFileTolerant {
    for ($i = 0; $i -lt 3; $i++) {
        try {
            $fs = [IO.File]::Open($DataPath, 'Open', 'Read', 'ReadWrite')
            try { $sr = [IO.StreamReader]::new($fs); $txt = $sr.ReadToEnd(); $sr.Close() } finally { $fs.Dispose() }
            $null = $txt | ConvertFrom-Json
            $script:lastGood = $txt
            return $txt
        } catch { Start-Sleep -Milliseconds 120 }
    }
    return $script:lastGood
}

function ConvertTo-ClientDataJson {
    param([Parameter(Mandatory)][string]$Text)

    $document = $Text | ConvertFrom-Json
    $document | Add-Member -NotePropertyName '_reviewDataHash' -NotePropertyValue (Get-TextSha256 -Text $Text) -Force
    return ($document | ConvertTo-Json -Depth 30 -Compress).Replace('</', '<\/')
}

$initialClientData = ConvertTo-ClientDataJson -Text $script:lastGood

$htmlTemplate = @'
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>PowerToys PR Review</title>
<style>
  :root { color-scheme: light dark; }
  * { box-sizing: border-box; }
  html,body { height:100%; }
  body { font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif; margin:0; background:#0d1117; color:#e6edf3; display:flex; flex-direction:column; }
  header { flex:0 0 auto; background:#161b22; border-bottom:1px solid #30363d; padding:10px 18px; display:flex; align-items:center; gap:14px; flex-wrap:wrap; }
  header h1 { font-size:15px; margin:0; font-weight:600; }
  header .meta { color:#8b949e; font-size:12px; }
  header .spacer { flex:1; }
  #phase { font-size:12.5px; color:#c9d1d9; }
  .counts { font-size:12px; color:#8b949e; }
  button { font:inherit; cursor:pointer; border-radius:6px; border:1px solid #30363d; background:#21262d; color:#e6edf3; padding:6px 12px; }
  button.primary { background:#238636; border-color:#2ea043; color:#fff; font-weight:600; }
  button.primary:disabled { opacity:.5; cursor:not-allowed; }
  button.ready { animation:glow 1.6s infinite; }
  button:hover:not(:disabled) { background:#2ea043; }
  .layout { flex:1 1 auto; display:flex; min-height:0; }
  aside { flex:0 0 300px; border-right:1px solid #21262d; overflow:auto; background:#0f141b; }
  .pritem { padding:11px 14px; border-bottom:1px solid #161b22; cursor:pointer; display:flex; gap:9px; align-items:flex-start; }
  .pritem:hover { background:#161b22; }
  .pritem.active { background:#132a17; border-left:3px solid #2ea043; padding-left:11px; }
  .dot { flex:0 0 auto; width:9px; height:9px; border-radius:50%; margin-top:5px; background:#30363d; }
  .dot.ready { background:#2ea043; }
  .dot.progress { background:#d29922; animation:pulse 1.4s infinite; }
  .dot.held { background:#388bfd; }
  .dot.error { background:#f85149; }
  .dot.queued { background:#30363d; }
  @keyframes pulse { 0%{opacity:.35} 50%{opacity:1} 100%{opacity:.35} }
  @keyframes glow { 0%{box-shadow:0 0 0 0 rgba(46,160,67,.5)} 70%{box-shadow:0 0 0 6px rgba(46,160,67,0)} 100%{box-shadow:0 0 0 0 rgba(46,160,67,0)} }
  .pritem .n { font-weight:600; font-size:13px; }
  .pritem .chk { color:#2ea043; font-weight:700; }
  .pritem .t { font-size:12.5px; color:#c9d1d9; line-height:1.3; margin:2px 0; overflow:hidden; text-overflow:ellipsis; display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; }
  .pritem .m { font-size:11px; color:#8b949e; }
  .pritem .wait { font-size:11px; color:#e3b341; margin-top:2px; }
  main { flex:1 1 auto; overflow:auto; padding:18px 22px; }
  .empty { color:#8b949e; margin-top:40px; text-align:center; }
  a { color:#58a6ff; text-decoration:none; } a:hover { text-decoration:underline; }
  .title { font-size:16px; font-weight:600; }
  .sub { color:#8b949e; font-size:12.5px; margin-top:4px; }
  .disp-line { color:#8b949e; font-size:12.5px; margin:3px 0 0; }
  .statusbar { margin-top:12px; padding:9px 12px; border-radius:8px; font-size:13px; border:1px solid #6b551a; background:#241d09; color:#f2cc60; }
  .statusbar.ready { border-color:#2b4a34; background:#0e1a12; color:#7ee787; }
  .statusbar.held { border-color:#1a3a5f; background:#0d1a2a; color:#79c0ff; }
  .badge { display:inline-block; padding:2px 8px; border-radius:999px; font-size:11px; font-weight:600; vertical-align:middle; }
  .b-assoc { background:#1f2937; color:#9ecbff; border:1px solid #274060; }
  .b-member { background:#132a17; color:#7ee787; border:1px solid #2b5a34; }
  .b-community { background:#3a2a0b; color:#f0b24b; border:1px solid #6b4d1a; }
  .b-draft { background:#26262b; color:#c9c9d1; border:1px solid #45454d; text-transform:uppercase; letter-spacing:.5px; }
  .b-first { background:#3b2f0b; color:#f2cc60; border:1px solid #6b551a; }
  .b-status { background:#22303c; color:#7ee787; border:1px solid #2b4a34; }
  .b-disp { background:#2d2233; color:#e2a0ff; border:1px solid #4a2d55; }
  .pills { margin-top:8px; display:flex; gap:6px; flex-wrap:wrap; align-items:center; }
  .pills .badge { font-size:11.5px; padding:3px 10px; }
  .seg { display:inline-flex; border:1px solid #30363d; border-radius:8px; overflow:hidden; }
  .seg button { background:#0d1117; color:#8b949e; border:0; border-right:1px solid #30363d; padding:6px 12px; font:inherit; font-size:12.5px; cursor:pointer; }
  .seg button:last-child { border-right:0; }
  .seg button:hover { background:#161b22; color:#e6edf3; }
  .seg button.active { background:#1f6feb; color:#fff; font-weight:600; }
  #ctxwrap { transition:opacity .15s; }
  .sev { text-transform:uppercase; letter-spacing:.4px; }
  .sev-critical { background:#3d1418; color:#ff9a9a; border:1px solid #6e2831; }
  .sev-high     { background:#3a1d12; color:#ffb08a; border:1px solid #6b3a24; }
  .sev-medium   { background:#3a3312; color:#f2cc60; border:1px solid #6b5f1a; }
  .sev-low      { background:#12303a; color:#7ad9f2; border:1px solid #1a4d5f; }
  .section-title { font-size:12px; text-transform:uppercase; letter-spacing:.6px; color:#8b949e; margin:16px 0 6px; }
  .ctx { background:#0d1117; border:1px solid #21262d; border-radius:8px; padding:10px 12px; white-space:pre-wrap; font-size:13px; }
  textarea { width:100%; background:#0d1117; color:#e6edf3; border:1px solid #30363d; border-radius:8px; padding:10px; font:inherit; font-size:13px; resize:vertical; }
  .sug { border:1px solid #21262d; border-radius:8px; margin:8px 0; overflow:hidden; }
  .sug-head { display:flex; gap:10px; align-items:center; padding:9px 12px; cursor:pointer; background:#0f141b; }
  .sug-head:hover { background:#131a23; }
  .sug-head .caret { color:#8b949e; font-size:10px; width:12px; flex:0 0 auto; transition:transform .12s ease; }
  .sug.open .sug-head .caret { transform:rotate(90deg); }
  .sug.open .sug-head { background:#131a23; border-bottom:1px solid #21262d; }
  .sug-head .grow { flex:1; font-weight:600; }
  .sug-head .file { color:#8b949e; font-size:12px; font-family:ui-monospace,Consolas,monospace; }
  .sug-body { display:none; padding:12px 14px; }
  .sug.open .sug-body { display:block; }
  .sug-body pre { background:#0d1117; border:1px solid #21262d; border-radius:6px; padding:10px; overflow:auto; font-family:ui-monospace,Consolas,monospace; font-size:12.5px; white-space:pre-wrap; }
  .sug-body pre.suggestion { border-color:#2b4a34; background:#0e1a12; }
  .sug-meta { display:flex; flex-wrap:wrap; gap:6px 18px; align-items:center; margin-bottom:10px; }
  .sug-meta > span { font-size:12.5px; color:#c9d1d9; }
  .sug-meta .k { color:#8b949e; margin-right:6px; text-transform:uppercase; letter-spacing:.4px; font-size:10.5px; }
  .sug-field { margin-top:12px; }
  .sug-field .lbl { font-size:10.5px; text-transform:uppercase; letter-spacing:.5px; color:#8b949e; margin-bottom:4px; }
  .sug-field .val { font-size:13px; line-height:1.5; }
  .sug-links { display:flex; gap:16px; flex-wrap:wrap; margin-top:14px; padding-top:10px; border-top:1px dashed #21262d; }
  .sug-links a { color:#58a6ff; font-size:12.5px; text-decoration:none; }
  .sug-links a:hover { text-decoration:underline; }
  .expander { font-size:11.5px; color:#58a6ff; cursor:pointer; user-select:none; margin-left:8px; font-weight:400; text-transform:none; letter-spacing:0; }
  .expander:hover { text-decoration:underline; }
  .actionrow { display:flex; gap:10px; align-items:center; flex-wrap:wrap; margin-top:10px; }
  select { font:inherit; background:#0d1117; color:#e6edf3; border:1px solid #30363d; border-radius:6px; padding:6px 8px; }
  label.chk { font-size:12.5px; color:#c9d1d9; display:inline-flex; gap:6px; align-items:center; }
  .toggle { color:#58a6ff; font-size:12px; cursor:pointer; user-select:none; }
  .muted { color:#8b949e; }
  .navbtns { display:flex; gap:8px; margin-top:22px; }
  #banner { flex:0 0 auto; display:none; background:#132a17; border-top:1px solid #2ea043; padding:13px 18px; }
  #banner.show { display:block; }
  #banner code { background:#0d1117; padding:2px 8px; border-radius:6px; border:1px solid #30363d; color:#7ee787; }
</style>
</head>
<body>
<header>
  <h1>PowerToys PR Review</h1>
  <span class="meta" id="genmeta"></span>
  <span id="phase"></span>
  <span class="spacer"></span>
  <span class="counts" id="counts"></span>
  <label class="chk" id="launchWrap" style="display:none;margin-right:6px"><input type="checkbox" id="launchChk" checked> Launch Copilot on submit</label>
  <button class="primary" id="submitBtn">Submit decisions</button>
</header>
<div class="layout">
  <aside id="sidebar"></aside>
  <main id="detail"><div class="empty">Waiting for the first PR…</div></main>
</div>
<div id="banner">
  <span id="bannerNote"><strong>Decisions saved.</strong> Return to your Copilot session and type:</span>
  <code id="resume"></code>
  <span class="muted"> — the agent will re-check each PR for new activity, then post only what you approved.</span>
</div>
<script>
let DATA = /*__REVIEW_DATA__*/;
const RESUME = "/*__RESUME__*/";
const LAUNCH_ENABLED = /*__LAUNCH__*/;
const state = {};
let current = null;

function esc(s){ return (s==null?'':String(s)).replace(/[&<>]/g, c=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[c])); }
function fmtBody(b){
  let html = esc(b||'');
  html = html.replace(/```suggestion([\s\S]*?)```/g, (m,code)=>'<pre class="suggestion">'+esc(code.trim())+'</pre>');
  html = html.replace(/```([\s\S]*?)```/g, (m,code)=>'<pre>'+esc(code.trim())+'</pre>');
  return html;
}
function payloadOf(pr){ return pr.publicPayload||{contextBody:'',items:[]}; }
function itemsOf(pr){ return payloadOf(pr).items||[]; }
function evidenceOf(pr){ return pr.internalEvidence||{}; }
function defaultItem(){ return 'post'; }
function prByNum(n){ return (DATA.prs||[]).find(p=>p.number==n); }
function phaseOf(pr){
  let p = (pr.phase||'').toLowerCase();
  if(!p){ p='queued'; }
  if(['ready','held','error','queued'].includes(p)) return p;
  return 'progress';
}
function statusText(pr){
  const bits=[]; const ph=(pr.phase||'').toLowerCase();
  if(ph && !['ready','held','queued'].includes(ph)) bits.push(ph);
  if(pr.loop) bits.push('round '+pr.loop);
  if(pr.waitingOn) bits.push('waiting on '+pr.waitingOn);
  return bits.join(' · ');
}
function sevCounts(pr){
  const c={critical:0,high:0,medium:0,low:0};
  itemsOf(pr).forEach(s=>{ const k=(s.severity||'low').toLowerCase(); if(k in c) c[k]++; });
  const parts=[]; if(c.critical)parts.push(c.critical+'C'); if(c.high)parts.push(c.high+'H'); if(c.medium)parts.push(c.medium+'M'); if(c.low)parts.push(c.low+'L');
  return parts.join(' ');
}
function ensureState(pr){
  let st = state[pr.number];
  if(!st){ st = state[pr.number] = { action:'comment', postContext:true, contextBody:(payloadOf(pr).contextBody||''), instructions:'', items:{}, open:{}, reviewed:false, edited:false }; }
  if(!st.open) st.open = {};
  itemsOf(pr).forEach(s=>{
    if(!(s.id in st.items)) st.items[s.id]= defaultItem();
    if(!(s.id in st.open)) st.open[s.id]= ['critical','high'].includes((s.severity||'').toLowerCase());
  });
  if(!st.edited && (!st.contextBody)) st.contextBody = payloadOf(pr).contextBody||'';
  return st;
}
function renderSidebar(){
  const el = document.getElementById('sidebar'); el.innerHTML='';
  (DATA.prs||[]).forEach(pr=>{
    const st = ensureState(pr); const ph = phaseOf(pr);
    const div = document.createElement('div');
    div.className = 'pritem'+(current==pr.number?' active':'');
    div.onclick = ()=> selectPR(pr.number);
    const sc = sevCounts(pr); const nsug=itemsOf(pr).length;
    const stx = statusText(pr);
    div.innerHTML =
      '<span class="dot '+ph+'"></span>'
      + '<div style="min-width:0">'
      +   '<div class="n">#'+esc(pr.number)+(pr.draft?' <span class="badge b-draft">draft</span>':'')+(pr.firstTimer?' <span class="badge b-community">first-time</span>':'')+(st.edited?' <span class="chk">✓</span>':'')+'</div>'
      +   '<div class="t">'+esc(pr.title)+'</div>'
      +   '<div class="m">'+esc(pr.author||'')+' · '+nsug+' item'+(nsug===1?'':'s')+(sc?(' · '+sc):'')+'</div>'
      +   (ph==='progress'&&stx?('<div class="wait">'+esc(stx)+'</div>'):'')
      + '</div>';
    el.appendChild(div);
  });
}
function selectPR(n){ current = n; renderSidebar(); renderDetail(n); document.getElementById('detail').scrollTop = 0; }
function markEdited(n){ state[n].edited = true; renderSidebar(); }
function renderDetail(n){
  const pr = prByNum(n); if(!pr){ return; } const st = ensureState(pr); const ph = phaseOf(pr);
  const canDecide = ph==='ready';
  const ai = assocInfo(pr.assoc, pr.firstTimer);
  const assoc = '<span class="badge '+ai.cls+'">'+esc(ai.label)+'</span>';
  const draftB = pr.draft ? '<span class="badge b-draft">Draft</span>' : '<span class="badge b-status">Ready for review</span>';
  const firstB = '';
  const evidence = evidenceOf(pr);
  const status = evidence.status ? '<span class="badge b-status">'+esc(evidence.status)+'</span>' : '';
  const disp = pr.disposition ? '<span class="badge b-disp">'+esc(pr.disposition)+'</span>' : '';
  const exePath = evidence.exePath ? evidence.exePath : (evidence.worktree ? (evidence.worktree.replace(/[\\/]+$/,'') + '\\x64\\Debug\\PowerToys.exe') : '');

  let statusHtml='';
  if(ph==='progress'){ statusHtml = '<div class="statusbar">Review in progress — '+esc(statusText(pr)||'working…')+'</div>'; }
  else if(ph==='ready'){ statusHtml = '<div class="statusbar ready">Review complete — set your decision below.</div>'; }
  else if(ph==='held'){ statusHtml = '<div class="statusbar held">Held — '+esc(pr.disposition||'no action drafted')+'</div>'; }
  else if(ph==='queued'){ statusHtml = '<div class="statusbar">Queued — not started yet.</div>'; }

  let sugHtml='';
  itemsOf(pr).forEach(s=>{
    const sev=(s.severity||'low').toLowerCase();
    const checked = st.items[s.id]==='post' ? 'checked' : '';
    const isOpen = !!st.open[s.id];
    const fileTxt = esc(s.path||'')+(s.line?(':'+s.line):'');
    const meta = '<div class="sug-meta">'
      + '<span><span class="k">kind</span>'+esc(s.kind||'')+'</span>'
      + (s.path?('<span><span class="k">location</span><span class="file">'+fileTxt+'</span></span>'):'')
      + '</div>';
    const links = '<div class="sug-links">'
      + (s.path?('<a class="codelink" target="_blank" rel="noopener" data-prurl="'+esc(pr.url||'')+'" data-path="'+esc(s.path||'')+'" data-line="'+esc(s.line||'')+'" href="'+esc((pr.url||'')+(pr.url?'/files':''))+'">View in PR diff \u2197</a>'):'')
      + '</div>';
    const bodyInner = meta
      + '<div class="sug-field"><div class="lbl">Public payload</div><div class="val">'+fmtBody(s.body)+'</div></div>'
      + links;
    sugHtml += '<div class="sug'+(isOpen?' open':'')+'" id="sug-'+esc(s.id)+'">'
      + '<div class="sug-head" onclick="toggleSug('+n+',\''+esc(s.id)+'\')">'
      + '<span class="caret">\u25B6</span>'
      + '<span class="badge sev sev-'+sev+'">'+esc(sev)+'</span>'
      + '<span class="grow">'+esc(s.title)+'</span>'
      + '<span class="file">'+fileTxt+'</span>'
      + (canDecide?('<label class="chk" onclick="event.stopPropagation()"><input type="checkbox" '+checked+' onchange="setSug('+n+',\''+esc(s.id)+'\',this.checked)"> post</label>'):'')
      + '</div><div class="sug-body">'+bodyInner+'</div></div>';
  });

  const opt = v => (st.action===v?' selected':'');
  const decisionControls = canDecide
    ? '<div class="actionrow"><span class="section-title" style="margin:0">Action</span>'
      + '<select onchange="setAction('+n+',this.value)">'
      + '<option value="comment"'+opt('comment')+'>Post checked items as a review</option>'
      + '<option value="request-changes"'+opt('request-changes')+'>Post as "Request changes" review</option>'
      + '<option value="hold"'+opt('hold')+'>Hold all — post nothing now</option>'
      + '<option value="close"'+opt('close')+'>Close / redirect (maintainer)</option>'
      + '<option value="custom"'+opt('custom')+'>Custom (use instructions)</option>'
      + '</select>'
      + '<span class="seg" id="ctxseg" title="Some reviewers prefer inline comments only, with no overall summary message">'
      + '<button type="button" class="'+(st.postContext?'active':'')+'" onclick="setCtxBtn('+n+',true)">Include overall comment</button>'
      + '<button type="button" class="'+(!st.postContext?'active':'')+'" onclick="setCtxBtn('+n+',false)">Skip \u2014 inline only</button>'
      + '</span></div>'
      + '<div class="section-title">Instructions / next steps for the agent (optional)</div>'
      + '<textarea rows="2" placeholder="e.g. also ask for a demo · hold the low-severity ones · rebase first, then give me local build + e2e steps" oninput="setIns('+n+',this.value)">'+esc(st.instructions)+'</textarea>'
    : '<div class="section-title muted">No publishing controls: this PR is '+esc(ph)+'.</div>';
  document.getElementById('detail').innerHTML =
    '<div class="title"><a href="'+esc(pr.url)+'" target="_blank">#'+esc(pr.number)+'</a> · '+esc(pr.title)+'</div>'
    + '<div class="pills">'+assoc+' '+draftB+(pr.assoc?(' <span class="badge b-assoc">'+esc(pr.assoc)+'</span>'):'')+'</div>'
    + '<div class="sub">'+esc(pr.author||'')+' '+status+' '+disp+'</div>'
    + (pr.disposition?('<div class="disp-line">Disposition: '+esc(pr.disposition)+'</div>'):'')
    + (pr.context?('<div class="disp-line">'+esc(pr.context)+'</div>'):'')
    + statusHtml
    + (pr.phase0Note?('<div class="section-title">Context / process (Phase 0)</div><div class="ctx">'+esc(pr.phase0Note)+'</div>'):'')
    + (payloadOf(pr).contextBody?('<div id="ctxwrap"'+(st.postContext?'':' style="opacity:0.4"')+'><div class="section-title">Drafted overall comment to author '+(canDecide?'<span class="toggle" id="edtog" onclick="ed()">edit</span>':'')+'</div><div class="ctx" id="ctxbox">'+esc(st.contextBody)+'</div></div>'):'')
    + (itemsOf(pr).length?('<div class="section-title">Public review items ('+itemsOf(pr).length+')<span class="expander" onclick="expandAll('+n+',true)">expand all</span><span class="expander" onclick="expandAll('+n+',false)">collapse all</span></div>'+sugHtml):'<div class="section-title muted">No public review items</div>')
    + ((exePath||pr.testInstructions)?('<div class="section-title">Run &amp; verify (already built)</div><div class="ctx">'+(exePath?('<strong>Launch:</strong> <span class="file">'+esc(exePath)+'</span>\n\n'):'')+esc(pr.testInstructions||'')+'</div>'):'')
    + decisionControls
    + '<div class="navbtns"><button onclick="nav(-1)">&larr; Prev</button><button onclick="nav(1)">Next &rarr;</button></div>';
  state[n]._sig = JSON.stringify(pr);
  applyCodeLinks();
}
function toggleSug(n,id){
  const st = state[n]; if(!st) return;
  if(!st.open) st.open = {};
  st.open[id] = !st.open[id];
  const el = document.getElementById('sug-'+id);
  if(el) el.classList.toggle('open', !!st.open[id]);
}
function expandAll(n,open){
  const st=state[n], pr=prByNum(n); if(!st||!pr) return;
  if(!st.open) st.open = {};
  itemsOf(pr).forEach(s=>{ st.open[s.id]=open; });
  document.querySelectorAll('#detail .sug').forEach(el=> el.classList.toggle('open', open));
}
async function sha256hex(str){
  const buf = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(str));
  return Array.from(new Uint8Array(buf)).map(b=>b.toString(16).padStart(2,'0')).join('');
}
async function applyCodeLinks(){
  if(!(window.crypto && crypto.subtle)) return;
  const links = document.querySelectorAll('#detail a.codelink[data-path]');
  for(const a of links){
    const prurl=a.getAttribute('data-prurl'), path=a.getAttribute('data-path'), line=a.getAttribute('data-line');
    if(!prurl || !path) continue;
    try{ const h=await sha256hex(path); a.href = prurl.replace(/\/+$/,'') + '/files#diff-'+h+(line?('R'+line):''); }catch(e){}
  }
}
function ed(){ const box=document.getElementById('ctxbox'); const t=document.getElementById('edtog'); const on=box.contentEditable!=='true'; box.contentEditable=on; box.style.outline=on?'1px solid #2ea043':''; t.textContent=on?'done':'edit'; if(on){ box.focus(); } state[current].contextBody=box.innerText; box.oninput=()=>{ state[current].contextBody=box.innerText; markEdited(current); }; }
function nav(d){ const arr=(DATA.prs||[]).map(p=>p.number); let i=arr.indexOf(current)+d; if(i<0)i=0; if(i>=arr.length)i=arr.length-1; selectPR(arr[i]); }
function setSug(n,id,v){ state[n].items[id]= v?'post':'hold'; markEdited(n); updateHeader(); }
function setAction(n,v){ state[n].action=v; markEdited(n); }
function setCtx(n,v){ state[n].postContext=v; markEdited(n); }
function setCtxBtn(n,v){
  state[n].postContext=v; markEdited(n);
  const seg=document.getElementById('ctxseg');
  if(seg){ const b=seg.querySelectorAll('button'); if(b.length===2){ b[0].classList.toggle('active',v); b[1].classList.toggle('active',!v); } }
  const wrap=document.getElementById('ctxwrap'); if(wrap) wrap.style.opacity = v?'':'0.4';
}
function assocInfo(a, firstTimer){
  a=(a||'').toUpperCase();
  if(['MEMBER','OWNER','COLLABORATOR'].includes(a)) return { label:'Member', cls:'b-member' };
  if(a==='FIRST_TIME_CONTRIBUTOR' || a==='FIRST_TIMER' || firstTimer) return { label:'Community \u00b7 first-time', cls:'b-community' };
  return { label:'Community', cls:'b-community' };
}
function setIns(n,v){ state[n].instructions=v; markEdited(n); }
function updateHeader(){
  const prs = DATA.prs||[]; let post=0,hold=0; const tally={ready:0,progress:0,held:0,queued:0,error:0};
  prs.forEach(pr=>{ tally[phaseOf(pr)]++; Object.values(state[pr.number]?state[pr.number].items:{}).forEach(d=> d==='post'?post++:hold++); });
  const active = tally.progress+tally.queued;
  const phaseEl = document.getElementById('phase');
  if(active>0){ phaseEl.textContent = '● reviewing — '+tally.ready+' ready · '+tally.progress+' in progress · '+tally.queued+' queued'+(tally.held?(' · '+tally.held+' held'):''); phaseEl.style.color='#e3b341'; }
  else { phaseEl.textContent = '✓ all reviews complete — set your decisions'; phaseEl.style.color='#7ee787'; }
  document.getElementById('counts').textContent = prs.length+' PRs · '+post+' to post · '+hold+' held';
  const btn = document.getElementById('submitBtn');
  if(active>0){ btn.classList.remove('ready'); } else { btn.classList.add('ready'); }
}
async function poll(){
  try{
    const r = await fetch('/status', {cache:'no-store'}); if(!r.ok) return;
    const nd = await r.json(); if(!nd || !nd.prs) return;
    DATA.generatedAt = nd.generatedAt || DATA.generatedAt; DATA.phase = nd.phase; DATA._reviewDataHash = nd._reviewDataHash;
    DATA.prs = nd.prs;
    (DATA.prs||[]).forEach(pr=> ensureState(pr));
    const first = current==null && DATA.prs.length;
    renderSidebar(); updateHeader();
    if(first){ selectPR(DATA.prs[0].number); }
    else if(current!=null){
      const st = state[current]; const editingBox = document.activeElement && document.activeElement.id==='ctxbox';
      const pr = prByNum(current); const sig = pr ? JSON.stringify(pr) : '';
      if(!st.edited && !editingBox && sig !== st._sig) renderDetail(current);
    }
  }catch(e){ /* transient (file mid-write); keep last view */ }
}
document.getElementById('submitBtn').onclick = async ()=>{
  const notReady = (DATA.prs||[]).filter(p=>phaseOf(p)==='progress'||phaseOf(p)==='queued').map(p=>'#'+p.number);
  if(notReady.length){ alert('Cannot submit while reviews are still in progress: '+notReady.join(', ')); return; }
  const launch = LAUNCH_ENABLED && !!(document.getElementById('launchChk') && document.getElementById('launchChk').checked);
  const out = { schemaVersion:2, reviewDataHash:DATA._reviewDataHash, submittedAt:new Date().toISOString(), launch:launch, prs:[] };
  (DATA.prs||[]).filter(pr=>phaseOf(pr)==='ready').forEach(pr=>{ const s=state[pr.number]; out.prs.push({ number:pr.number, headSha:pr.headSha, action:s.action, postContext:s.postContext, contextBody:s.contextBody, items:s.items, instructions:s.instructions||'', edited:s.edited }); });
  try {
    const r = await fetch('/submit', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(out,null,2) });
    const j = await r.json();
    if(!r.ok){ alert('Decisions were not saved:\n'+((j.errors||[j.error||'validation failed']).join('\n'))); return; }
    document.getElementById('resume').textContent = j.resume || RESUME;
    const note = document.getElementById('bannerNote');
    if(j.launched){ note.innerHTML = '<strong>A Copilot session was launched in a new terminal</strong> to run these decisions — watch and approve there. As a fallback you can also type the phrase in any existing session.'; }
    else { note.innerHTML = '<strong>Decisions saved.</strong> Return to your Copilot session and type the phrase above — the agent re-checks each PR for new activity, then posts only what you approved.'; }
    document.getElementById('banner').classList.add('show');
  } catch(e){ alert('Could not reach the local server: '+e); }
};
if(LAUNCH_ENABLED){ const lw=document.getElementById('launchWrap'); if(lw) lw.style.display=''; }
document.getElementById('genmeta').textContent = DATA.generatedAt ? ('generated '+DATA.generatedAt) : '';
document.getElementById('resume').textContent = RESUME;
(DATA.prs||[]).forEach(pr=> ensureState(pr));
renderSidebar(); updateHeader();
if((DATA.prs||[]).length) selectPR(DATA.prs[0].number);
setInterval(poll, 2500);
</script>
</body>
</html>
'@

$html = $htmlTemplate.Replace('/*__REVIEW_DATA__*/', $initialClientData).Replace('/*__RESUME__*/', $resumePhrase).Replace('/*__LAUNCH__*/', $(if($launchEnabled){'true'}else{'false'}))

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://localhost:$Port/")
try { $listener.Start() }
catch { throw "Could not start server on port $Port ($($_.Exception.Message)). Try another port with -Port." }

Write-Host "Review dashboard serving at http://localhost:$Port/  (PID $PID)" -ForegroundColor Green
Write-Host "Live status is read from: $DataPath" -ForegroundColor Cyan
Write-Host "Decisions will be written to: $DecisionsPath" -ForegroundColor Cyan
Write-Host "After you Submit in the browser, return to Copilot and type: '$resumePhrase'" -ForegroundColor Yellow
if ($launchEnabled) { Write-Host "Launch-on-submit ENABLED (repo: $RepoDir) — Submit can open a supervised Copilot session for you." -ForegroundColor Yellow }
Write-Host "Stop with Ctrl+C (or Stop-Process -Id $PID if detached)." -ForegroundColor DarkGray

if (-not $NoBrowser) { Start-Process "http://localhost:$Port/" | Out-Null }

$htmlBytes = [Text.Encoding]::UTF8.GetBytes($html)
while ($listener.IsListening) {
    $ctx = $listener.GetContext()
    $req = $ctx.Request; $res = $ctx.Response
    try {
        switch ($req.Url.AbsolutePath) {
            '/' {
                $res.ContentType = 'text/html; charset=utf-8'
                $res.ContentLength64 = $htmlBytes.Length
                $res.OutputStream.Write($htmlBytes, 0, $htmlBytes.Length)
            }
            '/status' {
                $txt = Read-DataFileTolerant
                $clientJson = ConvertTo-ClientDataJson -Text $txt
                $b = [Text.Encoding]::UTF8.GetBytes($clientJson)
                $res.ContentType = 'application/json; charset=utf-8'
                $res.Headers.Add('Cache-Control', 'no-store')
                $res.OutputStream.Write($b, 0, $b.Length)
            }
            '/health' {
                $b = [Text.Encoding]::UTF8.GetBytes('{"ok":true}')
                $res.ContentType = 'application/json'; $res.OutputStream.Write($b, 0, $b.Length)
            }
            '/submit' {
                $reader = [IO.StreamReader]::new($req.InputStream, $req.ContentEncoding)
                $body = $reader.ReadToEnd(); $reader.Close()
                $validationErrors = [System.Collections.Generic.List[string]]::new()
                try {
                    $decisions = $body | ConvertFrom-Json
                    $latestText = Read-DataFileTolerant
                    $latestData = $latestText | ConvertFrom-Json
                    $latestHash = Get-TextSha256 -Text $latestText
                    foreach ($errorMessage in Test-ReviewDataDocument -Document $latestData -CheckGitHub) {
                        $validationErrors.Add($errorMessage)
                    }
                    foreach ($errorMessage in Test-ReviewDecisionDocument -Decisions $decisions -ReviewData $latestData -ExpectedHash $latestHash) {
                        $validationErrors.Add($errorMessage)
                    }
                }
                catch {
                    $validationErrors.Add($_.Exception.Message)
                }

                if ($validationErrors.Count -gt 0) {
                    $res.StatusCode = 400
                    $resp = @{ ok = $false; errors = $validationErrors.ToArray() } | ConvertTo-Json -Compress
                }
                else {
                    $temporaryDecisionsPath = "$DecisionsPath.tmp"
                    $body | Set-Content -LiteralPath $temporaryDecisionsPath -Encoding utf8
                    Move-Item -LiteralPath $temporaryDecisionsPath -Destination $DecisionsPath -Force
                    "submitted $(Get-Date -Format o)" | Set-Content -LiteralPath "$DecisionsPath.submitted" -Encoding utf8
                    Write-Host "Validated decisions received and written to $DecisionsPath" -ForegroundColor Green
                    $launched = $false
                    $wantLaunch = [bool]$decisions.launch
                    if ($wantLaunch -and $launchEnabled) {
                        try {
                            $promptText = "$resumePhrase  (decisions file: $DecisionsPath)"
                            $launchCmd  = "copilot -C '$RepoDir' -i '$promptText'"
                            Start-Process -FilePath 'pwsh' -ArgumentList '-NoLogo','-NoExit','-Command',$launchCmd | Out-Null
                            $launched = $true
                            Write-Host "Launched supervised Copilot session in a new terminal (repo: $RepoDir)" -ForegroundColor Green
                        }
                        catch {
                            Write-Host "Launch-on-submit failed: $($_.Exception.Message)" -ForegroundColor Red
                        }
                    }
                    $resp = @{ ok = $true; resume = $resumePhrase; decisionsPath = $DecisionsPath; launched = $launched } | ConvertTo-Json -Compress
                }

                $b = [Text.Encoding]::UTF8.GetBytes($resp)
                $res.ContentType = 'application/json'; $res.OutputStream.Write($b, 0, $b.Length)
            }
            default {
                $res.StatusCode = 404
                $b = [Text.Encoding]::UTF8.GetBytes('not found'); $res.OutputStream.Write($b, 0, $b.Length)
            }
        }
    }
    catch { Write-Warning "Request error: $($_.Exception.Message)" }
    finally { $res.OutputStream.Close() }
}
