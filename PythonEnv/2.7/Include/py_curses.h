
#ifndef Py_CURSES_H
#define Py_CURSES_H

#ifdef __APPLE__
/*
** On Mac OS X 10.2 [n]curses.h and stdlib.h use different guards
** against multiple definition of wchar_t.
*/
#ifdef	_BSD_WCHAR_T_DEFINED_
#define _WCHAR_T
#endif

/* the following define is necessary for OS X 10.6; without it, the
   Apple-supplied ncurses.h sets NCURSES_OPAQUE to 1, and then Python
   can't get at the WINDOW flags field. */
#define NCURSES_OPAQUE 0
#endif /* __APPLE__ */

#ifdef __FreeBSD__
/*
** On FreeBSD, [n]curses.h and stdlib.h/wchar.h use different guards
** against multiple definition of wchar_t and wint_t.
*/
#ifdef	_XOPEN_SOURCE_EXTENDED
#ifndef __FreeBSD_version
#include <osreldate.h>
#endif
#if __FreeBSD_version >= 500000
#ifndef __wchar_t
#define __wchar_t
#endif
#ifndef __wint_t
#define __wint_t
#endif
#else
#ifndef _WCHAR_T
#define _WCHAR_T
#endif
#ifndef _WINT_T
#define _WINT_T
#endif
#endif
#endif
#endif

#ifdef HAVE_NCURSES_H
#include <ncurses.h>
#else
#include <curses.h>
#ifdef HAVE_TERM_H
/* for tigetstr, which is not declared in SysV curses */
#include <term.h>
#endif
#endif

#ifdef HAVE_NCURSES_H
/* configure was checking <curses.h>, but we will
   use <ncurses.h>, which has all these features. */
#ifndef WINDOW_HAS_FLAGS
#define WINDOW_HAS_FLAGS 1
#endif
#ifndef MVWDELCH_IS_EXPRESSION
#define MVWDELCH_IS_EXPRESSION 1
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define PyCurses_API_pointers 4

/* Type declarations */

typedef struct {
	PyObject_HEAD
	WINDOW *win;
} PyCursesWindowObject;

#define PyCursesWindow_Check(v)	 (Py_TYPE(v) == &PyCursesWindow_Type)

#define PyCurses_CAPSULE_NAME "_curses._C_API"


#ifdef CURSES_MODULE
/* This section is used when compiling _cursesmodule.c */

#else
/* This section is used in modules that use the _cursesmodule API */

static void **PyCurses_API;

#define PyCursesWindow_Type (*(PyTypeObject *) PyCurses_API[0])
#define PyCursesSetupTermCalled  {if (! ((int (*)(void))PyCurses_API[1]) () ) return NULL;}
#define PyCursesInitialised      {if (! ((int (*)(void))PyCurses_API[2]) () ) return NULL;}
#define PyCursesInitialisedColor {if (! ((int (*)(void))PyCurses_API[3]) () ) return NULL;}

#define import_curses() \
    PyCurses_API = (void **)PyCapsule_Import(PyCurses_CAPSULE_NAME, 1);

#endif

/* general error messages */
static char *catchall_ERR  = "curses function returned ERR";
static char *catchall_NULL = "curses function returned NULL";

/* Function Prototype Macros - They are ugly but very, very useful. ;-)

   X - function name
   TYPE - parameter Type
   ERGSTR - format string for construction of the return value
   PARSESTR - format string for argument parsing
   */

#define NoArgNoReturnFunction(X) \
static PyObject *PyCurses_ ## X (PyObject *self) \
{ \
  PyCursesInitialised \
  return PyCursesCheckERR(X(), # X); }

#define NoArgOrFlagNoReturnFunction(X) \
static PyObject *PyCurses_ ## X (PyObject *self, PyObject *args) \
{ \
  int flag = 0; \
  PyCursesInitialised \
  switch(PyTuple_Size(args)) { \
  case 0: \
    return PyCursesCheckERR(X(), # X); \
  case 1: \
    if (!PyArg_ParseTuple(args, "i;True(1) or False(0)", &flag)) return NULL; \
    if (flag) return PyCursesCheckERR(X(), # X); \
    else return PyCursesCheckERR(no ## X (), # X); \
  default: \
    PyErr_SetString(PyExc_TypeError, # X " requires 0 or 1 arguments"); \
    return NULL; } }

#define NoArgReturnIntFunction(X) \
static PyObject *PyCurses_ ## X (PyObject *self) \
{ \
 PyCursesInitialised \
 return PyInt_FromLong((long) X()); }


#define NoArgReturnStringFunction(X) \
static PyObject *PyCurses_ ## X (PyObject *self) \
{ \
  PyCursesInitialised \
  return PyString_FromString(X()); }

#define NoArgTrueFalseFunction(X) \
static PyObject *PyCurses_ ## X (PyObject *self) \
{ \
  PyCursesInitialised \
  if (X () == FALSE) { \
    Py_INCREF(Py_False); \
    return Py_False; \
  } \
  Py_INCREF(Py_True); \
  return Py_True; }

#define NoArgNoReturnVoidFunction(X) \
static PyObject *PyCurses_ ## X (PyObject *self) \
{ \
  PyCursesInitialised \
  X(); \
  Py_INCREF(Py_None); \
  return Py_None; }

#ifdef __cplusplus
}
#endif

#endif /* !defined(Py_CURSES_H) */


