/*
   CObjects are marked Pending Deprecation as of Python 2.7.
   The full schedule for 2.x is as follows:
     - CObjects are marked Pending Deprecation in Python 2.7.
     - CObjects will be marked Deprecated in Python 2.8
       (if there is one).
     - CObjects will be removed in Python 2.9 (if there is one).

   Additionally, for the Python 3.x series:
     - CObjects were marked Deprecated in Python 3.1.
     - CObjects will be removed in Python 3.2.

   You should switch all use of CObjects to capsules.  Capsules
   have a safer and more consistent API.  For more information,
   see Include/pycapsule.h, or read the "Capsules" topic in
   the "Python/C API Reference Manual".

   Python 2.7 no longer uses CObjects itself; all objects which
   were formerly CObjects are now capsules.  Note that this change
   does not by itself break binary compatibility with extensions
   built for previous versions of Python--PyCObject_AsVoidPtr()
   has been changed to also understand capsules.

*/

/* original file header comment follows: */

/* C objects to be exported from one extension module to another.
 
   C objects are used for communication between extension modules.
   They provide a way for an extension module to export a C interface
   to other extension modules, so that extension modules can use the
   Python import mechanism to link to one another.

*/

#ifndef Py_COBJECT_H
#define Py_COBJECT_H
#ifdef __cplusplus
extern "C" {
#endif

PyAPI_DATA(PyTypeObject) PyCObject_Type;

#define PyCObject_Check(op) (Py_TYPE(op) == &PyCObject_Type)

/* Create a PyCObject from a pointer to a C object and an optional
   destructor function.  If the second argument is non-null, then it
   will be called with the first argument if and when the PyCObject is
   destroyed.

*/
PyAPI_FUNC(PyObject *) PyCObject_FromVoidPtr(
	void *cobj, void (*destruct)(void*));


/* Create a PyCObject from a pointer to a C object, a description object,
   and an optional destructor function.  If the third argument is non-null,
   then it will be called with the first and second arguments if and when 
   the PyCObject is destroyed.
*/
PyAPI_FUNC(PyObject *) PyCObject_FromVoidPtrAndDesc(
	void *cobj, void *desc, void (*destruct)(void*,void*));

/* Retrieve a pointer to a C object from a PyCObject. */
PyAPI_FUNC(void *) PyCObject_AsVoidPtr(PyObject *);

/* Retrieve a pointer to a description object from a PyCObject. */
PyAPI_FUNC(void *) PyCObject_GetDesc(PyObject *);

/* Import a pointer to a C object from a module using a PyCObject. */
PyAPI_FUNC(void *) PyCObject_Import(char *module_name, char *cobject_name);

/* Modify a C object. Fails (==0) if object has a destructor. */
PyAPI_FUNC(int) PyCObject_SetVoidPtr(PyObject *self, void *cobj);


typedef struct {
    PyObject_HEAD
    void *cobject;
    void *desc;
    void (*destructor)(void *);
} PyCObject;


#ifdef __cplusplus
}
#endif
#endif /* !Py_COBJECT_H */
