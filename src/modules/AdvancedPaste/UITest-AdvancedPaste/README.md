# Legacy Advanced Paste UI tests

This WinAppDriver-based project is retained as the migration reference. Add new
coverage and fix active UI tests in `..\AdvancedPaste.UITests.Next`, which uses
`Microsoft.PowerToys.UITest.Next` and the winapp CLI.

The new suite links this directory's `TestFiles` rather than duplicating them.
Do not remove those fixtures with the old test project.

Retire this project only in a separate maintainer-approved cleanup after the new
suite has replaced every legacy consumer and its shared fixtures have been
preserved or relocated. There is no requirement to maintain duplicate test
coverage in both projects.
