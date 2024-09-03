#ifdef __cplusplus
extern "C" {
#endif


BOOL ShowEulaW( const TCHAR * ToolName, int *argc, PWCHAR argv[] );
BOOL ShowEula( const TCHAR * ToolName, int *argc, TCHAR *argv[] );
DWORD ShowEulaConsole();
void ShowEulaConsoleNoPrompt();
BOOL IsIoTEdition();
BOOL IsRemoteOnlyEdition();
BOOL IsRunningRemotely();
BOOL IsEulaAccepted(const TCHAR * ToolName, int *argc, PTCHAR argv[]);

#ifdef __cplusplus
}
#endif
