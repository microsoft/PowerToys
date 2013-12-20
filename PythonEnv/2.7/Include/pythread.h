
#ifndef Py_PYTHREAD_H
#define Py_PYTHREAD_H

typedef void *PyThread_type_lock;
typedef void *PyThread_type_sema;

#ifdef __cplusplus
extern "C" {
#endif

PyAPI_FUNC(void) PyThread_init_thread(void);
PyAPI_FUNC(long) PyThread_start_new_thread(void (*)(void *), void *);
PyAPI_FUNC(void) PyThread_exit_thread(void);
PyAPI_FUNC(long) PyThread_get_thread_ident(void);

PyAPI_FUNC(PyThread_type_lock) PyThread_allocate_lock(void);
PyAPI_FUNC(void) PyThread_free_lock(PyThread_type_lock);
PyAPI_FUNC(int) PyThread_acquire_lock(PyThread_type_lock, int);
#define WAIT_LOCK	1
#define NOWAIT_LOCK	0
PyAPI_FUNC(void) PyThread_release_lock(PyThread_type_lock);

PyAPI_FUNC(size_t) PyThread_get_stacksize(void);
PyAPI_FUNC(int) PyThread_set_stacksize(size_t);

/* Thread Local Storage (TLS) API */
PyAPI_FUNC(int) PyThread_create_key(void);
PyAPI_FUNC(void) PyThread_delete_key(int);
PyAPI_FUNC(int) PyThread_set_key_value(int, void *);
PyAPI_FUNC(void *) PyThread_get_key_value(int);
PyAPI_FUNC(void) PyThread_delete_key_value(int key);

/* Cleanup after a fork */
PyAPI_FUNC(void) PyThread_ReInitTLS(void);

#ifdef __cplusplus
}
#endif

#endif /* !Py_PYTHREAD_H */
