# Run Output — {task title}

> Final assembled artifact from a multi-agent run.

## Termination Condition

**Reason:** {One of: User accepted | Reviewer approved | Constraint budget exhausted | Deadlock — escalated to user | User cancelled}

## Constraint Budgets

<!-- Track all active constraints inline. Remove this section if no constraints are active. -->

| Constraint | Used | Max | Status |
|------------|------|-----|--------|
| Clarifying questions | 📊 {n} | {max} | {Active / Exhausted} |
| Revision cycles | 📊 {n} | {max} | {Active / Exhausted} |

## Result

{Assembled final artifact goes here. This is the Coordinator's synthesis of agent outputs.}

---

## Reviewer Verdict

<!-- Include one block per review. Remove this section if no review occurred. -->

### Review by {Name} ({Role})

| Field | Value |
|-------|-------|
| **Verdict** | {Approved / Rejected} |
| **What's wrong** | {Specific issue — not vague} |
| **Why it matters** | {Impact if not fixed} |
| **Who fixes it** | {Name of agent assigned to revise — MUST NOT be the original author} |
| **Revision budget** | 📊 {used} / {max} revision cycles remaining |

---

## APPENDIX: RAW AGENT OUTPUTS

<!-- Paste each agent's verbatim response below. Do NOT edit, summarize, rewrite, or wrap in code fences. One section per agent. -->

### {Name} ({Role}) — Raw Output

{Paste agent's verbatim response here, unedited}

### {Name} ({Role}) — Raw Output

{Paste agent's verbatim response here, unedited}
