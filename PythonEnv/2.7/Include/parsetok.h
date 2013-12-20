
/* Parser-tokenizer link interface */

#ifndef Py_PARSETOK_H
#define Py_PARSETOK_H
#ifdef __cplusplus
extern "C" {
#endif

typedef struct {
    int error;
    const char *filename;
    int lineno;
    int offset;
    char *text;
    int token;
    int expected;
} perrdetail;

#if 0
#define PyPARSE_YIELD_IS_KEYWORD	0x0001
#endif

#define PyPARSE_DONT_IMPLY_DEDENT	0x0002

#if 0
#define PyPARSE_WITH_IS_KEYWORD		0x0003
#endif

#define PyPARSE_PRINT_IS_FUNCTION       0x0004
#define PyPARSE_UNICODE_LITERALS        0x0008



PyAPI_FUNC(node *) PyParser_ParseString(const char *, grammar *, int,
                                              perrdetail *);
PyAPI_FUNC(node *) PyParser_ParseFile (FILE *, const char *, grammar *, int,
                                             char *, char *, perrdetail *);

PyAPI_FUNC(node *) PyParser_ParseStringFlags(const char *, grammar *, int,
                                              perrdetail *, int);
PyAPI_FUNC(node *) PyParser_ParseFileFlags(FILE *, const char *, grammar *,
						 int, char *, char *,
						 perrdetail *, int);
PyAPI_FUNC(node *) PyParser_ParseFileFlagsEx(FILE *, const char *, grammar *,
						 int, char *, char *,
						 perrdetail *, int *);

PyAPI_FUNC(node *) PyParser_ParseStringFlagsFilename(const char *,
					      const char *,
					      grammar *, int,
                                              perrdetail *, int);
PyAPI_FUNC(node *) PyParser_ParseStringFlagsFilenameEx(const char *,
					      const char *,
					      grammar *, int,
                                              perrdetail *, int *);

/* Note that he following function is defined in pythonrun.c not parsetok.c. */
PyAPI_FUNC(void) PyParser_SetError(perrdetail *);

#ifdef __cplusplus
}
#endif
#endif /* !Py_PARSETOK_H */
