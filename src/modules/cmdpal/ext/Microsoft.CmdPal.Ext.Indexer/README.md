# File Search Built-in Extension

## Building Search Query

### Query Handling Contract

The module does not always forward the user query to Windows Search unchanged.

For simple free-text queries, it broadens filename matching so search feels more natural.
For queries that already look like AQS or other Windows Search syntax, it does not rewrite them.

That split is intentional.
The module is trying to improve plain filename search without breaking structured Windows Search queries.

### When We Do Not Rewrite

If the input looks structured, we pass it through `ISearchQueryHelper.GenerateSQLFromUserQuery(...)` as-is.

Examples:

- `name:report`
- `kind:folder`
- `kind:folder AND report`
- `*report*`
- `C:\Users`
- `size>10MB`
- `(report)`

Parentheses are treated conservatively because they can be real query syntax.

### What Broadening Means

For simple free-text input we may build two filename restrictions:

- a literal `LIKE` restriction on `System.FileName`
- an indexed `CONTAINS(System.ItemNameDisplay, ...)` restriction

They serve different purposes:

- `LIKE` preserves the original text literally
- `CONTAINS` gives better indexed matching and can normalize separator-like punctuation

The primary query may use both.
The fallback query uses the `LIKE` branch only.

### Intentional Asymmetry

The broadening is intentionally asymmetric.

Desired behavior:

- `red` should find `[red]`
- `[red]` should stay mostly literal

In other words:

- plain terms are broadened
- punctuation-wrapped literals are usually not normalized
- separator punctuation inside a token can still broaden

This is the most important design rule in the module.

### Separator Punctuation vs Wrapper Punctuation

Some punctuation behaves like a separator inside filenames.

Examples:

- `foo-bar`
- `20220409-tontrager.xlsx`

Users usually expect broadening here, because `tontrager` should still find `20220409-tontrager.xlsx`.

Other punctuation usually signals literal intent.

Examples:

- `[red]`
- `{draft}`
- `<todo>`

Those should usually stay on the literal filename path instead of being normalized to bare words.

### Examples

| User input | Behavior |
| --- | --- |
| `red` | broad plain-text search; can match `random [red] search.txt` |
| `[red]` | literal filename match; does not also broaden to plain `red` |
| `foo-bar` | keeps literal `foo-bar` matching and also broadens as a separator-style term |
| `term Kind:Folder` | broadens `term`, preserves `Kind:Folder` |
| `%` | treated as a literal percent sign in the filename match |
| `_` | treated as a literal underscore in the filename match |
| `(report)` | not rewritten locally; passed through to Windows Search |

### Why The Fallback Exists

Some inputs are valid literal filename searches but poor full-text searches.

Typical failure mode:

- the `CONTAINS(...)` side returns `QUERY_E_ALLNOISE`
- or the primary query otherwise fails to produce a useful rowset

When both branches exist:

- primary query = `CONTAINS(...) OR LIKE ...`
- fallback query = `LIKE ...` only

The fallback exists so punctuation-heavy or noisy input can still produce useful filename matches.