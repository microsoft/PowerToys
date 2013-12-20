/* Set object interface */

#ifndef Py_SETOBJECT_H
#define Py_SETOBJECT_H
#ifdef __cplusplus
extern "C" {
#endif


/*
There are three kinds of slots in the table:

1. Unused:  key == NULL
2. Active:  key != NULL and key != dummy
3. Dummy:   key == dummy

Note: .pop() abuses the hash field of an Unused or Dummy slot to
hold a search finger.  The hash field of Unused or Dummy slots has
no meaning otherwise.
*/

#define PySet_MINSIZE 8

typedef struct {
    long hash;      /* cached hash code for the entry key */
    PyObject *key;
} setentry;


/*
This data structure is shared by set and frozenset objects.
*/

typedef struct _setobject PySetObject;
struct _setobject {
    PyObject_HEAD

    Py_ssize_t fill;  /* # Active + # Dummy */
    Py_ssize_t used;  /* # Active */

    /* The table contains mask + 1 slots, and that's a power of 2.
     * We store the mask instead of the size because the mask is more
     * frequently needed.
     */
    Py_ssize_t mask;

    /* table points to smalltable for small tables, else to
     * additional malloc'ed memory.  table is never NULL!  This rule
     * saves repeated runtime null-tests.
     */
    setentry *table;
    setentry *(*lookup)(PySetObject *so, PyObject *key, long hash);
    setentry smalltable[PySet_MINSIZE];

    long hash;                  /* only used by frozenset objects */
    PyObject *weakreflist;      /* List of weak references */
};

PyAPI_DATA(PyTypeObject) PySet_Type;
PyAPI_DATA(PyTypeObject) PyFrozenSet_Type;

/* Invariants for frozensets:
 *     data is immutable.
 *     hash is the hash of the frozenset or -1 if not computed yet.
 * Invariants for sets:
 *     hash is -1
 */

#define PyFrozenSet_CheckExact(ob) (Py_TYPE(ob) == &PyFrozenSet_Type)
#define PyAnySet_CheckExact(ob) \
    (Py_TYPE(ob) == &PySet_Type || Py_TYPE(ob) == &PyFrozenSet_Type)
#define PyAnySet_Check(ob) \
    (Py_TYPE(ob) == &PySet_Type || Py_TYPE(ob) == &PyFrozenSet_Type || \
      PyType_IsSubtype(Py_TYPE(ob), &PySet_Type) || \
      PyType_IsSubtype(Py_TYPE(ob), &PyFrozenSet_Type))
#define PySet_Check(ob) \
    (Py_TYPE(ob) == &PySet_Type || \
    PyType_IsSubtype(Py_TYPE(ob), &PySet_Type))
#define   PyFrozenSet_Check(ob) \
    (Py_TYPE(ob) == &PyFrozenSet_Type || \
      PyType_IsSubtype(Py_TYPE(ob), &PyFrozenSet_Type))

PyAPI_FUNC(PyObject *) PySet_New(PyObject *);
PyAPI_FUNC(PyObject *) PyFrozenSet_New(PyObject *);
PyAPI_FUNC(Py_ssize_t) PySet_Size(PyObject *anyset);
#define PySet_GET_SIZE(so) (((PySetObject *)(so))->used)
PyAPI_FUNC(int) PySet_Clear(PyObject *set);
PyAPI_FUNC(int) PySet_Contains(PyObject *anyset, PyObject *key);
PyAPI_FUNC(int) PySet_Discard(PyObject *set, PyObject *key);
PyAPI_FUNC(int) PySet_Add(PyObject *set, PyObject *key);
PyAPI_FUNC(int) _PySet_Next(PyObject *set, Py_ssize_t *pos, PyObject **key);
PyAPI_FUNC(int) _PySet_NextEntry(PyObject *set, Py_ssize_t *pos, PyObject **key, long *hash);
PyAPI_FUNC(PyObject *) PySet_Pop(PyObject *set);
PyAPI_FUNC(int) _PySet_Update(PyObject *set, PyObject *iterable);

#ifdef __cplusplus
}
#endif
#endif /* !Py_SETOBJECT_H */
