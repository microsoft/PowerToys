/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import './browser/controller/coreCommands.js';
import './browser/widget/codeEditorWidget.js';
import './browser/widget/diffEditorWidget.js';
import './browser/widget/diffNavigator.js';
import './contrib/anchorSelect/anchorSelect.js';
import './contrib/bracketMatching/bracketMatching.js';
import './contrib/caretOperations/caretOperations.js';
import './contrib/caretOperations/transpose.js';
import './contrib/clipboard/clipboard.js';
import './contrib/codeAction/codeActionContributions.js';
import './contrib/codelens/codelensController.js';
import './contrib/colorPicker/colorContributions.js';
import './contrib/comment/comment.js';
import './contrib/contextmenu/contextmenu.js';
import './contrib/cursorUndo/cursorUndo.js';
import './contrib/dnd/dnd.js';
import './contrib/find/findController.js';
import './contrib/folding/folding.js';
import './contrib/fontZoom/fontZoom.js';
import './contrib/format/formatActions.js';
import './contrib/documentSymbols/documentSymbols.js';
import './contrib/gotoSymbol/goToCommands.js';
import './contrib/gotoSymbol/link/goToDefinitionAtPosition.js';
import './contrib/gotoError/gotoError.js';
import './contrib/hover/hover.js';
import './contrib/indentation/indentation.js';
import './contrib/inlineHints/inlineHintsController.js';
import './contrib/inPlaceReplace/inPlaceReplace.js';
import './contrib/linesOperations/linesOperations.js';
import './contrib/linkedEditing/linkedEditing.js';
import './contrib/links/links.js';
import './contrib/multicursor/multicursor.js';
import './contrib/parameterHints/parameterHints.js';
import './contrib/rename/rename.js';
import './contrib/smartSelect/smartSelect.js';
import './contrib/snippet/snippetController2.js';
import './contrib/suggest/suggestController.js';
import './contrib/tokenization/tokenization.js';
import './contrib/toggleTabFocusMode/toggleTabFocusMode.js';
import './contrib/unusualLineTerminators/unusualLineTerminators.js';
import './contrib/viewportSemanticTokens/viewportSemanticTokens.js';
import './contrib/wordHighlighter/wordHighlighter.js';
import './contrib/wordOperations/wordOperations.js';
import './contrib/wordPartOperations/wordPartOperations.js';
// Load up these strings even in VSCode, even if they are not used
// in order to get them translated
import './common/standaloneStrings.js';
import '../base/browser/ui/codicons/codiconStyles.js'; // The codicons are defined here and must be loaded
