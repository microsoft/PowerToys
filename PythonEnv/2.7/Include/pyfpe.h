#ifndef Py_PYFPE_H
#define Py_PYFPE_H
#ifdef __cplusplus
extern "C" {
#endif
/*
     ---------------------------------------------------------------------
    /                       Copyright (c) 1996.                           \
   |          The Regents of the University of California.                 |
   |                        All rights reserved.                           |
   |                                                                       |
   |   Permission to use, copy, modify, and distribute this software for   |
   |   any purpose without fee is hereby granted, provided that this en-   |
   |   tire notice is included in all copies of any software which is or   |
   |   includes  a  copy  or  modification  of  this software and in all   |
   |   copies of the supporting documentation for such software.           |
   |                                                                       |
   |   This  work was produced at the University of California, Lawrence   |
   |   Livermore National Laboratory under  contract  no.  W-7405-ENG-48   |
   |   between  the  U.S.  Department  of  Energy and The Regents of the   |
   |   University of California for the operation of UC LLNL.              |
   |                                                                       |
   |                              DISCLAIMER                               |
   |                                                                       |
   |   This  software was prepared as an account of work sponsored by an   |
   |   agency of the United States Government. Neither the United States   |
   |   Government  nor the University of California nor any of their em-   |
   |   ployees, makes any warranty, express or implied, or  assumes  any   |
   |   liability  or  responsibility  for the accuracy, completeness, or   |
   |   usefulness of any information,  apparatus,  product,  or  process   |
   |   disclosed,   or  represents  that  its  use  would  not  infringe   |
   |   privately-owned rights. Reference herein to any specific  commer-   |
   |   cial  products,  process,  or  service  by trade name, trademark,   |
   |   manufacturer, or otherwise, does not  necessarily  constitute  or   |
   |   imply  its endorsement, recommendation, or favoring by the United   |
   |   States Government or the University of California. The views  and   |
   |   opinions  of authors expressed herein do not necessarily state or   |
   |   reflect those of the United States Government or  the  University   |
   |   of  California,  and shall not be used for advertising or product   |
    \  endorsement purposes.                                              /
     ---------------------------------------------------------------------
*/

/*
 *       Define macros for handling SIGFPE.
 *       Lee Busby, LLNL, November, 1996
 *       busby1@llnl.gov
 * 
 *********************************************
 * Overview of the system for handling SIGFPE:
 * 
 * This file (Include/pyfpe.h) defines a couple of "wrapper" macros for
 * insertion into your Python C code of choice. Their proper use is
 * discussed below. The file Python/pyfpe.c defines a pair of global
 * variables PyFPE_jbuf and PyFPE_counter which are used by the signal
 * handler for SIGFPE to decide if a particular exception was protected
 * by the macros. The signal handler itself, and code for enabling the
 * generation of SIGFPE in the first place, is in a (new) Python module
 * named fpectl. This module is standard in every respect. It can be loaded
 * either statically or dynamically as you choose, and like any other
 * Python module, has no effect until you import it.
 * 
 * In the general case, there are three steps toward handling SIGFPE in any
 * Python code:
 * 
 * 1) Add the *_PROTECT macros to your C code as required to protect
 *    dangerous floating point sections.
 * 
 * 2) Turn on the inclusion of the code by adding the ``--with-fpectl''
 *    flag at the time you run configure.  If the fpectl or other modules
 *    which use the *_PROTECT macros are to be dynamically loaded, be
 *    sure they are compiled with WANT_SIGFPE_HANDLER defined.
 * 
 * 3) When python is built and running, import fpectl, and execute
 *    fpectl.turnon_sigfpe(). This sets up the signal handler and enables
 *    generation of SIGFPE whenever an exception occurs. From this point
 *    on, any properly trapped SIGFPE should result in the Python
 *    FloatingPointError exception.
 * 
 * Step 1 has been done already for the Python kernel code, and should be
 * done soon for the NumPy array package.  Step 2 is usually done once at
 * python install time. Python's behavior with respect to SIGFPE is not
 * changed unless you also do step 3. Thus you can control this new
 * facility at compile time, or run time, or both.
 * 
 ******************************** 
 * Using the macros in your code:
 * 
 * static PyObject *foobar(PyObject *self,PyObject *args)
 * {
 *     ....
 *     PyFPE_START_PROTECT("Error in foobar", return 0)
 *     result = dangerous_op(somearg1, somearg2, ...);
 *     PyFPE_END_PROTECT(result)
 *     ....
 * }
 * 
 * If a floating point error occurs in dangerous_op, foobar returns 0 (NULL),
 * after setting the associated value of the FloatingPointError exception to
 * "Error in foobar". ``Dangerous_op'' can be a single operation, or a block
 * of code, function calls, or any combination, so long as no alternate
 * return is possible before the PyFPE_END_PROTECT macro is reached.
 * 
 * The macros can only be used in a function context where an error return
 * can be recognized as signaling a Python exception. (Generally, most
 * functions that return a PyObject * will qualify.)
 * 
 * Guido's original design suggestion for PyFPE_START_PROTECT and
 * PyFPE_END_PROTECT had them open and close a local block, with a locally
 * defined jmp_buf and jmp_buf pointer. This would allow recursive nesting
 * of the macros. The Ansi C standard makes it clear that such local
 * variables need to be declared with the "volatile" type qualifier to keep
 * setjmp from corrupting their values. Some current implementations seem
 * to be more restrictive. For example, the HPUX man page for setjmp says
 * 
 *   Upon the return from a setjmp() call caused by a longjmp(), the
 *   values of any non-static local variables belonging to the routine
 *   from which setjmp() was called are undefined. Code which depends on
 *   such values is not guaranteed to be portable.
 * 
 * I therefore decided on a more limited form of nesting, using a counter
 * variable (PyFPE_counter) to keep track of any recursion.  If an exception
 * occurs in an ``inner'' pair of macros, the return will apparently
 * come from the outermost level.
 * 
 */

#ifdef WANT_SIGFPE_HANDLER
#include <signal.h>
#include <setjmp.h>
#include <math.h>
extern jmp_buf PyFPE_jbuf;
extern int PyFPE_counter;
extern double PyFPE_dummy(void *);

#define PyFPE_START_PROTECT(err_string, leave_stmt) \
if (!PyFPE_counter++ && setjmp(PyFPE_jbuf)) { \
	PyErr_SetString(PyExc_FloatingPointError, err_string); \
	PyFPE_counter = 0; \
	leave_stmt; \
}

/*
 * This (following) is a heck of a way to decrement a counter. However,
 * unless the macro argument is provided, code optimizers will sometimes move
 * this statement so that it gets executed *before* the unsafe expression
 * which we're trying to protect.  That pretty well messes things up,
 * of course.
 * 
 * If the expression(s) you're trying to protect don't happen to return a
 * value, you will need to manufacture a dummy result just to preserve the
 * correct ordering of statements.  Note that the macro passes the address
 * of its argument (so you need to give it something which is addressable).
 * If your expression returns multiple results, pass the last such result
 * to PyFPE_END_PROTECT.
 * 
 * Note that PyFPE_dummy returns a double, which is cast to int.
 * This seeming insanity is to tickle the Floating Point Unit (FPU).
 * If an exception has occurred in a preceding floating point operation,
 * some architectures (notably Intel 80x86) will not deliver the interrupt
 * until the *next* floating point operation.  This is painful if you've
 * already decremented PyFPE_counter.
 */
#define PyFPE_END_PROTECT(v) PyFPE_counter -= (int)PyFPE_dummy(&(v));

#else

#define PyFPE_START_PROTECT(err_string, leave_stmt)
#define PyFPE_END_PROTECT(v)

#endif

#ifdef __cplusplus
}
#endif
#endif /* !Py_PYFPE_H */
