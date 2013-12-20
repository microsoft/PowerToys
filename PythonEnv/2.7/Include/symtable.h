#ifndef Py_SYMTABLE_H
#define Py_SYMTABLE_H

#ifdef __cplusplus
extern "C" {
#endif

typedef enum _block_type { FunctionBlock, ClassBlock, ModuleBlock }
    _Py_block_ty;

struct _symtable_entry;

struct symtable {
    const char *st_filename; /* name of file being compiled */
    struct _symtable_entry *st_cur; /* current symbol table entry */
    struct _symtable_entry *st_top; /* module entry */
    PyObject *st_symbols;    /* dictionary of symbol table entries */
    PyObject *st_stack;      /* stack of namespace info */
    PyObject *st_global;     /* borrowed ref to MODULE in st_symbols */
    int st_nblocks;          /* number of blocks */
    PyObject *st_private;        /* name of current class or NULL */
    PyFutureFeatures *st_future; /* module's future features */
};

typedef struct _symtable_entry {
    PyObject_HEAD
    PyObject *ste_id;        /* int: key in st_symbols */
    PyObject *ste_symbols;   /* dict: name to flags */
    PyObject *ste_name;      /* string: name of block */
    PyObject *ste_varnames;  /* list of variable names */
    PyObject *ste_children;  /* list of child ids */
    _Py_block_ty ste_type;   /* module, class, or function */
    int ste_unoptimized;     /* false if namespace is optimized */
    int ste_nested;      /* true if block is nested */
    unsigned ste_free : 1;        /* true if block has free variables */
    unsigned ste_child_free : 1;  /* true if a child block has free vars,
                                     including free refs to globals */
    unsigned ste_generator : 1;   /* true if namespace is a generator */
    unsigned ste_varargs : 1;     /* true if block has varargs */
    unsigned ste_varkeywords : 1; /* true if block has varkeywords */
    unsigned ste_returns_value : 1;  /* true if namespace uses return with
                                        an argument */
    int ste_lineno;          /* first line of block */
    int ste_opt_lineno;      /* lineno of last exec or import * */
    int ste_tmpname;         /* counter for listcomp temp vars */
    struct symtable *ste_table;
} PySTEntryObject;

PyAPI_DATA(PyTypeObject) PySTEntry_Type;

#define PySTEntry_Check(op) (Py_TYPE(op) == &PySTEntry_Type)

PyAPI_FUNC(int) PyST_GetScope(PySTEntryObject *, PyObject *);

PyAPI_FUNC(struct symtable *) PySymtable_Build(mod_ty, const char *,
                                              PyFutureFeatures *);
PyAPI_FUNC(PySTEntryObject *) PySymtable_Lookup(struct symtable *, void *);

PyAPI_FUNC(void) PySymtable_Free(struct symtable *);

/* Flags for def-use information */

#define DEF_GLOBAL 1           /* global stmt */
#define DEF_LOCAL 2            /* assignment in code block */
#define DEF_PARAM 2<<1         /* formal parameter */
#define USE 2<<2               /* name is used */
#define DEF_FREE 2<<3         /* name used but not defined in nested block */
#define DEF_FREE_CLASS 2<<4    /* free variable from class's method */
#define DEF_IMPORT 2<<5        /* assignment occurred via import */

#define DEF_BOUND (DEF_LOCAL | DEF_PARAM | DEF_IMPORT)

/* GLOBAL_EXPLICIT and GLOBAL_IMPLICIT are used internally by the symbol
   table.  GLOBAL is returned from PyST_GetScope() for either of them.
   It is stored in ste_symbols at bits 12-14.
*/
#define SCOPE_OFF 11
#define SCOPE_MASK 7

#define LOCAL 1
#define GLOBAL_EXPLICIT 2
#define GLOBAL_IMPLICIT 3
#define FREE 4
#define CELL 5

/* The following three names are used for the ste_unoptimized bit field */
#define OPT_IMPORT_STAR 1
#define OPT_EXEC 2
#define OPT_BARE_EXEC 4
#define OPT_TOPLEVEL 8  /* top-level names, including eval and exec */

#define GENERATOR 1
#define GENERATOR_EXPRESSION 2

#ifdef __cplusplus
}
#endif
#endif /* !Py_SYMTABLE_H */
