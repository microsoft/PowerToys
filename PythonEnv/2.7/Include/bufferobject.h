
/* Buffer object interface */

/* Note: the object's structure is private */

#ifndef Py_BUFFEROBJECT_H
#define Py_BUFFEROBJECT_H
#ifdef __cplusplus
extern "C" {
#endif


PyAPI_DATA(PyTypeObject) PyBuffer_Type;

#define PyBuffer_Check(op) (Py_TYPE(op) == &PyBuffer_Type)

#define Py_END_OF_BUFFER	(-1)

PyAPI_FUNC(PyObject *) PyBuffer_FromObject(PyObject *base,
                                           Py_ssize_t offset, Py_ssize_t size);
PyAPI_FUNC(PyObject *) PyBuffer_FromReadWriteObject(PyObject *base,
                                                    Py_ssize_t offset,
                                                    Py_ssize_t size);

PyAPI_FUNC(PyObject *) PyBuffer_FromMemory(void *ptr, Py_ssize_t size);
PyAPI_FUNC(PyObject *) PyBuffer_FromReadWriteMemory(void *ptr, Py_ssize_t size);

PyAPI_FUNC(PyObject *) PyBuffer_New(Py_ssize_t size);

#ifdef __cplusplus
}
#endif
#endif /* !Py_BUFFEROBJECT_H */
