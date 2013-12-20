/* An arena-like memory interface for the compiler.
 */

#ifndef Py_PYARENA_H
#define Py_PYARENA_H

#ifdef __cplusplus
extern "C" {
#endif

  typedef struct _arena PyArena;

  /* PyArena_New() and PyArena_Free() create a new arena and free it,
     respectively.  Once an arena has been created, it can be used
     to allocate memory via PyArena_Malloc().  Pointers to PyObject can
     also be registered with the arena via PyArena_AddPyObject(), and the
     arena will ensure that the PyObjects stay alive at least until
     PyArena_Free() is called.  When an arena is freed, all the memory it
     allocated is freed, the arena releases internal references to registered
     PyObject*, and none of its pointers are valid.
     XXX (tim) What does "none of its pointers are valid" mean?  Does it
     XXX mean that pointers previously obtained via PyArena_Malloc() are
     XXX no longer valid?  (That's clearly true, but not sure that's what
     XXX the text is trying to say.)

     PyArena_New() returns an arena pointer.  On error, it
     returns a negative number and sets an exception.
     XXX (tim):  Not true.  On error, PyArena_New() actually returns NULL,
     XXX and looks like it may or may not set an exception (e.g., if the
     XXX internal PyList_New(0) returns NULL, PyArena_New() passes that on
     XXX and an exception is set; OTOH, if the internal
     XXX block_new(DEFAULT_BLOCK_SIZE) returns NULL, that's passed on but
     XXX an exception is not set in that case).
  */
  PyAPI_FUNC(PyArena *) PyArena_New(void);
  PyAPI_FUNC(void) PyArena_Free(PyArena *);

  /* Mostly like malloc(), return the address of a block of memory spanning
   * `size` bytes, or return NULL (without setting an exception) if enough
   * new memory can't be obtained.  Unlike malloc(0), PyArena_Malloc() with
   * size=0 does not guarantee to return a unique pointer (the pointer
   * returned may equal one or more other pointers obtained from
   * PyArena_Malloc()).
   * Note that pointers obtained via PyArena_Malloc() must never be passed to
   * the system free() or realloc(), or to any of Python's similar memory-
   * management functions.  PyArena_Malloc()-obtained pointers remain valid
   * until PyArena_Free(ar) is called, at which point all pointers obtained
   * from the arena `ar` become invalid simultaneously.
   */
  PyAPI_FUNC(void *) PyArena_Malloc(PyArena *, size_t size);

  /* This routine isn't a proper arena allocation routine.  It takes
   * a PyObject* and records it so that it can be DECREFed when the
   * arena is freed.
   */
  PyAPI_FUNC(int) PyArena_AddPyObject(PyArena *, PyObject *);

#ifdef __cplusplus
}
#endif

#endif /* !Py_PYARENA_H */
