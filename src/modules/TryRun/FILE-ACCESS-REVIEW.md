# Review blocked Windows file access

This milestone covers Windows EXEs, PowerShell scripts and CMD/batch scripts.
Linux session work is paused. It uses MXC native capture in **Block** mode.

## User flow

1. Before running, enable **Capture access checks** in **Run permissions →
   Diagnostics** and choose **Block**. The initial setup's capture checkbox can
   also enable native block capture on supported hosts.
2. Run the program. An ungranted access stays denied; the program may continue,
   fail or exit. Try Run does not terminate a program merely for producing a denial.
3. After completion (or **Stop run**), select **View blocked access** / **Access
   report**. Select a native file-read denial to see the complete resource path.
4. **Review read-only access…** validates one existing local regular file and
   previews the addition. **Keep blocked** closes the preview without changing
   policy. **Apply and run again** retries after the existing result-discard check.

If the report does not contain the needed file, **Choose a file to read…** opens
a file picker. Its preview explicitly identifies this as a user-selected new
permission, not an MXC-confirmed denial. This route also works with capture off;
it does not change capture settings or infer paths from console output.

The retry uses the recorded program, arguments and permissions, adding only that
file to `readonlyPaths`. It creates a fresh workspace and recopies the recorded
input paths. File contents at those source paths may have changed since the first
run. Editing another task in configuration does not change what this retry runs;
the separate configuration draft remains intact. A grant applies to this retry
chain, not to unrelated future tasks or a global profile.

## Scope and evidence

- Raw native resource identifiers are preserved separately from the bounded list
  display. Only native **blocked file-read** records can create a proposal.
  Workload-written observations, output text, permissive capture, unknown access,
  writes and other resource types never authorize this action.
- Native capture is not a complete file-I/O trace on the tested host: denied reads
  of owned fixture files were absent even from the sealed ETL (which did contain
  other access-check events). The UI therefore offers explicit file selection as
  a fallback, and never claims that a selected file appeared in native evidence.
- A proposal is one exact file, not its folder. Existing effective grants and
  explicit denies are not overridden. Paths inside the previous run's temporary
  session cannot be added to a fresh run.
- Missing files, directories, UNC/device/mapped-network paths, streams, wildcard
  paths, traversal, ambiguous display characters, links and redirected ancestors
  are rejected. The file and ancestors are held against replacement while the
  preview and retry are active. This temporarily prevents conflicting file writes;
  it does not change a host ACL. Applications needing exclusive file handles or
  additional permissions may still fail; the action never broadens a file grant
  into a folder grant to make them succeed.
- Existing network and other permissions are retained and shown in the preview.
  Granting a read exposes that file's contents to the program under those policies.
  No DACL fallback, write permission, network permission or permissive capture is
  enabled by this workflow.
- Native reports are collected at finalization. There is no live “allow this
  blocked operation and resume it” API in this integration. Partial/unavailable
  capture remains explicitly labeled. The displayed list retains its existing
  100-record bound; omitted records cannot be authorized from truncated text.
- Successful execution and blocked access are separate facts. A denial does not
  establish malicious behavior, and missing events do not prove there were none.

## Try it

Create a harmless text file outside the selected input bundle. Right-click only
`Samples/Windows/FileAccess/read-file.ps1` or `read-file.cmd` and choose Try Run.
Supply the text file's absolute path as one argument; do not add that file to
the copied inputs or initial read-only grants. Enable native Block capture.

The first run should fail to read it. Review its file-read record if available,
or choose the file yourself, and retry with its exact read-only grant. The second
run should print its contents (the batch demo sorts the lines). For an EXE,
select Windows `sort.exe` and supply the text file's path as one argument.
The tested `findstr.exe` file-argument and CMD `type` workflows still failed with
only a single-file grant, even without a host grant lease. Extra access needed by
an application requires separate review; these failures do not trigger a wider grant.
Unsupported capture hosts can use explicit file selection but
cannot provide native denial evidence.

## Validation

The final x64 Debug build succeeded. The full suite in `file-access-full.trx`
passed 189 tests, skipped one existing border-pixel test and had no failures.
That desktop could not rasterize the test's solid-color control; pixel rendering
was not verified. The new access-list layout and window workflow checks passed.

The 21 targeted checks passed in `file-access-milestone.trx`. They cover full
native identifiers and evidence provenance, malformed paths and 1,500 mutated
report inputs, exact policy changes, file/parent replacement protection, canceled
previews and result-discard cancellation, and Windows EXE/PowerShell/batch retries
using the recorded task even after another configuration was edited.

Real MXC execution verified a denied initial read followed by an allowed selected
file read, with writes and sibling reads still denied and both originals unchanged.
That enforcement check deliberately released the host grant lease before execution.
Native-record proposal generation is covered by controlled report fixtures;
the host's missing native target-file records prevent claiming automatic end-to-end
denial recovery for those workloads. The runtime UI tests use explicit file selection.
