
/* Generator object interface */

#ifndef Py_GENOBJECT_H
#define Py_GENOBJECT_H
#ifdef __cplusplus
extern "C" {
#endif

struct _frame; /* Avoid including frameobject.h */

typedef struct {
	PyObject_HEAD
	/* The gi_ prefix is intended to remind of generator-iterator. */

	/* Note: gi_frame can be NULL if the generator is "finished" */
	struct _frame *gi_frame;

	/* True if generator is being executed. */
	int gi_running;
    
	/* The code object backing the generator */
	PyObject *gi_code;

	/* List of weak reference. */
	PyObject *gi_weakreflist;
} PyGenObject;

PyAPI_DATA(PyTypeObject) PyGen_Type;

#define PyGen_Check(op) PyObject_TypeCheck(op, &PyGen_Type)
#define PyGen_CheckExact(op) (Py_TYPE(op) == &PyGen_Type)

PyAPI_FUNC(PyObject *) PyGen_New(struct _frame *);
PyAPI_FUNC(int) PyGen_NeedsFinalizing(PyGenObject *);

#ifdef __cplusplus
}
#endif
#endif /* !Py_GENOBJECT_H */
