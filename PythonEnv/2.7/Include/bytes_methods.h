#ifndef Py_BYTES_CTYPE_H
#define Py_BYTES_CTYPE_H

/*
 * The internal implementation behind PyString (bytes) and PyBytes (buffer)
 * methods of the given names, they operate on ASCII byte strings.
 */
extern PyObject* _Py_bytes_isspace(const char *cptr, Py_ssize_t len);
extern PyObject* _Py_bytes_isalpha(const char *cptr, Py_ssize_t len);
extern PyObject* _Py_bytes_isalnum(const char *cptr, Py_ssize_t len);
extern PyObject* _Py_bytes_isdigit(const char *cptr, Py_ssize_t len);
extern PyObject* _Py_bytes_islower(const char *cptr, Py_ssize_t len);
extern PyObject* _Py_bytes_isupper(const char *cptr, Py_ssize_t len);
extern PyObject* _Py_bytes_istitle(const char *cptr, Py_ssize_t len);

/* These store their len sized answer in the given preallocated *result arg. */
extern void _Py_bytes_lower(char *result, const char *cptr, Py_ssize_t len);
extern void _Py_bytes_upper(char *result, const char *cptr, Py_ssize_t len);
extern void _Py_bytes_title(char *result, char *s, Py_ssize_t len);
extern void _Py_bytes_capitalize(char *result, char *s, Py_ssize_t len);
extern void _Py_bytes_swapcase(char *result, char *s, Py_ssize_t len);

/* Shared __doc__ strings. */
extern const char _Py_isspace__doc__[];
extern const char _Py_isalpha__doc__[];
extern const char _Py_isalnum__doc__[];
extern const char _Py_isdigit__doc__[];
extern const char _Py_islower__doc__[];
extern const char _Py_isupper__doc__[];
extern const char _Py_istitle__doc__[];
extern const char _Py_lower__doc__[];
extern const char _Py_upper__doc__[];
extern const char _Py_title__doc__[];
extern const char _Py_capitalize__doc__[];
extern const char _Py_swapcase__doc__[];

/* These are left in for backward compatibility and will be removed
   in 2.8/3.2 */
#define ISLOWER(c)  Py_ISLOWER(c)
#define ISUPPER(c)  Py_ISUPPER(c)
#define ISALPHA(c)  Py_ISALPHA(c)
#define ISDIGIT(c)  Py_ISDIGIT(c)
#define ISXDIGIT(c) Py_ISXDIGIT(c)
#define ISALNUM(c)  Py_ISALNUM(c)
#define ISSPACE(c)  Py_ISSPACE(c)

#undef islower
#define islower(c) undefined_islower(c)
#undef isupper
#define isupper(c) undefined_isupper(c)
#undef isalpha
#define isalpha(c) undefined_isalpha(c)
#undef isdigit
#define isdigit(c) undefined_isdigit(c)
#undef isxdigit
#define isxdigit(c) undefined_isxdigit(c)
#undef isalnum
#define isalnum(c) undefined_isalnum(c)
#undef isspace
#define isspace(c) undefined_isspace(c)

/* These are left in for backward compatibility and will be removed
   in 2.8/3.2 */
#define TOLOWER(c) Py_TOLOWER(c)
#define TOUPPER(c) Py_TOUPPER(c)

#undef tolower
#define tolower(c) undefined_tolower(c)
#undef toupper
#define toupper(c) undefined_toupper(c)

/* this is needed because some docs are shared from the .o, not static */
#define PyDoc_STRVAR_shared(name,str) const char name[] = PyDoc_STR(str)

#endif /* !Py_BYTES_CTYPE_H */
