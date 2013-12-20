
/* System module interface */

#ifndef Py_SYSMODULE_H
#define Py_SYSMODULE_H
#ifdef __cplusplus
extern "C" {
#endif

PyAPI_FUNC(PyObject *) PySys_GetObject(char *);
PyAPI_FUNC(int) PySys_SetObject(char *, PyObject *);
PyAPI_FUNC(FILE *) PySys_GetFile(char *, FILE *);
PyAPI_FUNC(void) PySys_SetArgv(int, char **);
PyAPI_FUNC(void) PySys_SetArgvEx(int, char **, int);
PyAPI_FUNC(void) PySys_SetPath(char *);

PyAPI_FUNC(void) PySys_WriteStdout(const char *format, ...)
			Py_GCC_ATTRIBUTE((format(printf, 1, 2)));
PyAPI_FUNC(void) PySys_WriteStderr(const char *format, ...)
			Py_GCC_ATTRIBUTE((format(printf, 1, 2)));

PyAPI_FUNC(void) PySys_ResetWarnOptions(void);
PyAPI_FUNC(void) PySys_AddWarnOption(char *);
PyAPI_FUNC(int) PySys_HasWarnOptions(void);

#ifdef __cplusplus
}
#endif
#endif /* !Py_SYSMODULE_H */
