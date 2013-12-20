
/* File object interface */

#ifndef Py_FILEOBJECT_H
#define Py_FILEOBJECT_H
#ifdef __cplusplus
extern "C" {
#endif

typedef struct {
    PyObject_HEAD
    FILE *f_fp;
    PyObject *f_name;
    PyObject *f_mode;
    int (*f_close)(FILE *);
    int f_softspace;            /* Flag used by 'print' command */
    int f_binary;               /* Flag which indicates whether the file is
                               open in binary (1) or text (0) mode */
    char* f_buf;                /* Allocated readahead buffer */
    char* f_bufend;             /* Points after last occupied position */
    char* f_bufptr;             /* Current buffer position */
    char *f_setbuf;             /* Buffer for setbuf(3) and setvbuf(3) */
    int f_univ_newline;         /* Handle any newline convention */
    int f_newlinetypes;         /* Types of newlines seen */
    int f_skipnextlf;           /* Skip next \n */
    PyObject *f_encoding;
    PyObject *f_errors;
    PyObject *weakreflist; /* List of weak references */
    int unlocked_count;         /* Num. currently running sections of code
                               using f_fp with the GIL released. */
    int readable;
    int writable;
} PyFileObject;

PyAPI_DATA(PyTypeObject) PyFile_Type;

#define PyFile_Check(op) PyObject_TypeCheck(op, &PyFile_Type)
#define PyFile_CheckExact(op) (Py_TYPE(op) == &PyFile_Type)

PyAPI_FUNC(PyObject *) PyFile_FromString(char *, char *);
PyAPI_FUNC(void) PyFile_SetBufSize(PyObject *, int);
PyAPI_FUNC(int) PyFile_SetEncoding(PyObject *, const char *);
PyAPI_FUNC(int) PyFile_SetEncodingAndErrors(PyObject *, const char *, char *errors);
PyAPI_FUNC(PyObject *) PyFile_FromFile(FILE *, char *, char *,
                                             int (*)(FILE *));
PyAPI_FUNC(FILE *) PyFile_AsFile(PyObject *);
PyAPI_FUNC(void) PyFile_IncUseCount(PyFileObject *);
PyAPI_FUNC(void) PyFile_DecUseCount(PyFileObject *);
PyAPI_FUNC(PyObject *) PyFile_Name(PyObject *);
PyAPI_FUNC(PyObject *) PyFile_GetLine(PyObject *, int);
PyAPI_FUNC(int) PyFile_WriteObject(PyObject *, PyObject *, int);
PyAPI_FUNC(int) PyFile_SoftSpace(PyObject *, int);
PyAPI_FUNC(int) PyFile_WriteString(const char *, PyObject *);
PyAPI_FUNC(int) PyObject_AsFileDescriptor(PyObject *);

/* The default encoding used by the platform file system APIs
   If non-NULL, this is different than the default encoding for strings
*/
PyAPI_DATA(const char *) Py_FileSystemDefaultEncoding;

/* Routines to replace fread() and fgets() which accept any of \r, \n
   or \r\n as line terminators.
*/
#define PY_STDIOTEXTMODE "b"
char *Py_UniversalNewlineFgets(char *, int, FILE*, PyObject *);
size_t Py_UniversalNewlineFread(char *, size_t, FILE *, PyObject *);

/* A routine to do sanity checking on the file mode string.  returns
   non-zero on if an exception occurred
*/
int _PyFile_SanitizeMode(char *mode);

#if defined _MSC_VER && _MSC_VER >= 1400
/* A routine to check if a file descriptor is valid on Windows.  Returns 0
 * and sets errno to EBADF if it isn't.  This is to avoid Assertions
 * from various functions in the Windows CRT beginning with
 * Visual Studio 2005
 */
int _PyVerify_fd(int fd);
#elif defined _MSC_VER && _MSC_VER >= 1200
/* fdopen doesn't set errno EBADF and crashes for large fd on debug build */
#define _PyVerify_fd(fd) (_get_osfhandle(fd) >= 0)
#else
#define _PyVerify_fd(A) (1) /* dummy */
#endif

/* A routine to check if a file descriptor can be select()-ed. */
#ifdef HAVE_SELECT
 #define _PyIsSelectable_fd(FD) (((FD) >= 0) && ((FD) < FD_SETSIZE))
#else
 #define _PyIsSelectable_fd(FD) (1)
#endif /* HAVE_SELECT */

#ifdef __cplusplus
}
#endif
#endif /* !Py_FILEOBJECT_H */
