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

Push-Location $submoduleRoot
try {
    foreach ($patch in $patches) {
        if (-not (Test-Path $patch)) {
            Write-Warning "[apply-spdlog-patches] missing patch file: $patch"
            continue
        }

        # If the reverse patch applies cleanly the patch is already applied -- skip.
        # Suppress git's stderr diagnostics; we drive the decision off $LASTEXITCODE.
        & git apply --reverse --check --whitespace=nowarn -- $patch 2>$null
        if ($LASTEXITCODE -eq 0) {
            continue
        }

        # Otherwise apply forwards. --check first so a clean failure is reported
        # without leaving the working tree partially modified.
        $checkOutput = & git apply --check --whitespace=nowarn -- $patch 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error ($checkOutput | Out-String)
            throw "[apply-spdlog-patches] cannot apply $(Split-Path -Leaf $patch) -- has the spdlog submodule SHA changed?"
        }
        $applyOutput = & git apply --whitespace=nowarn -- $patch 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error ($applyOutput | Out-String)
            throw "[apply-spdlog-patches] git apply of $(Split-Path -Leaf $patch) failed."
        }
        Write-Host "[apply-spdlog-patches] applied $(Split-Path -Leaf $patch)"
    }
}
finally {
    Pop-Location
}
