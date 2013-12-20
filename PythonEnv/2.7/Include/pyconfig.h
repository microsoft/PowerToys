#ifndef Py_CONFIG_H
#define Py_CONFIG_H

/* pyconfig.h.  NOT Generated automatically by configure.

This is a manually maintained version used for the Watcom,
Borland and Microsoft Visual C++ compilers.  It is a
standard part of the Python distribution.

WINDOWS DEFINES:
The code specific to Windows should be wrapped around one of
the following #defines

MS_WIN64 - Code specific to the MS Win64 API
MS_WIN32 - Code specific to the MS Win32 (and Win64) API (obsolete, this covers all supported APIs)
MS_WINDOWS - Code specific to Windows, but all versions.
MS_WINCE - Code specific to Windows CE
Py_ENABLE_SHARED - Code if the Python core is built as a DLL.

Also note that neither "_M_IX86" or "_MSC_VER" should be used for
any purpose other than "Windows Intel x86 specific" and "Microsoft
compiler specific".  Therefore, these should be very rare.


NOTE: The following symbols are deprecated:
NT, USE_DL_EXPORT, USE_DL_IMPORT, DL_EXPORT, DL_IMPORT
MS_CORE_DLL.

WIN32 is still required for the locale module.

*/

#ifdef _WIN32_WCE
#define MS_WINCE
#endif

/* Deprecated USE_DL_EXPORT macro - please use Py_BUILD_CORE */
#ifdef USE_DL_EXPORT
#	define Py_BUILD_CORE
#endif /* USE_DL_EXPORT */

/* Visual Studio 2005 introduces deprecation warnings for
   "insecure" and POSIX functions. The insecure functions should
   be replaced by *_s versions (according to Microsoft); the
   POSIX functions by _* versions (which, according to Microsoft,
   would be ISO C conforming). Neither renaming is feasible, so
   we just silence the warnings. */

#ifndef _CRT_SECURE_NO_DEPRECATE
#define _CRT_SECURE_NO_DEPRECATE 1
#endif
#ifndef _CRT_NONSTDC_NO_DEPRECATE
#define _CRT_NONSTDC_NO_DEPRECATE 1
#endif

/* Windows CE does not have these */
#ifndef MS_WINCE
#define HAVE_IO_H
#define HAVE_SYS_UTIME_H
#define HAVE_TEMPNAM
#define HAVE_TMPFILE
#define HAVE_TMPNAM
#define HAVE_CLOCK
#define HAVE_STRERROR
#endif

#ifdef HAVE_IO_H
#include <io.h>
#endif

#define HAVE_HYPOT
#define HAVE_STRFTIME
#define DONT_HAVE_SIG_ALARM
#define DONT_HAVE_SIG_PAUSE
#define LONG_BIT	32
#define WORD_BIT 32
#define PREFIX ""
#define EXEC_PREFIX ""

#define MS_WIN32 /* only support win32 and greater. */
#define MS_WINDOWS
#ifndef PYTHONPATH
#	define PYTHONPATH ".\\DLLs;.\\lib;.\\lib\\plat-win;.\\lib\\lib-tk"
#endif
#define NT_THREADS
#define WITH_THREAD
#ifndef NETSCAPE_PI
#define USE_SOCKET
#endif

/* CE6 doesn't have strdup() but _strdup(). Assume the same for earlier versions. */
#if defined(MS_WINCE)
#  include <stdlib.h>
#  define strdup _strdup
#endif

#ifdef MS_WINCE
/* Windows CE does not support environment variables */
#define getenv(v) (NULL)
#define environ (NULL)
#endif

/* Compiler specific defines */

/* ------------------------------------------------------------------------*/
/* Microsoft C defines _MSC_VER */
#ifdef _MSC_VER

/* We want COMPILER to expand to a string containing _MSC_VER's *value*.
 * This is horridly tricky, because the stringization operator only works
 * on macro arguments, and doesn't evaluate macros passed *as* arguments.
 * Attempts simpler than the following appear doomed to produce "_MSC_VER"
 * literally in the string.
 */
#define _Py_PASTE_VERSION(SUFFIX) \
	("[MSC v." _Py_STRINGIZE(_MSC_VER) " " SUFFIX "]")
/* e.g., this produces, after compile-time string catenation,
 * 	("[MSC v.1200 32 bit (Intel)]")
 *
 * _Py_STRINGIZE(_MSC_VER) expands to
 * _Py_STRINGIZE1((_MSC_VER)) expands to
 * _Py_STRINGIZE2(_MSC_VER) but as this call is the result of token-pasting
 *      it's scanned again for macros and so further expands to (under MSVC 6)
 * _Py_STRINGIZE2(1200) which then expands to
 * "1200"
 */
#define _Py_STRINGIZE(X) _Py_STRINGIZE1((X))
#define _Py_STRINGIZE1(X) _Py_STRINGIZE2 ## X
#define _Py_STRINGIZE2(X) #X

/* MSVC defines _WINxx to differentiate the windows platform types

   Note that for compatibility reasons _WIN32 is defined on Win32
   *and* on Win64. For the same reasons, in Python, MS_WIN32 is
   defined on Win32 *and* Win64. Win32 only code must therefore be
   guarded as follows:
   	#if defined(MS_WIN32) && !defined(MS_WIN64)
   Some modules are disabled on Itanium processors, therefore we
   have MS_WINI64 set for those targets, otherwise MS_WINX64
*/
#ifdef _WIN64
#define MS_WIN64
#endif

/* set the COMPILER */
#ifdef MS_WIN64
#if defined(_M_IA64)
#define COMPILER _Py_PASTE_VERSION("64 bit (Itanium)")
#define MS_WINI64
#elif defined(_M_X64) || defined(_M_AMD64)
#define COMPILER _Py_PASTE_VERSION("64 bit (AMD64)")
#define MS_WINX64
#else
#define COMPILER _Py_PASTE_VERSION("64 bit (Unknown)")
#endif
#endif /* MS_WIN64 */

/* set the version macros for the windows headers */
#ifdef MS_WINX64
/* 64 bit only runs on XP or greater */
#define Py_WINVER _WIN32_WINNT_WINXP
#define Py_NTDDI NTDDI_WINXP
#else
/* Python 2.6+ requires Windows 2000 or greater */
#ifdef _WIN32_WINNT_WIN2K
#define Py_WINVER _WIN32_WINNT_WIN2K
#else
#define Py_WINVER 0x0500
#endif
#define Py_NTDDI NTDDI_WIN2KSP4
#endif

/* We only set these values when building Python - we don't want to force
   these values on extensions, as that will affect the prototypes and
   structures exposed in the Windows headers. Even when building Python, we
   allow a single source file to override this - they may need access to
   structures etc so it can optionally use new Windows features if it
   determines at runtime they are available.
*/
#if defined(Py_BUILD_CORE) || defined(Py_BUILD_CORE_MODULE)
#ifndef NTDDI_VERSION
#define NTDDI_VERSION Py_NTDDI
#endif
#ifndef WINVER
#define WINVER Py_WINVER
#endif
#ifndef _WIN32_WINNT
#define _WIN32_WINNT Py_WINVER
#endif
#endif

/* _W64 is not defined for VC6 or eVC4 */
#ifndef _W64
#define _W64
#endif

/* Define like size_t, omitting the "unsigned" */
#ifdef MS_WIN64
typedef __int64 ssize_t;
#else
typedef _W64 int ssize_t;
#endif
#define HAVE_SSIZE_T 1

#if defined(MS_WIN32) && !defined(MS_WIN64)
#ifdef _M_IX86
#define COMPILER _Py_PASTE_VERSION("32 bit (Intel)")
#else
#define COMPILER _Py_PASTE_VERSION("32 bit (Unknown)")
#endif
#endif /* MS_WIN32 && !MS_WIN64 */

typedef int pid_t;

#include <float.h>
#define Py_IS_NAN _isnan
#define Py_IS_INFINITY(X) (!_finite(X) && !_isnan(X))
#define Py_IS_FINITE(X) _finite(X)
#define copysign _copysign
#define hypot _hypot

#endif /* _MSC_VER */

/* define some ANSI types that are not defined in earlier Win headers */
#if defined(_MSC_VER) && _MSC_VER >= 1200
/* This file only exists in VC 6.0 or higher */
#include <basetsd.h>
#endif

/* ------------------------------------------------------------------------*/
/* The Borland compiler defines __BORLANDC__ */
/* XXX These defines are likely incomplete, but should be easy to fix. */
#ifdef __BORLANDC__
#define COMPILER "[Borland]"

#ifdef _WIN32
/* tested with BCC 5.5 (__BORLANDC__ >= 0x0550)
 */

typedef int pid_t;
/* BCC55 seems to understand __declspec(dllimport), it is used in its
   own header files (winnt.h, ...) - so we can do nothing and get the default*/

#undef HAVE_SYS_UTIME_H
#define HAVE_UTIME_H
#define HAVE_DIRENT_H

/* rename a few functions for the Borland compiler */
#include <io.h>
#define _chsize chsize
#define _setmode setmode

#else /* !_WIN32 */
#error "Only Win32 and later are supported"
#endif /* !_WIN32 */

#endif /* BORLANDC */

/* ------------------------------------------------------------------------*/
/* egcs/gnu-win32 defines __GNUC__ and _WIN32 */
#if defined(__GNUC__) && defined(_WIN32)
/* XXX These defines are likely incomplete, but should be easy to fix.
   They should be complete enough to build extension modules. */
/* Suggested by Rene Liebscher <R.Liebscher@gmx.de> to avoid a GCC 2.91.*
   bug that requires structure imports.  More recent versions of the
   compiler don't exhibit this bug.
*/
#if (__GNUC__==2) && (__GNUC_MINOR__<=91)
#warning "Please use an up-to-date version of gcc! (>2.91 recommended)"
#endif

#define COMPILER "[gcc]"
#define hypot _hypot
#define PY_LONG_LONG long long
#define PY_LLONG_MIN LLONG_MIN
#define PY_LLONG_MAX LLONG_MAX
#define PY_ULLONG_MAX ULLONG_MAX
#endif /* GNUC */

/* ------------------------------------------------------------------------*/
/* lcc-win32 defines __LCC__ */
#if defined(__LCC__)
/* XXX These defines are likely incomplete, but should be easy to fix.
   They should be complete enough to build extension modules. */

#define COMPILER "[lcc-win32]"
typedef int pid_t;
/* __declspec() is supported here too - do nothing to get the defaults */

#endif /* LCC */

/* ------------------------------------------------------------------------*/
/* End of compilers - finish up */

#ifndef NO_STDIO_H
#	include <stdio.h>
#endif

/* 64 bit ints are usually spelt __int64 unless compiler has overridden */
#define HAVE_LONG_LONG 1
#ifndef PY_LONG_LONG
#	define PY_LONG_LONG __int64
#	define PY_LLONG_MAX _I64_MAX
#	define PY_LLONG_MIN _I64_MIN
#	define PY_ULLONG_MAX _UI64_MAX
#endif

/* For Windows the Python core is in a DLL by default.  Test
Py_NO_ENABLE_SHARED to find out.  Also support MS_NO_COREDLL for b/w compat */
#if !defined(MS_NO_COREDLL) && !defined(Py_NO_ENABLE_SHARED)
#	define Py_ENABLE_SHARED 1 /* standard symbol for shared library */
#	define MS_COREDLL	/* deprecated old symbol */
#endif /* !MS_NO_COREDLL && ... */

/*  All windows compilers that use this header support __declspec */
#define HAVE_DECLSPEC_DLL

/* For an MSVC DLL, we can nominate the .lib files used by extensions */
#ifdef MS_COREDLL
#	ifndef Py_BUILD_CORE /* not building the core - must be an ext */
#		if defined(_MSC_VER)
			/* So MSVC users need not specify the .lib file in
			their Makefile (other compilers are generally
			taken care of by distutils.) */
#			ifdef _DEBUG
#				pragma comment(lib,"python27_d.lib")
#			else
#				pragma comment(lib,"python27.lib")
#			endif /* _DEBUG */
#		endif /* _MSC_VER */
#	endif /* Py_BUILD_CORE */
#endif /* MS_COREDLL */

#if defined(MS_WIN64)
/* maintain "win32" sys.platform for backward compatibility of Python code,
   the Win64 API should be close enough to the Win32 API to make this
   preferable */
#	define PLATFORM "win32"
#	define SIZEOF_VOID_P 8
#	define SIZEOF_TIME_T 8
#	define SIZEOF_OFF_T 4
#	define SIZEOF_FPOS_T 8
#	define SIZEOF_HKEY 8
#	define SIZEOF_SIZE_T 8
/* configure.ac defines HAVE_LARGEFILE_SUPPORT iff HAVE_LONG_LONG,
   sizeof(off_t) > sizeof(long), and sizeof(PY_LONG_LONG) >= sizeof(off_t).
   On Win64 the second condition is not true, but if fpos_t replaces off_t
   then this is true. The uses of HAVE_LARGEFILE_SUPPORT imply that Win64
   should define this. */
#	define HAVE_LARGEFILE_SUPPORT
#elif defined(MS_WIN32)
#	define PLATFORM "win32"
#	define HAVE_LARGEFILE_SUPPORT
#	define SIZEOF_VOID_P 4
#	define SIZEOF_OFF_T 4
#	define SIZEOF_FPOS_T 8
#	define SIZEOF_HKEY 4
#	define SIZEOF_SIZE_T 4
	/* MS VS2005 changes time_t to an 64-bit type on all platforms */
#	if defined(_MSC_VER) && _MSC_VER >= 1400
#	define SIZEOF_TIME_T 8
#	else
#	define SIZEOF_TIME_T 4
#	endif
#endif

#ifdef _DEBUG
#	define Py_DEBUG
#endif


#ifdef MS_WIN32

#define SIZEOF_SHORT 2
#define SIZEOF_INT 4
#define SIZEOF_LONG 4
#define SIZEOF_LONG_LONG 8
#define SIZEOF_DOUBLE 8
#define SIZEOF_FLOAT 4

/* VC 7.1 has them and VC 6.0 does not.  VC 6.0 has a version number of 1200.
   Microsoft eMbedded Visual C++ 4.0 has a version number of 1201 and doesn't
   define these.
   If some compiler does not provide them, modify the #if appropriately. */
#if defined(_MSC_VER)
#if _MSC_VER > 1300
#define HAVE_UINTPTR_T 1
#define HAVE_INTPTR_T 1
#else
/* VC6, VS 2002 and eVC4 don't support the C99 LL suffix for 64-bit integer literals */
#define Py_LL(x) x##I64
#endif  /* _MSC_VER > 1200  */
#endif  /* _MSC_VER */

#endif

/* define signed and unsigned exact-width 32-bit and 64-bit types, used in the
   implementation of Python long integers. */
#ifndef PY_UINT32_T
#if SIZEOF_INT == 4
#define HAVE_UINT32_T 1
#define PY_UINT32_T unsigned int
#elif SIZEOF_LONG == 4
#define HAVE_UINT32_T 1
#define PY_UINT32_T unsigned long
#endif
#endif

#ifndef PY_UINT64_T
#if SIZEOF_LONG_LONG == 8
#define HAVE_UINT64_T 1
#define PY_UINT64_T unsigned PY_LONG_LONG
#endif
#endif

#ifndef PY_INT32_T
#if SIZEOF_INT == 4
#define HAVE_INT32_T 1
#define PY_INT32_T int
#elif SIZEOF_LONG == 4
#define HAVE_INT32_T 1
#define PY_INT32_T long
#endif
#endif

#ifndef PY_INT64_T
#if SIZEOF_LONG_LONG == 8
#define HAVE_INT64_T 1
#define PY_INT64_T PY_LONG_LONG
#endif
#endif

/* Fairly standard from here! */

/* Define to 1 if you have the `copysign' function. */
#define HAVE_COPYSIGN 1

/* Define to 1 if you have the `isinf' macro. */
#define HAVE_DECL_ISINF 1

/* Define to 1 if you have the `isnan' function. */
#define HAVE_DECL_ISNAN 1

/* Define if on AIX 3.
   System headers sometimes define this.
   We just want to avoid a redefinition error message.  */
#ifndef _ALL_SOURCE
/* #undef _ALL_SOURCE */
#endif

/* Define to empty if the keyword does not work.  */
/* #define const  */

/* Define to 1 if you have the <conio.h> header file. */
#ifndef MS_WINCE
#define HAVE_CONIO_H 1
#endif

/* Define to 1 if you have the <direct.h> header file. */
#ifndef MS_WINCE
#define HAVE_DIRECT_H 1
#endif

/* Define if you have dirent.h.  */
/* #define DIRENT 1 */

/* Define to the type of elements in the array set by `getgroups'.
   Usually this is either `int' or `gid_t'.  */
/* #undef GETGROUPS_T */

/* Define to `int' if <sys/types.h> doesn't define.  */
/* #undef gid_t */

/* Define if your struct tm has tm_zone.  */
/* #undef HAVE_TM_ZONE */

/* Define if you don't have tm_zone but do have the external array
   tzname.  */
#define HAVE_TZNAME

/* Define to `int' if <sys/types.h> doesn't define.  */
/* #undef mode_t */

/* Define if you don't have dirent.h, but have ndir.h.  */
/* #undef NDIR */

/* Define to `long' if <sys/types.h> doesn't define.  */
/* #undef off_t */

/* Define to `int' if <sys/types.h> doesn't define.  */
/* #undef pid_t */

/* Define if the system does not provide POSIX.1 features except
   with this defined.  */
/* #undef _POSIX_1_SOURCE */

/* Define if you need to in order for stat and other things to work.  */
/* #undef _POSIX_SOURCE */

/* Define as the return type of signal handlers (int or void).  */
#define RETSIGTYPE void

/* Define to `unsigned' if <sys/types.h> doesn't define.  */
/* #undef size_t */

/* Define if you have the ANSI C header files.  */
#define STDC_HEADERS 1

/* Define if you don't have dirent.h, but have sys/dir.h.  */
/* #undef SYSDIR */

/* Define if you don't have dirent.h, but have sys/ndir.h.  */
/* #undef SYSNDIR */

/* Define if you can safely include both <sys/time.h> and <time.h>.  */
/* #undef TIME_WITH_SYS_TIME */

/* Define if your <sys/time.h> declares struct tm.  */
/* #define TM_IN_SYS_TIME 1 */

/* Define to `int' if <sys/types.h> doesn't define.  */
/* #undef uid_t */

/* Define if the closedir function returns void instead of int.  */
/* #undef VOID_CLOSEDIR */

/* Define if getpgrp() must be called as getpgrp(0)
   and (consequently) setpgrp() as setpgrp(0, 0). */
/* #undef GETPGRP_HAVE_ARGS */

/* Define this if your time.h defines altzone */
/* #define HAVE_ALTZONE */

/* Define if you have the putenv function.  */
#ifndef MS_WINCE
#define HAVE_PUTENV
#endif

/* Define if your compiler supports function prototypes */
#define HAVE_PROTOTYPES

/* Define if  you can safely include both <sys/select.h> and <sys/time.h>
   (which you can't on SCO ODT 3.0). */
/* #undef SYS_SELECT_WITH_SYS_TIME */

/* Define if you want documentation strings in extension modules */
#define WITH_DOC_STRINGS 1

/* Define if you want to compile in rudimentary thread support */
/* #undef WITH_THREAD */

/* Define if you want to use the GNU readline library */
/* #define WITH_READLINE 1 */

/* Define if you want to have a Unicode type. */
#define Py_USING_UNICODE

/* Define as the size of the unicode type. */
/* This is enough for unicodeobject.h to do the "right thing" on Windows. */
#define Py_UNICODE_SIZE 2

/* Use Python's own small-block memory-allocator. */
#define WITH_PYMALLOC 1

/* Define if you have clock.  */
/* #define HAVE_CLOCK */

/* Define when any dynamic module loading is enabled */
#define HAVE_DYNAMIC_LOADING

/* Define if you have ftime.  */
#ifndef MS_WINCE
#define HAVE_FTIME
#endif

/* Define if you have getpeername.  */
#define HAVE_GETPEERNAME

/* Define if you have getpgrp.  */
/* #undef HAVE_GETPGRP */

/* Define if you have getpid.  */
#ifndef MS_WINCE
#define HAVE_GETPID
#endif

/* Define if you have gettimeofday.  */
/* #undef HAVE_GETTIMEOFDAY */

/* Define if you have getwd.  */
/* #undef HAVE_GETWD */

/* Define if you have lstat.  */
/* #undef HAVE_LSTAT */

/* Define if you have the mktime function.  */
#define HAVE_MKTIME

/* Define if you have nice.  */
/* #undef HAVE_NICE */

/* Define if you have readlink.  */
/* #undef HAVE_READLINK */

/* Define if you have select.  */
/* #undef HAVE_SELECT */

/* Define if you have setpgid.  */
/* #undef HAVE_SETPGID */

/* Define if you have setpgrp.  */
/* #undef HAVE_SETPGRP */

/* Define if you have setsid.  */
/* #undef HAVE_SETSID */

/* Define if you have setvbuf.  */
#define HAVE_SETVBUF

/* Define if you have siginterrupt.  */
/* #undef HAVE_SIGINTERRUPT */

/* Define if you have symlink.  */
/* #undef HAVE_SYMLINK */

/* Define if you have tcgetpgrp.  */
/* #undef HAVE_TCGETPGRP */

/* Define if you have tcsetpgrp.  */
/* #undef HAVE_TCSETPGRP */

/* Define if you have times.  */
/* #undef HAVE_TIMES */

/* Define if you have uname.  */
/* #undef HAVE_UNAME */

/* Define if you have waitpid.  */
/* #undef HAVE_WAITPID */

/* Define to 1 if you have the `wcscoll' function. */
#ifndef MS_WINCE
#define HAVE_WCSCOLL 1
#endif

/* Define if the zlib library has inflateCopy */
#define HAVE_ZLIB_COPY 1

/* Define if you have the <dlfcn.h> header file.  */
/* #undef HAVE_DLFCN_H */

/* Define to 1 if you have the <errno.h> header file. */
#ifndef MS_WINCE
#define HAVE_ERRNO_H 1
#endif

/* Define if you have the <fcntl.h> header file.  */
#ifndef MS_WINCE
#define HAVE_FCNTL_H 1
#endif

/* Define to 1 if you have the <process.h> header file. */
#ifndef MS_WINCE
#define HAVE_PROCESS_H 1
#endif

/* Define to 1 if you have the <signal.h> header file. */
#ifndef MS_WINCE
#define HAVE_SIGNAL_H 1
#endif

/* Define if you have the <stdarg.h> prototypes.  */
#define HAVE_STDARG_PROTOTYPES

/* Define if you have the <stddef.h> header file.  */
#define HAVE_STDDEF_H 1

/* Define if you have the <sys/audioio.h> header file.  */
/* #undef HAVE_SYS_AUDIOIO_H */

/* Define if you have the <sys/param.h> header file.  */
/* #define HAVE_SYS_PARAM_H 1 */

/* Define if you have the <sys/select.h> header file.  */
/* #define HAVE_SYS_SELECT_H 1 */

/* Define to 1 if you have the <sys/stat.h> header file.  */
#ifndef MS_WINCE
#define HAVE_SYS_STAT_H 1
#endif

/* Define if you have the <sys/time.h> header file.  */
/* #define HAVE_SYS_TIME_H 1 */

/* Define if you have the <sys/times.h> header file.  */
/* #define HAVE_SYS_TIMES_H 1 */

/* Define to 1 if you have the <sys/types.h> header file.  */
#ifndef MS_WINCE
#define HAVE_SYS_TYPES_H 1
#endif

/* Define if you have the <sys/un.h> header file.  */
/* #define HAVE_SYS_UN_H 1 */

/* Define if you have the <sys/utime.h> header file.  */
/* #define HAVE_SYS_UTIME_H 1 */

/* Define if you have the <sys/utsname.h> header file.  */
/* #define HAVE_SYS_UTSNAME_H 1 */

/* Define if you have the <thread.h> header file.  */
/* #undef HAVE_THREAD_H */

/* Define if you have the <unistd.h> header file.  */
/* #define HAVE_UNISTD_H 1 */

/* Define if you have the <utime.h> header file.  */
/* #define HAVE_UTIME_H 1 */

/* Define if the compiler provides a wchar.h header file. */
#define HAVE_WCHAR_H 1

/* Define if you have the dl library (-ldl).  */
/* #undef HAVE_LIBDL */

/* Define if you have the mpc library (-lmpc).  */
/* #undef HAVE_LIBMPC */

/* Define if you have the nsl library (-lnsl).  */
#define HAVE_LIBNSL 1

/* Define if you have the seq library (-lseq).  */
/* #undef HAVE_LIBSEQ */

/* Define if you have the socket library (-lsocket).  */
#define HAVE_LIBSOCKET 1

/* Define if you have the sun library (-lsun).  */
/* #undef HAVE_LIBSUN */

/* Define if you have the termcap library (-ltermcap).  */
/* #undef HAVE_LIBTERMCAP */

/* Define if you have the termlib library (-ltermlib).  */
/* #undef HAVE_LIBTERMLIB */

/* Define if you have the thread library (-lthread).  */
/* #undef HAVE_LIBTHREAD */

/* WinSock does not use a bitmask in select, and uses
   socket handles greater than FD_SETSIZE */
#define Py_SOCKET_FD_CAN_BE_GE_FD_SETSIZE

/* Define if C doubles are 64-bit IEEE 754 binary format, stored with the
   least significant byte first */
#define DOUBLE_IS_LITTLE_ENDIAN_IEEE754 1

#endif /* !Py_CONFIG_H */
