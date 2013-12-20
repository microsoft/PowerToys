/* ByteArray object interface */

#ifndef Py_BYTEARRAYOBJECT_H
#define Py_BYTEARRAYOBJECT_H
#ifdef __cplusplus
extern "C" {
#endif

#include <stdarg.h>

/* Type PyByteArrayObject represents a mutable array of bytes.
 * The Python API is that of a sequence;
 * the bytes are mapped to ints in [0, 256).
 * Bytes are not characters; they may be used to encode characters.
 * The only way to go between bytes and str/unicode is via encoding
 * and decoding.
 * For the convenience of C programmers, the bytes type is considered
 * to contain a char pointer, not an unsigned char pointer.
 */

/* Object layout */
typedef struct {
    PyObject_VAR_HEAD
    /* XXX(nnorwitz): should ob_exports be Py_ssize_t? */
    int ob_exports; /* how many buffer exports */
    Py_ssize_t ob_alloc; /* How many bytes allocated */
    char *ob_bytes;
} PyByteArrayObject;

/* Type object */
PyAPI_DATA(PyTypeObject) PyByteArray_Type;
PyAPI_DATA(PyTypeObject) PyByteArrayIter_Type;

/* Type check macros */
#define PyByteArray_Check(self) PyObject_TypeCheck(self, &PyByteArray_Type)
#define PyByteArray_CheckExact(self) (Py_TYPE(self) == &PyByteArray_Type)

/* Direct API functions */
PyAPI_FUNC(PyObject *) PyByteArray_FromObject(PyObject *);
PyAPI_FUNC(PyObject *) PyByteArray_Concat(PyObject *, PyObject *);
PyAPI_FUNC(PyObject *) PyByteArray_FromStringAndSize(const char *, Py_ssize_t);
PyAPI_FUNC(Py_ssize_t) PyByteArray_Size(PyObject *);
PyAPI_FUNC(char *) PyByteArray_AsString(PyObject *);
PyAPI_FUNC(int) PyByteArray_Resize(PyObject *, Py_ssize_t);

/* Macros, trading safety for speed */
#define PyByteArray_AS_STRING(self) \
    (assert(PyByteArray_Check(self)), \
     Py_SIZE(self) ? ((PyByteArrayObject *)(self))->ob_bytes : _PyByteArray_empty_string)
#define PyByteArray_GET_SIZE(self)  (assert(PyByteArray_Check(self)),Py_SIZE(self))

PyAPI_DATA(char) _PyByteArray_empty_string[];

#ifdef __cplusplus
}
#endif
#endif /* !Py_BYTEARRAYOBJECT_H */
