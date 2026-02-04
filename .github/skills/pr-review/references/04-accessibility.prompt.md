# Step 04: Accessibility Review

**Goal**: Ensure UI changes are accessible to users with disabilities, following WCAG guidelines and Windows accessibility standards.

## Output file
`Generated Files/prReview/{{pr_number}}/04-accessibility.md`

## Checks to execute

### Screen reader support
- [ ] Do all interactive elements have accessible names (AutomationProperties.Name)?
- [ ] Are images with meaning given alt text (AutomationProperties.Name)?
- [ ] Are decorative images marked as such (AutomationProperties.AccessibilityView="Raw")?
- [ ] Is live region support used for dynamic content updates?
- [ ] Are landmarks/headings used for navigation structure?

### Keyboard navigation
- [ ] Can all functionality be accessed via keyboard alone?
- [ ] Is tab order logical and complete?
- [ ] Are custom controls keyboard accessible?
- [ ] Are keyboard shortcuts documented and non-conflicting?
- [ ] Is focus visible and properly managed?
- [ ] Are focus traps avoided (dialogs excepted)?

### Color and contrast
- [ ] Does text meet minimum contrast ratios (4.5:1 for normal, 3:1 for large)?
- [ ] Is color not the only means of conveying information?
- [ ] Are error states indicated by more than just color?
- [ ] Does the UI work in high contrast mode?
- [ ] Are focus indicators visible in all themes?

### Visual design
- [ ] Can text be resized up to 200% without loss of functionality?
- [ ] Are touch targets at least 44x44 pixels?
- [ ] Is spacing sufficient between interactive elements?
- [ ] Are animations respectful of prefers-reduced-motion?
- [ ] Is content readable without requiring horizontal scrolling?

### Forms and input
- [ ] Are form fields properly labeled?
- [ ] Are error messages associated with their fields?
- [ ] Are required fields indicated accessibly?
- [ ] Is autocomplete supported where appropriate?
- [ ] Are input instructions provided before fields?

### Windows-specific
- [ ] Are UIA (UI Automation) patterns correctly implemented?
- [ ] Does the control work with Narrator?
- [ ] Are tooltips accessible (keyboard-triggerable)?
- [ ] Is the control visible in Accessibility Insights?

## PowerToys-specific checks
- [ ] Are Settings UI pages fully keyboard navigable?
- [ ] Do overlay UIs (FancyZones editor, ColorPicker) support keyboard?
- [ ] Are hotkey-activated features announced to screen readers?
- [ ] Do preview handlers provide accessible content?
- [ ] Are notification messages accessible?

## File template
```md
# Accessibility Review
**PR:** {{pr_number}} — Base:{{baseRefName}} Head:{{headRefName}}
**Review iteration:** {{iteration}}

## Iteration history
### Iteration {{iteration}}
- <Key finding 1>
- <Key finding 2>

## Checks executed
- <List specific checks performed>

## Findings
```mcp-review-comment
{"file":"path/to/file.xaml","start_line":45,"end_line":50,"severity":"high|medium|low|info","tags":["accessibility","pr-{{pr_number}}"],"body":"Problem → WCAG criterion affected → Concrete fix."}
```
```

## Severity guidelines
- **High**: Completely inaccessible feature, keyboard trap, missing screen reader support
- **Medium**: Partial accessibility, poor contrast, missing labels
- **Low**: Minor accessibility improvements, enhancement opportunities
- **Info**: Best practice suggestions, proactive improvements

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| WCAG 2.1 Quick Ref | https://www.w3.org/WAI/WCAG21/quickref/ | WCAG Level A/AA violations |
| Windows Accessibility | https://docs.microsoft.com/en-us/windows/apps/design/accessibility/accessibility | Windows-specific patterns |
| UIA Patterns | https://docs.microsoft.com/en-us/windows/win32/winauto/uiauto-controlpatternsoverview | Automation support |
| Contrast Checker | https://webaim.org/resources/contrastchecker/ | Color contrast ratios |

**Enforcement**: Include `## References consulted` section with:
- WCAG success criteria checked (e.g., 1.4.3 Contrast)
- Any violations with specific guideline IDs
