# Idempotently apply patches in deps/ to the deps/spdlog submodule's working tree.
#
# This exists because deps/spdlog (v1.8.5 + commit 616866fc) bundles fmt 7 whose
# format.h(357) references stdext::checked_array_iterator -- a Microsoft type
# that was deprecated in VS 2019 16.10 and removed in MSVC 14.51 (STL4043).
# Rather than mutate the upstream submodule pointer, store the fix as a
# standard unified-diff patch alongside this script and apply it on every
# build via deps/spdlog.props. The longer-term direction is to migrate
# deps/spdlog to vcpkg with the same patch as a port overlay (see PR #47910
# review feedback).
#
# This script is invoked by deps/spdlog.props' <ApplySpdlogPatches> target
# before any spdlog source is compiled. Calling it directly is also safe.
#
# Concurrency: deps/spdlog.props is imported by ~80 vcxproj files; MSBuild
# typically builds many of them in parallel, so multiple instances of this
# script can run at the same time. A named mutex serialises the
# check-and-apply step across all processes on the same machine to prevent
# the classic check-then-apply race where two processes both decide the
# patch needs applying and the second's apply fails because the first
# already patched the file.

# Note: do NOT set $ErrorActionPreference = 'Stop' here -- git apply --check
# writes diagnostics to stderr when a patch does not apply, and we use that
# to drive the idempotency check below. Errors are surfaced explicitly via
# $LASTEXITCODE checks and `throw`.

$submoduleRoot = Join-Path $PSScriptRoot 'spdlog'

# Bail cleanly if the submodule has not been initialized yet -- the build
# itself will fail with a clearer message in that case.
if (-not (Test-Path (Join-Path $submoduleRoot '.git'))) {
    Write-Host "[apply-spdlog-patches] deps/spdlog submodule not initialized; skipping."
    exit 0
}

$patches = @(
    Join-Path $PSScriptRoot 'spdlog-msvc-fix.patch'
)

# Local (not Global\) mutex name so we don't require elevation and only
# serialise within the current user session, which is the unit of work.
$mutex = New-Object System.Threading.Mutex($false, 'PowerToys-deps-spdlog-msvc-fix-patch-apply')
$mutexAcquired = $false
Push-Location $submoduleRoot
try {
    # Wait up to 5 minutes for the mutex; if it takes longer than that
    # something is wrong upstream and failing fast is better than hanging.
    $mutexAcquired = $mutex.WaitOne([System.TimeSpan]::FromMinutes(5))
    if (-not $mutexAcquired) {
        throw "[apply-spdlog-patches] timed out waiting for patch-apply mutex."
    }

    foreach ($patch in $patches) {
        if (-not (Test-Path $patch)) {
            Write-Warning "[apply-spdlog-patches] missing patch file: $patch"
            continue
        }

        # If the reverse patch applies cleanly the patch is already applied -- skip.
        # Suppress git's stderr diagnostics; we drive the decision off $LASTEXITCODE.
        # --ignore-whitespace makes context matching tolerant of CRLF vs LF differences
        # between this patch file and the submodule's checked-out files.
        & git apply --reverse --check --whitespace=nowarn --ignore-whitespace -- $patch 2>$null
        if ($LASTEXITCODE -eq 0) {
            continue
        }

        # Otherwise apply forwards. --check first so a clean failure is reported
        # without leaving the working tree partially modified.
        $checkOutput = & git apply --check --whitespace=nowarn --ignore-whitespace -- $patch 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error ($checkOutput | Out-String)
            throw "[apply-spdlog-patches] cannot apply $(Split-Path -Leaf $patch) -- has the spdlog submodule SHA changed?"
        }
        $applyOutput = & git apply --whitespace=nowarn --ignore-whitespace -- $patch 2>&1
        if ($LASTEXITCODE -ne 0) {
            # Defensive race recovery: even with the mutex, if something else
            # (e.g. another script outside this mutex's scope) raced us, the
            # patch may now be applied. Treat that as success.
            & git apply --reverse --check --whitespace=nowarn --ignore-whitespace -- $patch 2>$null
            if ($LASTEXITCODE -eq 0) {
                continue
            }
            Write-Error ($applyOutput | Out-String)
            throw "[apply-spdlog-patches] git apply of $(Split-Path -Leaf $patch) failed."
        }
        Write-Host "[apply-spdlog-patches] applied $(Split-Path -Leaf $patch)"
    }
}
finally {
    Pop-Location
    if ($mutexAcquired) {
        $mutex.ReleaseMutex()
    }
    $mutex.Dispose()
}
