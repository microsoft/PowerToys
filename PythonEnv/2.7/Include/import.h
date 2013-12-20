
/* Module definition and import interface */

#ifndef Py_IMPORT_H
#define Py_IMPORT_H
#ifdef __cplusplus
extern "C" {
#endif

PyAPI_FUNC(long) PyImport_GetMagicNumber(void);
PyAPI_FUNC(PyObject *) PyImport_ExecCodeModule(char *name, PyObject *co);
PyAPI_FUNC(PyObject *) PyImport_ExecCodeModuleEx(
	char *name, PyObject *co, char *pathname);
PyAPI_FUNC(PyObject *) PyImport_GetModuleDict(void);
PyAPI_FUNC(PyObject *) PyImport_AddModule(const char *name);
PyAPI_FUNC(PyObject *) PyImport_ImportModule(const char *name);
PyAPI_FUNC(PyObject *) PyImport_ImportModuleNoBlock(const char *);
PyAPI_FUNC(PyObject *) PyImport_ImportModuleLevel(char *name,
	PyObject *globals, PyObject *locals, PyObject *fromlist, int level);

#define PyImport_ImportModuleEx(n, g, l, f) \
	PyImport_ImportModuleLevel(n, g, l, f, -1)

PyAPI_FUNC(PyObject *) PyImport_GetImporter(PyObject *path);
PyAPI_FUNC(PyObject *) PyImport_Import(PyObject *name);
PyAPI_FUNC(PyObject *) PyImport_ReloadModule(PyObject *m);
PyAPI_FUNC(void) PyImport_Cleanup(void);
PyAPI_FUNC(int) PyImport_ImportFrozenModule(char *);

#ifdef WITH_THREAD
PyAPI_FUNC(void) _PyImport_AcquireLock(void);
PyAPI_FUNC(int) _PyImport_ReleaseLock(void);
#else
#define _PyImport_AcquireLock()
#define _PyImport_ReleaseLock() 1
#endif

PyAPI_FUNC(struct filedescr *) _PyImport_FindModule(
	const char *, PyObject *, char *, size_t, FILE **, PyObject **);
PyAPI_FUNC(int) _PyImport_IsScript(struct filedescr *);
PyAPI_FUNC(void) _PyImport_ReInitLock(void);

PyAPI_FUNC(PyObject *)_PyImport_FindExtension(char *, char *);
PyAPI_FUNC(PyObject *)_PyImport_FixupExtension(char *, char *);

struct _inittab {
    char *name;
    void (*initfunc)(void);
};

PyAPI_DATA(PyTypeObject) PyNullImporter_Type;
PyAPI_DATA(struct _inittab *) PyImport_Inittab;

PyAPI_FUNC(int) PyImport_AppendInittab(const char *name, void (*initfunc)(void));
PyAPI_FUNC(int) PyImport_ExtendInittab(struct _inittab *newtab);

struct _frozen {
    char *name;
    unsigned char *code;
    int size;
};

/* Embedding apps may change this pointer to point to their favorite
   collection of frozen modules: */

PyAPI_DATA(struct _frozen *) PyImport_FrozenModules;

#ifdef __cplusplus
}
#endif
#endif /* !Py_IMPORT_H */
