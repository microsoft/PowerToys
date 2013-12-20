#ifndef Py_STRUCTMEMBER_H
#define Py_STRUCTMEMBER_H
#ifdef __cplusplus
extern "C" {
#endif


/* Interface to map C struct members to Python object attributes */

#include <stddef.h> /* For offsetof */

/* The offsetof() macro calculates the offset of a structure member
   in its structure.  Unfortunately this cannot be written down
   portably, hence it is provided by a Standard C header file.
   For pre-Standard C compilers, here is a version that usually works
   (but watch out!): */

#ifndef offsetof
#define offsetof(type, member) ( (int) & ((type*)0) -> member )
#endif

/* An array of memberlist structures defines the name, type and offset
   of selected members of a C structure.  These can be read by
   PyMember_Get() and set by PyMember_Set() (except if their READONLY flag
   is set).  The array must be terminated with an entry whose name
   pointer is NULL. */

struct memberlist {
    /* Obsolete version, for binary backwards compatibility */
    char *name;
    int type;
    int offset;
    int flags;
};

typedef struct PyMemberDef {
    /* Current version, use this */
    char *name;
    int type;
    Py_ssize_t offset;
    int flags;
    char *doc;
} PyMemberDef;

/* Types */
#define T_SHORT         0
#define T_INT           1
#define T_LONG          2
#define T_FLOAT         3
#define T_DOUBLE        4
#define T_STRING        5
#define T_OBJECT        6
/* XXX the ordering here is weird for binary compatibility */
#define T_CHAR          7       /* 1-character string */
#define T_BYTE          8       /* 8-bit signed int */
/* unsigned variants: */
#define T_UBYTE         9
#define T_USHORT        10
#define T_UINT          11
#define T_ULONG         12

/* Added by Jack: strings contained in the structure */
#define T_STRING_INPLACE        13

/* Added by Lillo: bools contained in the structure (assumed char) */
#define T_BOOL          14

#define T_OBJECT_EX     16      /* Like T_OBJECT, but raises AttributeError
                   when the value is NULL, instead of
                   converting to None. */
#ifdef HAVE_LONG_LONG
#define T_LONGLONG      17
#define T_ULONGLONG      18
#endif /* HAVE_LONG_LONG */

#define T_PYSSIZET       19 /* Py_ssize_t */


/* Flags */
#define READONLY        1
#define RO              READONLY                /* Shorthand */
#define READ_RESTRICTED 2
#define PY_WRITE_RESTRICTED 4
#define RESTRICTED      (READ_RESTRICTED | PY_WRITE_RESTRICTED)


/* Obsolete API, for binary backwards compatibility */
PyAPI_FUNC(PyObject *) PyMember_Get(const char *, struct memberlist *, const char *);
PyAPI_FUNC(int) PyMember_Set(char *, struct memberlist *, const char *, PyObject *);

/* Current API, use this */
PyAPI_FUNC(PyObject *) PyMember_GetOne(const char *, struct PyMemberDef *);
PyAPI_FUNC(int) PyMember_SetOne(char *, struct PyMemberDef *, PyObject *);


#ifdef __cplusplus
}
#endif
#endif /* !Py_STRUCTMEMBER_H */
