# GPO Testing Instructions for Markdown Local Images

## Manual Registry Testing

Run these commands in an elevated (Administrator) command prompt:

```cmd
REM Force-enable local images (toggle locked ON in Settings UI):
reg add "HKLM\SOFTWARE\Policies\PowerToys" /v MarkdownAllowLocalImages /t REG_DWORD /d 1 /f

REM Force-disable local images (toggle locked OFF in Settings UI):
reg add "HKLM\SOFTWARE\Policies\PowerToys" /v MarkdownAllowLocalImages /t REG_DWORD /d 0 /f

REM Remove policy (user controls the setting):
reg delete "HKLM\SOFTWARE\Policies\PowerToys" /v MarkdownAllowLocalImages /f
```

After changing the registry value, restart PowerToys for the setting to take effect.

## Group Policy Editor Testing (gpedit.msc)

1. Copy `src/gpo/assets/PowerToys.admx` to `C:\Windows\PolicyDefinitions\`
2. Copy `src/gpo/assets/en-US/PowerToys.adml` to `C:\Windows\PolicyDefinitions\en-US\`
3. Open `gpedit.msc`
4. Navigate to: Computer Configuration > Administrative Templates > PowerToys > File Explorer Preview
5. Find "Markdown preview: Show local images - Configure enabled state"
6. Set to Enabled, Disabled, or Not Configured

## Expected Behavior Matrix

| GPO State | User Setting | Images Shown? | Settings UI Toggle | Info Bar |
|-----------|-------------|---------------|-------------------|----------|
| Not configured | OFF | No | Editable, OFF | "Some pictures have been blocked..." |
| Not configured | ON | Local/UNC only | Editable, ON | "Some online images have been blocked..." (if remote images in file) |
| Enabled | (ignored) | Local/UNC only | Locked ON (grayed out) | "Some online images have been blocked..." (if remote images in file) |
| Disabled | (ignored) | No | Locked OFF (grayed out) | "Some pictures have been blocked..." |

## Verification Steps

1. Set GPO to "Enabled" via registry
2. Open PowerToys Settings > File Explorer > Markdown
3. Verify the "Show local images" toggle is ON and grayed out (cannot be changed)
4. Preview `test-local-images.md` in File Explorer
5. Verify local image renders, remote image is blocked with info bar

6. Set GPO to "Disabled" via registry
7. Restart PowerToys
8. Verify the toggle is OFF and grayed out
9. Preview the same file
10. Verify all images are blocked

11. Remove the GPO registry value
12. Restart PowerToys
13. Verify the toggle is editable again

## Notes

- Machine scope (HKLM) takes precedence over user scope (HKCU)
- The "Some preview handlers are managed by your organization" info bar will appear in Settings when any GPO is configured
- Remote/online images (http/https URLs) are ALWAYS blocked regardless of this setting
