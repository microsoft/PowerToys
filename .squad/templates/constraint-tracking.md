# Constraint Budget Tracking

When the user or system imposes constraints (question limits, revision limits, time budgets), maintain a visible counter in your responses and in the artifact.

## Format

```
📊 Clarifying questions used: 2 / 3
```

## Rules

- Update the counter each time the constraint is consumed
- When a constraint is exhausted, state it: `📊 Question budget exhausted (3/3). Proceeding with current information.`
- If no constraints are active, do not display counters
- Include the final constraint status in multi-agent artifacts

## Example Session

```
Coordinator: Spawning agents to analyze requirements...
📊 Clarifying questions used: 0 / 3

Agent asks clarification: "Should we support OAuth?"
Coordinator: Checking with user...
📊 Clarifying questions used: 1 / 3

Agent asks clarification: "What's the rate limit?"
Coordinator: Checking with user...
📊 Clarifying questions used: 2 / 3

Agent asks clarification: "Do we need RBAC?"
Coordinator: Checking with user...
📊 Clarifying questions used: 3 / 3

Agent asks clarification: "Should we cache responses?"
Coordinator: 📊 Question budget exhausted (3/3). Proceeding without clarification.
```
