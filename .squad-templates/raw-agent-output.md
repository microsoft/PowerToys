# Raw Agent Output — Appendix Format

> This template defines the format for the `## APPENDIX: RAW AGENT OUTPUTS` section
> in any multi-agent artifact.

## Rules

1. **Verbatim only.** Paste the agent's response exactly as returned. No edits.
2. **No summarizing.** Do not condense, paraphrase, or rephrase any part of the output.
3. **No rewriting.** Do not fix typos, grammar, formatting, or style.
4. **No code fences around the entire output.** The raw output is pasted as-is, not wrapped in ``` blocks.
5. **One section per agent.** Each agent that contributed gets its own heading.
6. **Order matches work order.** List agents in the order they were spawned.
7. **Include all outputs.** Even if an agent's work was rejected, include their output for diagnostic traceability.

## Format

```markdown
## APPENDIX: RAW AGENT OUTPUTS

### {Name} ({Role}) — Raw Output

{Paste agent's verbatim response here, unedited}

### {Name} ({Role}) — Raw Output

{Paste agent's verbatim response here, unedited}
```

## Why This Exists

The appendix provides diagnostic integrity. It lets anyone verify:
- What each agent actually said (vs. what the Coordinator assembled)
- Whether the Coordinator faithfully represented agent work
- What was lost or changed in synthesis

Without raw outputs, multi-agent collaboration is unauditable.
