
/* Integer object interface */

/*
PyIntObject represents a (long) integer.  This is an immutable object;
an integer cannot change its value after creation.

There are functions to create new integer objects, to test an object
for integer-ness, and to get the integer value.  The latter functions
returns -1 and sets errno to EBADF if the object is not an PyIntObject.
None of the functions should be applied to nil objects.

The type PyIntObject is (unfortunately) exposed here so we can declare
_Py_TrueStruct and _Py_ZeroStruct in boolobject.h; don't use this.
*/

#ifndef Py_INTOBJECT_H
#define Py_INTOBJECT_H
#ifdef __cplusplus
extern "C" {
#endif

typedef struct {
    PyObject_HEAD
    long ob_ival;
} PyIntObject;

PyAPI_DATA(PyTypeObject) PyInt_Type;

#define PyInt_Check(op) \
		 PyType_FastSubclass((op)->ob_type, Py_TPFLAGS_INT_SUBCLASS)
#define PyInt_CheckExact(op) ((op)->ob_type == &PyInt_Type)

PyAPI_FUNC(PyObject *) PyInt_FromString(char*, char**, int);
#ifdef Py_USING_UNICODE
PyAPI_FUNC(PyObject *) PyInt_FromUnicode(Py_UNICODE*, Py_ssize_t, int);
#endif
PyAPI_FUNC(PyObject *) PyInt_FromLong(long);
PyAPI_FUNC(PyObject *) PyInt_FromSize_t(size_t);
PyAPI_FUNC(PyObject *) PyInt_FromSsize_t(Py_ssize_t);
PyAPI_FUNC(long) PyInt_AsLong(PyObject *);
PyAPI_FUNC(Py_ssize_t) PyInt_AsSsize_t(PyObject *);
PyAPI_FUNC(int) _PyInt_AsInt(PyObject *);
PyAPI_FUNC(unsigned long) PyInt_AsUnsignedLongMask(PyObject *);
#ifdef HAVE_LONG_LONG
PyAPI_FUNC(unsigned PY_LONG_LONG) PyInt_AsUnsignedLongLongMask(PyObject *);
#endif

PyAPI_FUNC(long) PyInt_GetMax(void);

/* Macro, trading safety for speed */
#define PyInt_AS_LONG(op) (((PyIntObject *)(op))->ob_ival)

/* These aren't really part of the Int object, but they're handy; the protos
 * are necessary for systems that need the magic of PyAPI_FUNC and that want
 * to have stropmodule as a dynamically loaded module instead of building it
 * into the main Python shared library/DLL.  Guido thinks I'm weird for
 * building it this way.  :-)  [cjh]
 */
PyAPI_FUNC(unsigned long) PyOS_strtoul(char *, char **, int);
PyAPI_FUNC(long) PyOS_strtol(char *, char **, int);

/* free list api */
PyAPI_FUNC(int) PyInt_ClearFreeList(void);

/* Convert an integer to the given base.  Returns a string.
   If base is 2, 8 or 16, add the proper prefix '0b', '0o' or '0x'.
   If newstyle is zero, then use the pre-2.6 behavior of octal having
   a leading "0" */
PyAPI_FUNC(PyObject*) _PyInt_Format(PyIntObject* v, int base, int newstyle);

/* Format the object based on the format_spec, as defined in PEP 3101
   (Advanced String Formatting). */
PyAPI_FUNC(PyObject *) _PyInt_FormatAdvanced(PyObject *obj,
					     char *format_spec,
					     Py_ssize_t format_spec_len);

#ifdef __cplusplus
}
#endif
#endif /* !Py_INTOBJECT_H */
