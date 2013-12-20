#ifndef Py_WARNINGS_H
#define Py_WARNINGS_H
#ifdef __cplusplus
extern "C" {
#endif

PyAPI_FUNC(void) _PyWarnings_Init(void);

PyAPI_FUNC(int) PyErr_WarnEx(PyObject *, const char *, Py_ssize_t);
PyAPI_FUNC(int) PyErr_WarnExplicit(PyObject *, const char *, const char *, int,
                                    const char *, PyObject *);

#define PyErr_WarnPy3k(msg, stacklevel) \
  (Py_Py3kWarningFlag ? PyErr_WarnEx(PyExc_DeprecationWarning, msg, stacklevel) : 0)

/* DEPRECATED: Use PyErr_WarnEx() instead. */
#define PyErr_Warn(category, msg) PyErr_WarnEx(category, msg, 1)

#ifdef __cplusplus
}
#endif
#endif /* !Py_WARNINGS_H */

