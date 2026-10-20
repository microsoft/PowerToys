# PowerScripts threat models — Microsoft Threat Modeling Tool worksheet

This worksheet converts the two PowerScripts security proposals into diagrams for the Microsoft
Threat Modeling Tool (TMT). Create **two separate models**, one for each proposal:

- `PowerScripts-Security-Proposal-A.tm7`
- `PowerScripts-Security-Proposal-B.tm7`

Use the **SDL TM Knowledge Base (Core)** template for both models. The tool's generated STRIDE
threats and HTML reports are part of the security-review evidence; do not submit the Mermaid
diagrams in the proposal documents as a replacement for the `.tm7` models.

## 1. Common TMT conventions

Use these element types from the stencil:

| TMT element | PowerScripts element |
| --- | --- |
| External interactor | User, PowerToys consumer module |
| Process | `PowerScripts.Host.exe`, script process, restricted runtime |
| Data store | Scripts folder, managed secure space, per-run workspace |
| Data flow | Requests, package/metadata reads, input files, results |
| Trust boundary | User/PowerToys boundary, Host/script boundary, isolation boundary |

For every element, set a meaningful name and leave **Out of scope** unchecked unless this worksheet
explicitly says otherwise. For every flow, use a descriptive label and select the closest available
data-flow type. The exact transport is less important than showing direction and crossing points:
the Host CLI is a process boundary, not a trust boundary by itself.

For each generated threat, record the design decision in the TMT threat properties:

- **Mitigated** — the proposal contains an enforceable control and the implementation/test evidence
  exists.
- **Needs investigation** — the proposal requires an implementation decision or test before it can
  be considered closed.
- **Accepted risk** — the proposal intentionally does not attempt to prevent the behavior.

Do not mark a threat mitigated merely because a path is hidden from the diagram. A control must be
enforced by the process token, filesystem boundary, broker, container, network policy, or output
validation.

## 2. Proposal A model — same-user, non-elevated execution

### 2.1 Diagram elements

Create these elements:

| Name | TMT type | Notes |
| --- | --- | --- |
| User | External interactor | Installs/removes packages and invokes PowerToys features. |
| PowerToys consumer module | External interactor | Keyboard Manager, Explorer context menu, or another module. |
| User-controlled scripts folder | Data store | User-selected location containing the script and descriptor/header. |
| `PowerScripts.Host.exe` | Process | Discovers, validates, normalizes, and launches scripts. |
| Script process | Process | Runs with the current interactive user's non-elevated token; not a sandbox. |
| Current-user resources | Data store | User files, devices, processes, and network reachable by that user. |
| Host/consumer API boundary | Trust boundary | Separates module callers from the Host contract. |
| Host/script privilege boundary | Trust boundary | Shows the required non-elevated token enforcement. |

### 2.2 Data flows

Draw and label the following directed flows:

| From | To | Label |
| --- | --- | --- |
| User | User-controlled scripts folder | Install or remove script package |
| PowerToys consumer module | `PowerScripts.Host.exe` | `list`, `run`, or `transform` request |
| `PowerScripts.Host.exe` | User-controlled scripts folder | Read script and descriptor metadata |
| User-controlled scripts folder | `PowerScripts.Host.exe` | Script package and I/O contract |
| `PowerScripts.Host.exe` | Script process | Launch using current interactive user's non-elevated token; no UAC |
| PowerToys consumer module | `PowerScripts.Host.exe` | Selected files and parameters |
| `PowerScripts.Host.exe` | Script process | Normalized inputs |
| Script process | `PowerScripts.Host.exe` | stdout, stderr, exit status, and result |
| Script process | Current-user resources | User-level file/process/device/network access |
| `PowerScripts.Host.exe` | PowerToys consumer module | Structured result or error |

### 2.3 Trust-boundary intent

TMT boundaries are containers: place the elements listed under **Inside** within the dotted
boundary and leave the elements listed under **Outside** outside it. A boundary may contain only
one element when that element represents a separate trust, privilege, or isolation domain; this is
normal for an isolated script process or runtime. Do not add a boundary solely to group related
boxes visually. A flow that crosses a boundary should be drawn so that TMT can analyze the crossing.

Use these boundaries:

| Boundary | Inside | Outside | Flows that cross it |
| --- | --- | --- | --- |
| Host API boundary | `PowerScripts.Host.exe` | User, PowerToys consumer module, scripts folder, script process | Consumer request/result; Host package read |
| Host/script privilege boundary | Script process | `PowerScripts.Host.exe` and the consumer | Host launch and normalized inputs; script result |

The Host/script boundary is a **privilege boundary only**. It does not claim containment. The
script process may access current-user resources because Proposal A explicitly accepts that risk.
Keep **Current-user resources** outside both boundaries. Draw the script-to-resources flow, but do not
surround those resources with a new trust boundary. This makes the accepted-risk path visible
without implying that Proposal A provides containment. Add this note to the diagram:

> Proposal A prevents a script from receiving an elevated token, including when PowerToys is
> elevated. It does not sandbox a script from resources available to the interactive user without
> elevation.

### 2.4 STRIDE review focus

Use the generated threats to drive these explicit decisions:

| Threat area | Expected disposition |
| --- | --- |
| Elevation of privilege from an elevated Host | **Mitigated** only when launch with the current interactive user's non-elevated token is implemented and tested, with no UAC or elevated fallback; otherwise **Needs investigation**. |
| Script reads or changes arbitrary user data | **Accepted risk** under Proposal A; document that this is intentional non-containment. |
| Malicious or modified package | **Accepted risk** after user-controlled installation, unless a separate authenticity policy is added. |
| Descriptor/header ambiguity or malformed input | **Needs investigation** until parser validation and rejection behavior are evidenced. |
| Consumer supplies an invalid path or parameter | **Needs investigation** until Host normalization and contract validation are evidenced. |
| Script output changes caller data unexpectedly | **Accepted risk** for direct user-level side effects; **Needs investigation** for outputs returned through the Host contract. |
| Hung process or unbounded output | **Needs investigation** until timeout and output-limit behavior is defined and tested. |

The key review conclusion for this model is:

> Proposal A's security guarantee is “scripts always run non-elevated and receive no additional
> privilege from PowerToys”; it is not “safe execution of untrusted scripts.”

## 3. Proposal B model — isolated execution

### 3.1 Diagram elements

Create these elements:

| Name | TMT type | Notes |
| --- | --- | --- |
| User | External interactor | Explicitly approves installation or uninstallation of an extension package; approval is not the same as administrator elevation. |
| PowerToys consumer module | External interactor | Sends a run/transform request and selected inputs. |
| Package manager | Process | Performs authorized installation and removal. |
| Managed secure space | Data store | Installed package and dependencies; writable only on install/uninstall. |
| `PowerScripts.Host.exe` | Process | Validates the contract and creates the restricted run. |
| Restricted Windows runner | Process | Windows execution backend with token, filesystem, network, and child-process controls. |
| WSL container | Process | WSL execution backend with controlled mounts and lifecycle. |
| Windows per-run workspace | Data store | Temporary state and controlled output area for the restricted Windows runner. |
| WSL per-run workspace | Data store | Temporary state and controlled output area for the WSL container. |
| Selected input files | Data store | Explicitly provided inputs, mounted/readable only for the run. |
| Unrelated host files and credentials | Data store | Must not be reachable from the restricted runtime. |
| Network | External interactor | Must be denied by default. |
| Administrator privileges | External interactor | Represents the privilege level the script must not obtain. |
| Installation boundary | Trust boundary | Separates package-management writes from normal execution. |
| Runtime isolation boundary | Trust boundary | Encloses the restricted Windows runner or WSL container. |

### 3.2 Data flows

Draw these flows:

| From | To | Label |
| --- | --- | --- |
| User | Package manager | User-approved install/uninstall request (not inherently elevated) |
| Package manager | Managed secure space | Package and dependency write/remove |
| PowerToys consumer module | `PowerScripts.Host.exe` | Run/transform request and declared inputs |
| Managed secure space | `PowerScripts.Host.exe` | Read-only package, dependency, and policy metadata |
| `PowerScripts.Host.exe` | Restricted Windows runner | Create process with restricted policy |
| `PowerScripts.Host.exe` | WSL container | Create/reuse isolated container with controlled mounts |
| Managed secure space | Restricted Windows runner | Read-only package mount |
| Managed secure space | WSL container | Read-only package mount |
| Selected input files | Restricted Windows runner | Read-only selected inputs |
| Selected input files | WSL container | Read-only selected inputs |
| Restricted Windows runner | Windows per-run workspace | Temporary state and controlled outputs |
| WSL container | WSL per-run workspace | Temporary state and controlled outputs |
| Restricted Windows runner | `PowerScripts.Host.exe` | stdout, stderr, exit status, and output files |
| WSL container | `PowerScripts.Host.exe` | stdout, stderr, exit status, and output files |
| `PowerScripts.Host.exe` | PowerToys consumer module | Validated declared results |

### 3.3 Isolation boundary notes

Use these boundaries:

| Boundary | Inside | Outside | Flows that cross it |
| --- | --- | --- | --- |
| Installation boundary | Package manager and managed secure space | User, PowerToys consumer module, Host, restricted runtime | User approval to install/uninstall; Host read of the installed package |
| Runtime isolation boundary | Restricted Windows runner, WSL container, Windows per-run workspace, and WSL per-run workspace | Host, package manager, managed secure space, original input files, output collector, unrelated host files, credentials, network, administrator privileges | Host selects one backend; package and selected inputs enter that backend read-only; runtime results leave for the collector |

Do not place the managed secure space inside the Runtime isolation boundary. It is intentionally
outside the run and is exposed only through a read-only package/dependency flow. Treat the Windows
runner/workspace path and the WSL container/workspace path as **alternative branches**: a given
execution selects one backend and creates only that backend's workspace. Do not imply that both
workspaces are active for the same run. Do not place the original selected-input data store inside
the runtime boundary. The runtime receives a read-only run-scoped copy or mount of selected inputs,
then sends results back to `PowerScripts.Host.exe`, which validates and returns declared outputs to
the consumer. Do not place unrelated host files, credentials, network, or administrator privileges
inside the runtime boundary; they are external resources whose access must be denied.

**Privilege clarification:** the model currently requires explicit user approval for installation
and uninstallation, but does not assert that either operation requires administrator privilege.
Model an administrator/elevation boundary for installation only if the selected storage or
deployment mechanism actually requires it. In all cases, normal script execution must remain
non-administrator and must not inherit an elevated Host token.

Add the following denied flows as threat-model notes or rejected data flows, depending on the TMT
template:

- Restricted runtime → unrelated host files and credentials: **blocked**.
- Restricted runtime → network: **blocked by default**.
- Restricted runtime → administrator privileges: **blocked**.
- Restricted runtime → managed secure space write: **blocked during normal execution**.
- Restricted runtime → arbitrary child process: **denied or constrained by policy**.

Add this note to the runtime boundary:

> The boundary is valid only if the implementation enforces the policy. ACLs, hidden paths, or
> metadata claims alone are not sufficient.

### 3.4 STRIDE review focus

| Threat area | Expected disposition |
| --- | --- |
| Package tampering during execution | **Mitigated** only when all package/dependency mounts are read-only and tested. |
| Read outside selected inputs | **Needs investigation** until the Windows mechanism and WSL mounts are selected and tested. |
| Network exfiltration or runtime dependency download | **Needs investigation** until deny-by-default enforcement is evidenced. |
| Administrator/token escape | **Needs investigation** until restricted-token and child-process behavior is demonstrated. |
| Cross-run contamination | **Needs investigation** until cleanup/reset and reuse behavior are tested. |
| Malicious output path, link, or reparse point | **Needs investigation** until output validation rules are implemented and tested. |
| Installation/uninstallation authorization | **Needs investigation** until the package-management boundary and audit behavior are defined. |
| Resource exhaustion | **Needs investigation** until timeout, memory, process, and output limits are defined. |
| Host/consumer confused deputy | **Mitigated** only when every caller uses the same Host policy chokepoint. |

The key review conclusion for this model is:

> Proposal B has a stronger security goal, but its security claims remain conditional on selecting
> concrete, enforceable Windows and WSL mechanisms and producing tests for each boundary.

## 4. Save and report procedure

For each model:

1. Open the Microsoft Threat Modeling Tool and choose **Create a Model**.
2. Select **SDL TM Knowledge Base (Core)**.
3. Create the diagram from the element and flow tables above.
4. Add the named trust boundaries and notes.
5. Open **Analysis**, review every generated STRIDE threat, and fill in justification/status.
6. Save the native model as `.tm7`.
7. Use **Reports → Create HTML Report** and save the report beside the model.
8. Include both the `.tm7` model and its HTML report in the security-review package.

The HTML report is the review-friendly rendering; the `.tm7` file is the editable source of truth.
Keep the proposal Markdown as the narrative and decision record, and link each proposal to its
corresponding TMT model and report.

## 5. Evidence checklist before review

- [ ] Proposal A `.tm7` contains the Host/consumer and Host/script boundaries.
- [ ] Proposal A explicitly records arbitrary current-user access as accepted residual risk.
- [ ] Proposal A has evidence for never inheriting an elevated PowerToys token.
- [ ] Proposal B `.tm7` separates installation writes from normal execution.
- [ ] Proposal B encloses the restricted runtime and per-run workspace in an isolation boundary.
- [ ] Proposal B records network, unrelated files, administrator privileges, and package writes as denied paths.
- [ ] Every “Mitigated” status has an implementation or test reference.
- [ ] Every “Needs investigation” item is tracked as an open engineering decision.
- [ ] Both `.tm7` files and both HTML reports are saved and attached to the review.
