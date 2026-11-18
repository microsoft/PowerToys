// Editor features PowerToys enables, taken from monaco-editor's public `features/<name>/register` entry points.
// The Preview Pane and Peek are read-only, and Monaco disables editing commands there by itself. Registry Preview
// edits .reg files, so the everyday editing features (clipboard, undo, multicursor, line and word operations,
// find and replace, autocomplete) stay.
// Left out on purpose:
// - language-service and refactoring features, which also add context-menu entries: codeAction, codelens,
//   colorPicker, diffEditor, diffEditorBreadcrumbs, dropOrPasteInto, floatingMenu, format, gotoSymbol, gpu,
//   inlayHints, inlineCompletions, inlineProgress, inspectTokens, iPadShowKeyboard, linkedEditing,
//   parameterHints, quickCommand, quickHelp, quickOutline, referenceSearch, rename, semanticTokens,
//   toggleHighContrast, toggleTabFocusMode;
// - rarely used editing extras: caretOperations, dnd, inPlaceReplace, insertFinalNewLine;
// - features with nothing to act on: gotoError (no language reports errors), placeholderText (no placeholder).
import 'monaco-editor/features/anchorSelect/register';
import 'monaco-editor/features/bracketMatching/register';
import 'monaco-editor/features/clipboard/register';
import 'monaco-editor/features/codeEditor/register';
import 'monaco-editor/features/codicon/register';
import 'monaco-editor/features/comment/register';
import 'monaco-editor/features/contextmenu/register';
import 'monaco-editor/features/cursorUndo/register';
import 'monaco-editor/features/documentSymbols/register';
import 'monaco-editor/features/find/register';
import 'monaco-editor/features/folding/register';
import 'monaco-editor/features/fontZoom/register';
import 'monaco-editor/features/gotoLine/register';
import 'monaco-editor/features/hover/register';
import 'monaco-editor/features/indentation/register';
import 'monaco-editor/features/lineSelection/register';
import 'monaco-editor/features/linesOperations/register';
import 'monaco-editor/features/links/register';
import 'monaco-editor/features/longLinesHelper/register';
import 'monaco-editor/features/middleScroll/register';
import 'monaco-editor/features/multicursor/register';
import 'monaco-editor/features/readOnlyMessage/register';
import 'monaco-editor/features/sectionHeaders/register';
import 'monaco-editor/features/smartSelect/register';
import 'monaco-editor/features/snippet/register';
import 'monaco-editor/features/stickyScroll/register';
import 'monaco-editor/features/suggest/register';
import 'monaco-editor/features/tokenization/register';
import 'monaco-editor/features/unicodeHighlighter/register';
import 'monaco-editor/features/unusualLineTerminators/register';
import 'monaco-editor/features/wordHighlighter/register';
import 'monaco-editor/features/wordOperations/register';
import 'monaco-editor/features/wordPartOperations/register';
