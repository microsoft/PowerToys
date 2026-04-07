# Multi-Agent Artifact Format

When multiple agents contribute to a final artifact (document, analysis, design), use this format. The assembled result must include:

- Termination condition
- Constraint budgets (if active)
- Reviewer verdicts (if any)
- Raw agent outputs appendix

## Assembly Structure

The assembled result goes at the top. Below it, include:

```
## APPENDIX: RAW AGENT OUTPUTS

### {Name} ({Role}) — Raw Output
{Paste agent's verbatim response here, unedited}

### {Name} ({Role}) — Raw Output
{Paste agent's verbatim response here, unedited}
```

## Appendix Rules

This appendix is for diagnostic integrity. Do not edit, summarize, or polish the raw outputs. The Coordinator may not rewrite raw agent outputs; it may only paste them verbatim and assemble the final artifact above.

See `.squad/templates/run-output.md` for the complete output format template.
