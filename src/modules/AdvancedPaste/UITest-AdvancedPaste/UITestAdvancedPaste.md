## [Advanced Paste](tests-checklist-template-advanced-paste-section.md)
  NOTES:
    When using Advanced Paste, make sure that window focused while starting/using Advanced paste is text editor or has text input field focused (e.g. Word).
 * Paste As Plain Text
   - [x] Copy some rich text (e.g word of the text is different color, another work is bold, underlined, etd.).
   - [x] Paste the text using standard Windows Ctrl + V shortcut and ensure that rich text is pasted (with all colors, formatting, etc.)
   - [x] Paste the text using Paste As Plain Text activation shortcut and ensure that plain text without any formatting is pasted.
   - [x] Paste again the text using standard Windows Ctrl + V shortcut and ensure the text is now pasted plain without formatting as well.
   - [x] Copy some rich text again.
   - [x] Open Advanced Paste window using hotkey, click Paste as Plain Text button and confirm that plain text without any formatting is pasted.
   - [x] Copy some rich text again.
   - [x] Open Advanced Paste window using hotkey, press Ctrl + 1 and confirm that plain text without any formatting is pasted.
 * Paste As Markdown
   - [] Open Settings and set Paste as Markdown directly hotkey
   - [x] Copy some text (e.g. some HTML text - convertible to Markdown)
   - [x] Paste the text using set hotkey and confirm that pasted text is converted to markdown
   - [x] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
   - [x] Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
   - [x] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
   - [x] Open Advanced Paste window using hotkey, press Ctrl + 2 and confirm that pasted text is converted to markdown
 * Paste As JSON
   - [] Open Settings and set Paste as JSON directly hotkey
   - [x] Copy some XML or CSV text (or any other text, it will be converted to simple JSON object)
   - [x] Paste the text using set hotkey and confirm that pasted text is converted to JSON
   - [x] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
   - [x] Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
   - [x] Copy some text (same as in the previous step or different. If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
   - [x] Open Advanced Paste window using hotkey, press Ctrl + 3 and confirm that pasted text is converted to markdown
 * Paste as custom format using AI
   - [] Open Settings, navigate to Enable Paste with AI and set OpenAI key.
   - [] Copy some text to clipboard. Any text.
   - [] Open Advanced Paste window using hotkey, and confirm that Custom intput text box is now enabled. Write "Insert smiley after every word" and press Enter. Observe that result preview shows coppied text with smileys between words. Press Enter to paste the result and observe that it is pasted.
   - [] Open Advanced Paste window using hotkey. Input some query (any, feel free to play around) and press Enter. When result is shown, click regenerate button, to see if new result is generated. Select one of the results and paste. Observe that correct result is pasted.
   - [] Create few custom actions. Set up hotkey for custom actions and confirm they work. Enable/disable custom actions and confirm that the change is reflected in Advanced Paste UI - custom action is not listed. Try different ctrl + <num> in-app shortcuts for custom actions. Try moving custom actions up/down and confirm that the change is reflected in Advanced Paste UI.
   - [] Open Settings and disable Custom format preview. Open Advanced Paste window with hotkey, enter some query and press enter. Observe that result is now pasted right away, without showing the preview first.
   - [] Open Settings and Disable Enable Paste with AI. Open Advanced Paste window with hotkey and observe that Custom Input text box is now disabled.
 * Clipboard History
   - [] Open Settings and Enable clipboard history (if not enabled already). Open Advanced Paste window with hotkey, click Clipboard history and try deleting some entry. Check OS clipboard history (Win+V), and confirm that the same entry no longer exist.
   - [] Open Advanced Paste window with hotkey, click Clipboard history, and click any entry (but first). Observe that entry is put on top of clipboard history. Check OS clipboard history (Win+V), and confirm that the same entry is on top of the clipboard.
   - [] Open Settings and Disable clipboard history. Open Advanced Paste window with hotkey and observe that Clipboard history button is disabled.
 * Disable Advanced Paste, try different Advanced Paste hotkeys and confirm that it's disabled and nothing happens.