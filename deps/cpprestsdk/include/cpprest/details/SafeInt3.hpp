/*-----------------------------------------------------------------------------------------------------------
SafeInt.hpp
Version 3.0.18p

This software is licensed under the Microsoft Public License (Ms-PL).
For more information about Microsoft open source licenses, refer to
http://www.microsoft.com/opensource/licenses.mspx

This license governs use of the accompanying software. If you use the software, you accept this license.
If you do not accept the license, do not use the software.

Definitions
The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here
as under U.S. copyright law. A "contribution" is the original software, or any additions or changes to
the software. A "contributor" is any person that distributes its contribution under this license.
"Licensed patents" are a contributor's patent claims that read directly on its contribution.

Grant of Rights
(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations
in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to
reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution
or any derivative works that you create.

(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in
section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed
patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution
in the software or derivative works of the contribution in the software.

Conditions and Limitations
(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo,
    or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the
    software, your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and
    attribution notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license
    by including a complete copy of this license with your distribution. If you distribute any portion of the
    software in compiled or object code form, you may only do so under a license that complies with this license.
(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties,
    guarantees, or conditions. You may have additional consumer rights under your local laws which this license
    cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties
    of merchantability, fitness for a particular purpose and non-infringement.


Copyright (c) Microsoft Corporation.  All rights reserved.

This header implements an integer handling class designed to catch
unsafe integer operations

This header compiles properly at Wall on Visual Studio, -Wall on gcc, and -Weverything on clang.

Please read the leading comments before using the class.
---------------------------------------------------------------*/
#pragma once

// It is a bit tricky to sort out what compiler we are actually using,
// do this once here, and avoid cluttering the code
#define VISUAL_STUDIO_COMPILER 0
#define CLANG_COMPILER 1
#define GCC_COMPILER 2
#define UNKNOWN_COMPILER -1

// Clang will sometimes pretend to be Visual Studio
// and does pretend to be gcc. Check it first, as nothing else pretends to be clang
#if defined __clang__
#define SAFEINT_COMPILER CLANG_COMPILER
#elif defined __GNUC__
#define SAFEINT_COMPILER GCC_COMPILER
#elif defined _MSC_VER
#define SAFEINT_COMPILER VISUAL_STUDIO_COMPILER
#else
#define SAFEINT_COMPILER UNKNOWN_COMPILER
#endif

// Enable compiling with /Wall under VC
#if SAFEINT_COMPILER == VISUAL_STUDIO_COMPILER
#pragma warning(push)
// Disable warnings coming from headers
#pragma warning(disable : 4987 4820 4987 4820)

#endif

// Need this for ptrdiff_t on some compilers
#include <cstddef>
#include <cstdlib>

#if SAFEINT_COMPILER == VISUAL_STUDIO_COMPILER && defined _M_AMD64
#include <intrin.h>
#define SAFEINT_USE_INTRINSICS 1
#else
#define SAFEINT_USE_INTRINSICS 0
#endif

#if SAFEINT_COMPILER == VISUAL_STUDIO_COMPILER
#pragma warning(pop)
#endif

// Various things needed for GCC
#if SAFEINT_COMPILER == GCC_COMPILER || SAFEINT_COMPILER == CLANG_COMPILER

#define NEEDS_INT_DEFINED

#if !defined NULL
#define NULL 0
#endif

// GCC warning suppression
#if SAFEINT_COMPILER == GCC_COMPILER
#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wunused-local-typedefs"
#endif

#include <stdint.h>

// clang only
#if SAFEINT_COMPILER == CLANG_COMPILER

#if __has_feature(cxx_nullptr)
#define NEEDS_NULLPTR_DEFINED 0
#endif

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wc++11-long-long"
#pragma clang diagnostic ignored "-Wold-style-cast"
#pragma clang diagnostic ignored "-Wunused-local-typedef"
#endif

#endif

// If the user made a choice, respect it #if !defined
#if !defined NEEDS_NULLPTR_DEFINED
// Visual Studio 2010 and higher support this
#if SAFEINT_COMPILER == VISUAL_STUDIO_COMPILER
#if (_MSC_VER < 1600)
#define NEEDS_NULLPTR_DEFINED 1
#else
#define NEEDS_NULLPTR_DEFINED 0
#endif
#else
// Let everything else trigger based on whether we use c++11 or above
#if __cplusplus >= 201103L
#define NEEDS_NULLPTR_DEFINED 0
#else
#define NEEDS_NULLPTR_DEFINED 1
#endif
#endif
#endif

#if NEEDS_NULLPTR_DEFINED
#define nullptr NULL
#endif

#ifndef C_ASSERT
#define C_ASSERT_DEFINED_SAFEINT
#define C_ASSERT(e) typedef char __C_ASSERT__[(e) ? 1 : -1]
#endif

// Let's test some assumptions
// We're assuming two's complement negative numbers
C_ASSERT(-1 == static_cast<int>(0xffffffff));

/************* Compiler Options
*****************************************************************************************************

SafeInt supports several compile-time options that can change the behavior of the class.

Compiler options:
SAFEINT_WARN_64BIT_PORTABILITY     - this re-enables various warnings that happen when /Wp64 is used. Enabling this
option is not recommended. NEEDS_INT_DEFINED                  - if your compiler does not support __int8, __int16,
__int32 and __int64, you can enable this. SAFEINT_ASSERT_ON_EXCEPTION        - it is often easier to stop on an assert
and figure out a problem than to try and figure out how you landed in the catch block. SafeIntDefaultExceptionHandler -
if you'd like to replace the exception handlers SafeInt provides, define your replacement and define this. Note - two
built in (Windows-specific) options exist:
                                     - SAFEINT_FAILFAST - bypasses all exception handlers, exits the app with an
exception
                                     - SAFEINT_RAISE_EXCEPTION - throws Win32 exceptions, which can be caught
SAFEINT_DISALLOW_UNSIGNED_NEGATION - Invoking the unary negation operator creates warnings, but if you'd like it to
completely fail to compile, define this. ANSI_CONVERSIONS                   - This changes the class to use default
comparison behavior, which may be unsafe. Enabling this option is not recommended. SAFEINT_DISABLE_BINARY_ASSERT      -
binary AND, OR or XOR operations on mixed size types can produce unexpected results. If you do this, the default is to
assert. Set this if you prefer not to assert under these conditions. SIZE_T_CAST_NEEDED                 - some compilers
complain if there is not a cast to size_t, others complain if there is one. This lets you not have your compiler
complain. SAFEINT_DISABLE_SHIFT_ASSERT       - Set this option if you don't want to assert when shifting more bits than
the type has. Enabling this option is not recommended.

************************************************************************************************************************************/

/*
*  The SafeInt class is designed to have as low an overhead as possible
*  while still ensuring that all integer operations are conducted safely.
*  Nearly every operator has been overloaded, with a very few exceptions.
*
*  A usability-safety trade-off has been made to help ensure safety. This
*  requires that every operation return either a SafeInt or a bool. If we
*  allowed an operator to return a base integer type T, then the following
*  can happen:
*
*  char i = SafeInt<char>(32) * 2 + SafeInt<char>(16) * 4;
*
*  The * operators take precedence, get overloaded, return a char, and then
*  you have:
*
*  char i = (char)64 + (char)64; //overflow!
*
*  This situation would mean that safety would depend on usage, which isn't
*  acceptable.
*
*  One key operator that is missing is an implicit cast to type T. The reason for
*  this is that if there is an implicit cast operator, then we end up with
*  an ambiguous compile-time precedence. Because of this amiguity, there
*  are two methods that are provided:
*
*  Casting operators for every native integer type
*  Version 3 note - it now compiles correctly for size_t without warnings
*
*  SafeInt::Ptr()   - returns the address of the internal integer
*  Note - the '&' (address of) operator has been overloaded and returns
*         the address of the internal integer.
*
*  The SafeInt class should be used in any circumstances where ensuring
*  integrity of the calculations is more important than performance. See Performance
*  Notes below for additional information.
*
*  Many of the conditionals will optimize out or be inlined for a release
*  build (especially with /Ox), but it does have significantly more overhead,
*  especially for signed numbers. If you do not _require_ negative numbers, use
*  unsigned integer types - certain types of problems cannot occur, and this class
*  performs most efficiently.
*
*  Here's an example of when the class should ideally be used -
*
*  void* AllocateMemForStructs(int StructSize, int HowMany)
*  {
*     SafeInt<unsigned long> s(StructSize);
*
*     s *= HowMany;
*
*     return malloc(s);
*
*  }
*
*  Here's when it should NOT be used:
*
*  void foo()
*  {
*    int i;
*
*    for(i = 0; i < 0xffff; i++)
*      ....
*  }
*
*  Error handling - a SafeInt class will throw exceptions if something
*  objectionable happens. The exceptions are SafeIntException classes,
*  which contain an enum as a code.
*
*  Typical usage might be:
*
*  bool foo()
*  {
*    SafeInt<unsigned long> s; //note that s == 0 unless set
*
*    try{
*      s *= 23;
*      ....
*    }
*    catch(SafeIntException err)
*    {
*       //handle errors here
*    }
*  }
*
*  Update for 3.0 - the exception class is now a template parameter.
*  You can replace the exception class with any exception class you like. This is accomplished by:
*  1) Create a class that has the following interface:
*
    template <> class YourSafeIntExceptionHandler < YourException >
    {
    public:
        static __declspec(noreturn) void __stdcall SafeIntOnOverflow()
        {
            throw YourException( YourSafeIntArithmeticOverflowError );
        }

        static __declspec(noreturn) void __stdcall SafeIntOnDivZero()
        {
            throw YourException( YourSafeIntDivideByZeroError );
        }
    };
*
*  Note that you don't have to throw C++ exceptions, you can throw Win32 exceptions, or do
*  anything you like, just don't return from the call back into the code.
*
*  2) Either explicitly declare SafeInts like so:
*     SafeInt< int, YourSafeIntExceptionHandler > si;
*  or
*     #define SafeIntDefaultExceptionHandler YourSafeIntExceptionHandler
*
*  Performance:
*
*  Due to the highly nested nature of this class, you can expect relatively poor
*  performance in unoptimized code. In tests of optimized code vs. correct inline checks
*  in native code, this class has been found to take approximately 8% more CPU time (this varies),
*  most of which is due to exception handling. Solutions:
*
*  1) Compile optimized code - /Ox is best, /O2 also performs well. Interestingly, /O1
*     (optimize for size) does not work as well.
*  2) If that 8% hit is really a serious problem, walk through the code and inline the
*     exact same checks as the class uses.
*  3) Some operations are more difficult than others - avoid using signed integers, and if
*     possible keep them all the same size. 64-bit integers are also expensive. Mixing
*     different integer sizes and types may prove expensive. Be aware that literals are
*     actually ints. For best performance, cast literals to the type desired.
*
*
*  Performance update
*  The current version of SafeInt uses template specialization to force the compiler to invoke only the
*  operator implementation needed for any given pair of types. This will dramatically improve the perf
*  of debug builds.
*
*  3.0 update - not only have we maintained the specialization, there were some cases that were overly complex,
*  and using some additional cases (e.g. signed __int64 and unsigned __int64) resulted in some simplification.
*  Additionally, there was a lot of work done to better optimize the 64-bit multiplication.
*
*  Binary Operators
*
*  All of the binary operators have certain assumptions built into the class design.
*  This is to ensure correctness. Notes on each class of operator follow:
*
*  Arithmetic Operators (*,/,+,-,%)
*  There are three possible variants:
*  SafeInt< T, E > op SafeInt< T, E >
*  SafeInt< T, E > op U
*  U op SafeInt< T, E >
*
*  The SafeInt< T, E > op SafeInt< U, E > variant is explicitly not supported, and if you try to do
*  this the compiler with throw the following error:
*
*  error C2593: 'operator *' is ambiguous
*
*  This is because the arithmetic operators are required to return a SafeInt of some type.
*  The compiler cannot know whether you'd prefer to get a type T or a type U returned. If
*  you need to do this, you need to extract the value contained within one of the two using
*  the casting operator. For example:
*
*  SafeInt< T, E > t, result;
*  SafeInt< U, E > u;
*
*  result = t * (U)u;
*
*  Comparison Operators
*  Because each of these operators return type bool, mixing SafeInts of differing types is
*  allowed.
*
*  Shift Operators
*  Shift operators always return the type on the left hand side of the operator. Mixed type
*  operations are allowed because the return type is always known.
*
*  Boolean Operators
*  Like comparison operators, these overloads always return type bool, and mixed-type SafeInts
*  are allowed. Additionally, specific overloads exist for type bool on both sides of the
*  operator.
*
*  Binary Operators
*  Mixed-type operations are discouraged, however some provision has been made in order to
*  enable things like:
*
*  SafeInt<char> c = 2;
*
*  if(c & 0x02)
*    ...
*
*  The "0x02" is actually an int, and it needs to work.
*  In the case of binary operations on integers smaller than 32-bit, or of mixed type, corner
*  cases do exist where you could get unexpected results. In any case where SafeInt returns a different
*  result than the underlying operator, it will call assert(). You should examine your code and cast things
*  properly so that you are not programming with side effects.
*
*  Documented issues:
*
*  This header compiles correctly at /W4 using VC++ 8 (Version 14.00.50727.42) and later.
*  As of this writing, I believe it will also work for VC 7.1, but not for VC 7.0 or below.
*  If you need a version that will work with lower level compilers, try version 1.0.7. None
*  of them work with Visual C++ 6, and gcc didn't work very well, either, though this hasn't
*  been tried recently.
*
*  It is strongly recommended that any code doing integer manipulation be compiled at /W4
*  - there are a number of warnings which pertain to integer manipulation enabled that are
*  not enabled at /W3 (default for VC++)
*
*  Perf note - postfix operators are slightly more costly than prefix operators.
*  Unless you're actually assigning it to something, ++SafeInt is less expensive than SafeInt++
*
*  The comparison operator behavior in this class varies from the ANSI definition, which is
*  arguably broken. As an example, consider the following:
*
*  unsigned int l = 0xffffffff;
*  char c = -1;
*
*  if(c == l)
*    printf("Why is -1 equal to 4 billion???\n");
*
*  The problem here is that c gets cast to an int, now has a value of 0xffffffff, and then gets
*  cast again to an unsigned int, losing the true value. This behavior is despite the fact that
*  an __int64 exists, and the following code will yield a different (and intuitively correct)
*  answer:
*
*  if((__int64)c == (__int64)l))
*    printf("Why is -1 equal to 4 billion???\n");
*  else
*    printf("Why doesn't the compiler upcast to 64-bits when needed?\n");
*
*  Note that combinations with smaller integers won't display the problem - if you
*  changed "unsigned int" above to "unsigned short", you'd get the right answer.
*
*  If you prefer to retain the ANSI standard behavior insert
*  #define ANSI_CONVERSIONS
*  into your source. Behavior differences occur in the following cases:
*  8, 16, and 32-bit signed int, unsigned 32-bit int
*  any signed int, unsigned 64-bit int
*  Note - the signed int must be negative to show the problem
*
*
*  Revision history:
*
*  Oct 12, 2003 - Created
*  Author - David LeBlanc - dleblanc@microsoft.com
*
*  Oct 27, 2003 - fixed numerous items pointed out by michmarc and bdawson
*  Dec 28, 2003 - 1.0
*                 added support for mixed-type operations
*                 thanks to vikramh
*                 also fixed broken __int64 multiplication section
*                 added extended support for mixed-type operations where possible
*  Jan 28, 2004 - 1.0.1
*                 changed WCHAR to wchar_t
*                 fixed a construct in two mixed-type assignment overloads that was
*                 not compiling on some compilers
*                 Also changed name of private method to comply with standards on
*                 reserved names
*                 Thanks to Niels Dekker for the input
*  Feb 12, 2004 - 1.0.2
*                 Minor changes to remove dependency on Windows headers
*                 Consistently used __int16, __int32 and __int64 to ensure
*                 portability
*  May 10, 2004 - 1.0.3
*                 Corrected bug in one case of GreaterThan
*  July 22, 2004 - 1.0.4
*                 Tightened logic in addition check (saving 2 instructions)
*                 Pulled error handler out into function to enable user-defined replacement
*                 Made internal type of SafeIntException an enum (as per Niels' suggestion)
*                 Added casts for base integer types (as per Scott Meyers' suggestion)
*                 Updated usage information - see important new perf notes.
*                 Cleaned up several const issues (more thanks to Niels)
*
*  Oct 1, 2004 - 1.0.5
*                 Added support for SEH exceptions instead of C++ exceptions - Win32 only
*                 Made handlers for DIV0 and overflows individually overridable
*                 Commented out the destructor - major perf gains here
*                 Added cast operator for type long, since long != __int32
*                  Corrected a couple of missing const modifiers
*                 Fixed broken >= and <= operators for type U op SafeInt< T, E >
*  Nov 5, 2004 - 1.0.6
*                 Implemented new logic in binary operators to resolve issues with
*                 implicit casts
*                 Fixed casting operator because char != signed char
*                 Defined __int32 as int instead of long
*                 Removed unsafe SafeInt::Value method
*                 Re-implemented casting operator as a result of removing Value method
*  Dec 1, 2004 - 1.0.7
*                 Implemented specialized operators for pointer arithmetic
*                 Created overloads for cases of U op= SafeInt. What you do with U
*                 after that may be dangerous.
*                 Fixed bug in corner case of MixedSizeModulus
*                 Fixed bug in MixedSizeMultiply and MixedSizeDivision with input of 0
*                 Added throw() decorations
*
*  Apr 12, 2005 - 2.0
*                 Extensive revisions to leverage template specialization.
*  April, 2007    Extensive revisions for version 3.0
*  Nov 22, 2009   Forked from MS internal code
*                 Changes needed to support gcc compiler - many thanks to Niels Dekker
*                 for determining not just the issues, but also suggesting fixes.
*                 Also updating some of the header internals to be the same as the upcoming Visual Studio version.
*
*  Jan 16, 2010   64-bit gcc has long == __int64, which means that many of the existing 64-bit
*                 templates are over-specialized. This forces a redefinition of all the 64-bit
*                 multiplication routines to use pointers instead of references for return
*                 values. Also, let's use some intrinsics for x64 Microsoft compiler to
*                 reduce code size, and hopefully improve efficiency.
*
*  June 21, 2014  Better support for clang, higher warning levels supported for all 3 primary supported
                  compilers (Visual Studio, clang, gcc).
                  Also started to converge the code base such that the public CodePlex version will
                  be a drop-in replacement for the Visual Studio version.

*  Note about code style - throughout this class, casts will be written using C-style (T),
*  not C++ style static_cast< T >. This is because the class is nearly always dealing with integer
*  types, and in this case static_cast and a C cast are equivalent. Given the large number of casts,
*  the code is a little more readable this way. In the event a cast is needed where static_cast couldn't
*  be substituted, we'll use the new templatized cast to make it explicit what the operation is doing.
*
************************************************************************************************************
* Version 3.0 changes:
*
* 1) The exception type thrown is now replacable, and you can throw your own exception types. This should help
*    those using well-developed exception classes.
* 2) The 64-bit multiplication code has had a lot of perf work done, and should be faster than 2.0.
* 3) There is now limited floating point support. You can initialize a SafeInt with a floating point type,
*    and you can cast it out (or assign) to a float as well.
* 4) There is now an Align method. I noticed people use this a lot, and rarely check errors, so now you have one.
*
* Another major improvement is the addition of external functions - if you just want to check an operation, this can now
happen:
* All of the following can be invoked without dealing with creating a class, or managing exceptions. This is especially
handy
* for 64-bit porting, since SafeCast compiles away for a 32-bit cast from size_t to unsigned long, but checks it for
64-bit.
*
* inline bool SafeCast( const T From, U& To ) throw()
* inline bool SafeEquals( const T t, const U u ) throw()
* inline bool SafeNotEquals( const T t, const U u ) throw()
* inline bool SafeGreaterThan( const T t, const U u ) throw()
* inline bool SafeGreaterThanEquals( const T t, const U u ) throw()
* inline bool SafeLessThan( const T t, const U u ) throw()
* inline bool SafeLessThanEquals( const T t, const U u ) throw()
* inline bool SafeModulus( const T& t, const U& u, T& result ) throw()
* inline bool SafeMultiply( T t, U u, T& result ) throw()
* inline bool SafeDivide( T t, U u, T& result ) throw()
* inline bool SafeAdd( T t, U u, T& result ) throw()
* inline bool SafeSubtract( T t, U u, T& result ) throw()
*
*/

// use these if the compiler does not support _intXX
#ifdef NEEDS_INT_DEFINED
#define __int8 char
#define __int16 short
#define __int32 int
#define __int64 long long
#endif

namespace msl
{
namespace safeint3
{
// catch these to handle errors
// Currently implemented code values:
// ERROR_ARITHMETIC_OVERFLOW
// EXCEPTION_INT_DIVIDE_BY_ZERO
enum SafeIntError
{
    SafeIntNoError = 0,
    SafeIntArithmeticOverflow,
    SafeIntDivideByZero
};

} // namespace safeint3
} // namespace msl

/*
 * Error handler classes
 * Using classes to deal with exceptions is going to allow the most
 * flexibility, and we can mix different error handlers in the same project
 * or even the same file. It isn't advisable to do this in the same function
 * because a SafeInt< int, MyExceptionHandler > isn't the same thing as
 * SafeInt< int, YourExceptionHander >.
 * If for some reason you have to translate between the two, cast one of them back to its
 * native type.
 *
 * To use your own exception class with SafeInt, first create your exception class,
 * which may look something like the SafeIntException class below. The second step is to
 * create a template specialization that implements SafeIntOnOverflow and SafeIntOnDivZero.
 * For example:
 *
 * template <> class SafeIntExceptionHandler < YourExceptionClass >
 * {
 *     static __declspec(noreturn) void __stdcall SafeIntOnOverflow()
 *     {
 *         throw YourExceptionClass( EXCEPTION_INT_OVERFLOW );
 *     }
 *
 *     static __declspec(noreturn) void __stdcall SafeIntOnDivZero()
 *     {
 *         throw YourExceptionClass( EXCEPTION_INT_DIVIDE_BY_ZERO );
 *     }
 * };
 *
 * typedef SafeIntExceptionHandler < YourExceptionClass > YourSafeIntExceptionHandler
 * You'd then declare your SafeInt objects like this:
 * SafeInt< int, YourSafeIntExceptionHandler >
 *
 * Unfortunately, there is no such thing as partial template specialization in typedef
 * statements, so you have three options if you find this cumbersome:
 *
 * 1) Create a holder class:
 *
 * template < typename T >
 * class MySafeInt
 * {
 *   public:
 *   SafeInt< T, MyExceptionClass> si;
 * };
 *
 * You'd then declare an instance like so:
 * MySafeInt< int > i;
 *
 * You'd lose handy things like initialization - it would have to be initialized as:
 *
 * i.si = 0;
 *
 * 2) You could create a typedef for every int type you deal with:
 *
 * typedef SafeInt< int, MyExceptionClass > MySafeInt;
 * typedef SafeInt< char, MyExceptionClass > MySafeChar;
 *
 * and so on. The second approach is probably more usable, and will just drop into code
 * better, which is the original intent of the SafeInt class.
 *
 * 3) If you're going to consistently use a different class to handle your exceptions,
 *    you can override the default typedef like so:
 *
 *    #define SafeIntDefaultExceptionHandler YourSafeIntExceptionHandler
 *
 *    Overall, this is probably the best approach.
 * */

// On the Microsoft compiler, violating a throw() annotation is a silent error.
// Other compilers might turn these into exceptions, and some users may want to not have throw() enabled.
// In addition, some error handlers may not throw C++ exceptions, which makes everything no throw.
#if defined SAFEINT_REMOVE_NOTHROW
#define SAFEINT_NOTHROW
#else
#define SAFEINT_NOTHROW throw()
#endif

namespace msl
{
namespace safeint3
{
// If you would like to use your own custom assert
// Define SAFEINT_ASSERT
#if !defined SAFEINT_ASSERT
#include <assert.h>
#define SAFEINT_ASSERT(x) assert(x)
#endif

#if defined SAFEINT_ASSERT_ON_EXCEPTION
inline void SafeIntExceptionAssert() SAFEINT_NOTHROW { SAFEINT_ASSERT(false); }
#else
inline void SafeIntExceptionAssert() SAFEINT_NOTHROW {}
#endif

#if SAFEINT_COMPILER == GCC_COMPILER || SAFEINT_COMPILER == CLANG_COMPILER
#define SAFEINT_NORETURN __attribute__((noreturn))
#define SAFEINT_STDCALL
#define SAFEINT_VISIBLE __attribute__((__visibility__("default")))
#define SAFEINT_WEAK __attribute__((weak))
#else
#define SAFEINT_NORETURN __declspec(noreturn)
#define SAFEINT_STDCALL __stdcall
#define SAFEINT_VISIBLE
#define SAFEINT_WEAK
#endif

class SAFEINT_VISIBLE SafeIntException
{
public:
    SafeIntException() SAFEINT_NOTHROW { m_code = SafeIntNoError; }
    SafeIntException(SafeIntError code) SAFEINT_NOTHROW { m_code = code; }
    SafeIntError m_code;
};

namespace SafeIntInternal
{
// Visual Studio version of SafeInt provides for two possible error
// handlers:
// SafeIntErrorPolicy_SafeIntException - C++ exception, default if not otherwise defined
// SafeIntErrorPolicy_InvalidParameter - Calls fail fast (Windows-specific), bypasses any exception handlers,
//                                       exits the app with a crash
template<typename E>
class SafeIntExceptionHandler;

template<>
class SafeIntExceptionHandler<SafeIntException>
{
public:
    static SAFEINT_NORETURN void SAFEINT_STDCALL SafeIntOnOverflow()
    {
        SafeIntExceptionAssert();
        throw SafeIntException(SafeIntArithmeticOverflow);
    }

    static SAFEINT_NORETURN void SAFEINT_STDCALL SafeIntOnDivZero()
    {
        SafeIntExceptionAssert();
        throw SafeIntException(SafeIntDivideByZero);
    }
};

#if !defined _CRT_SECURE_INVALID_PARAMETER
// Calling fail fast is somewhat more robust than calling abort,
// but abort is the closest we can manage without Visual Studio support
// Need the header for abort()
#include <stdlib.h>
#define _CRT_SECURE_INVALID_PARAMETER(msg) abort()
#endif

class SafeInt_InvalidParameter
{
public:
    static SAFEINT_NORETURN void SafeIntOnOverflow() SAFEINT_NOTHROW
    {
        SafeIntExceptionAssert();
        _CRT_SECURE_INVALID_PARAMETER("SafeInt Arithmetic Overflow");
    }

    static SAFEINT_NORETURN void SafeIntOnDivZero() SAFEINT_NOTHROW
    {
        SafeIntExceptionAssert();
        _CRT_SECURE_INVALID_PARAMETER("SafeInt Divide By Zero");
    }
};

#if defined _WINDOWS_

class SafeIntWin32ExceptionHandler
{
public:
    static SAFEINT_NORETURN void SAFEINT_STDCALL SafeIntOnOverflow() SAFEINT_NOTHROW
    {
        SafeIntExceptionAssert();
        RaiseException(static_cast<DWORD>(EXCEPTION_INT_OVERFLOW), EXCEPTION_NONCONTINUABLE, 0, 0);
    }

    static SAFEINT_NORETURN void SAFEINT_STDCALL SafeIntOnDivZero() SAFEINT_NOTHROW
    {
        SafeIntExceptionAssert();
        RaiseException(static_cast<DWORD>(EXCEPTION_INT_DIVIDE_BY_ZERO), EXCEPTION_NONCONTINUABLE, 0, 0);
    }
};

#endif

} // namespace SafeIntInternal

// both of these have cross-platform support
typedef SafeIntInternal::SafeIntExceptionHandler<SafeIntException> CPlusPlusExceptionHandler;
typedef SafeIntInternal::SafeInt_InvalidParameter InvalidParameterExceptionHandler;

// This exception handler is no longer recommended, but is left here in order not to break existing users
#if defined _WINDOWS_
typedef SafeIntInternal::SafeIntWin32ExceptionHandler Win32ExceptionHandler;
#endif

// For Visual Studio compatibility
#if defined VISUAL_STUDIO_SAFEINT_COMPAT
typedef CPlusPlusExceptionHandler SafeIntErrorPolicy_SafeIntException;
typedef InvalidParameterExceptionHandler SafeIntErrorPolicy_InvalidParameter;
#endif

// If the user hasn't defined a default exception handler,
// define one now, depending on whether they would like Win32 or C++ exceptions

// This library will use conditional noexcept soon, but not in this release
// Some users might mix exception handlers, which is not advised, but is supported
#if !defined SafeIntDefaultExceptionHandler
#if defined SAFEINT_RAISE_EXCEPTION
#if !defined _WINDOWS_
#error Include windows.h in order to use Win32 exceptions
#endif

#define SafeIntDefaultExceptionHandler Win32ExceptionHandler
#elif defined SAFEINT_FAILFAST
#define SafeIntDefaultExceptionHandler InvalidParameterExceptionHandler
#else
#define SafeIntDefaultExceptionHandler CPlusPlusExceptionHandler
#if !defined SAFEINT_EXCEPTION_HANDLER_CPP
#define SAFEINT_EXCEPTION_HANDLER_CPP 1
#endif
#endif
#endif

#if !defined SAFEINT_EXCEPTION_HANDLER_CPP
#define SAFEINT_EXCEPTION_HANDLER_CPP 0
#endif

// If an error handler is chosen other than C++ exceptions, such as Win32 exceptions, fail fast,
// or abort, then all methods become no throw. Some teams track throw() annotations closely,
// and the following option provides for this.
#if SAFEINT_EXCEPTION_HANDLER_CPP
#define SAFEINT_CPP_THROW
#else
#define SAFEINT_CPP_THROW SAFEINT_NOTHROW
#endif

// Turns out we can fool the compiler into not seeing compile-time constants with
// a simple template specialization
template<int method>
class CompileConst;
template<>
class CompileConst<true>
{
public:
    static bool Value() SAFEINT_NOTHROW { return true; }
};
template<>
class CompileConst<false>
{
public:
    static bool Value() SAFEINT_NOTHROW { return false; }
};

// The following template magic is because we're now not allowed
// to cast a float to an enum. This means that if we happen to assign
// an enum to a SafeInt of some type, it won't compile, unless we prevent
//              isFloat = ( (T)( (float)1.1 ) > (T)1 )
// from compiling in the case of an enum, which is the point of the specialization
// that follows.

// If we have support for std<typetraits>, then we can do this easily, and detect enums as well
template<typename T>
class NumericType;

#if defined _LIBCPP_TYPE_TRAITS || defined _TYPE_TRAITS_
// Continue to special case bool
template<>
class NumericType<bool>
{
public:
    enum
    {
        isBool = true,
        isFloat = false,
        isInt = false
    };
};
template<typename T>
class NumericType
{
public:
    enum
    {
        isBool = false, // We specialized out a bool
        isFloat = std::is_floating_point<T>::value,
        // If it is an enum, then consider it an int type
        // This does allow someone to make a SafeInt from an enum type, which is not recommended,
        // but it also allows someone to add an enum value to a SafeInt, which is handy.
        isInt = std::is_integral<T>::value || std::is_enum<T>::value
    };
};

#else

template<>
class NumericType<bool>
{
public:
    enum
    {
        isBool = true,
        isFloat = false,
        isInt = false
    };
};
template<>
class NumericType<char>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<unsigned char>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<signed char>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<short>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<unsigned short>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
#if defined SAFEINT_USE_WCHAR_T || defined _NATIVE_WCHAR_T_DEFINED
template<>
class NumericType<wchar_t>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
#endif
template<>
class NumericType<int>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<unsigned int>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<long>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<unsigned long>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<__int64>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<unsigned __int64>
{
public:
    enum
    {
        isBool = false,
        isFloat = false,
        isInt = true
    };
};
template<>
class NumericType<float>
{
public:
    enum
    {
        isBool = false,
        isFloat = true,
        isInt = false
    };
};
template<>
class NumericType<double>
{
public:
    enum
    {
        isBool = false,
        isFloat = true,
        isInt = false
    };
};
template<>
class NumericType<long double>
{
public:
    enum
    {
        isBool = false,
        isFloat = true,
        isInt = false
    };
};
// Catch-all for anything not supported
template<typename T>
class NumericType
{
public:
    // We have some unknown type, which could be an enum. For parity with the code that uses <type_traits>,
    // We can try a static_cast<int> - it if compiles, then it might be an enum, and should work.
    // If it is something else that just happens to have a constructor that takes an int, and a casting operator,
    // then it is possible something will go wrong, and for best results, cast it directly to an int before letting it
    // interact with a SafeInt

    enum
    {
        isBool = false,
        isFloat = false,
        isInt = static_cast<int>(static_cast<T>(0)) == 0
    };
};
#endif // type traits

// Use this to avoid compile-time const truncation warnings
template<int fSigned, int bits>
class SafeIntMinMax;

template<>
class SafeIntMinMax<true, 8>
{
public:
    const static signed __int8 min = (-0x7f - 1);
    const static signed __int8 max = 0x7f;
};
template<>
class SafeIntMinMax<true, 16>
{
public:
    const static __int16 min = (-0x7fff - 1);
    const static __int16 max = 0x7fff;
};
template<>
class SafeIntMinMax<true, 32>
{
public:
    const static __int32 min = (-0x7fffffff - 1);
    const static __int32 max = 0x7fffffff;
};
template<>
class SafeIntMinMax<true, 64>
{
public:
    const static __int64 min = static_cast<__int64>(0x8000000000000000LL);
    const static __int64 max = 0x7fffffffffffffffLL;
};

template<>
class SafeIntMinMax<false, 8>
{
public:
    const static unsigned __int8 min = 0;
    const static unsigned __int8 max = 0xff;
};
template<>
class SafeIntMinMax<false, 16>
{
public:
    const static unsigned __int16 min = 0;
    const static unsigned __int16 max = 0xffff;
};
template<>
class SafeIntMinMax<false, 32>
{
public:
    const static unsigned __int32 min = 0;
    const static unsigned __int32 max = 0xffffffff;
};
template<>
class SafeIntMinMax<false, 64>
{
public:
    const static unsigned __int64 min = 0;
    const static unsigned __int64 max = 0xffffffffffffffffULL;
};

template<typename T>
class IntTraits
{
public:
    C_ASSERT(NumericType<T>::isInt);
    enum
    {
        isSigned = ((T)(-1) < 0),
        is64Bit = (sizeof(T) == 8),
        is32Bit = (sizeof(T) == 4),
        is16Bit = (sizeof(T) == 2),
        is8Bit = (sizeof(T) == 1),
        isLT32Bit = (sizeof(T) < 4),
        isLT64Bit = (sizeof(T) < 8),
        isInt8 = (sizeof(T) == 1 && isSigned),
        isUint8 = (sizeof(T) == 1 && !isSigned),
        isInt16 = (sizeof(T) == 2 && isSigned),
        isUint16 = (sizeof(T) == 2 && !isSigned),
        isInt32 = (sizeof(T) == 4 && isSigned),
        isUint32 = (sizeof(T) == 4 && !isSigned),
        isInt64 = (sizeof(T) == 8 && isSigned),
        isUint64 = (sizeof(T) == 8 && !isSigned),
        bitCount = (sizeof(T) * 8),
        isBool = ((T)2 == (T)1)
    };

    // On version 13.10 enums cannot define __int64 values
    // so we'll use const statics instead!
    // These must be cast to deal with the possibility of a SafeInt being given an enum as an argument
    const static T maxInt = static_cast<T>(SafeIntMinMax<isSigned, bitCount>::max);
    const static T minInt = static_cast<T>(SafeIntMinMax<isSigned, bitCount>::min);
};

template<typename T>
const T IntTraits<T>::maxInt;
template<typename T>
const T IntTraits<T>::minInt;

template<typename T, typename U>
class SafeIntCompare
{
public:
    enum
    {
        isBothSigned = (IntTraits<T>::isSigned && IntTraits<U>::isSigned),
        isBothUnsigned = (!IntTraits<T>::isSigned && !IntTraits<U>::isSigned),
        isLikeSigned = ((bool)(IntTraits<T>::isSigned) == (bool)(IntTraits<U>::isSigned)),
        isCastOK = ((isLikeSigned && sizeof(T) >= sizeof(U)) || (IntTraits<T>::isSigned && sizeof(T) > sizeof(U))),
        isBothLT32Bit = (IntTraits<T>::isLT32Bit && IntTraits<U>::isLT32Bit),
        isBothLT64Bit = (IntTraits<T>::isLT64Bit && IntTraits<U>::isLT64Bit)
    };
};

// all of the arithmetic operators can be solved by the same code within
// each of these regions without resorting to compile-time constant conditionals
// most operators collapse the problem into less than the 22 zones, but this is used
// as the first cut
// using this also helps ensure that we handle all of the possible cases correctly

template<typename T, typename U>
class IntRegion
{
public:
    enum
    {
        // unsigned-unsigned zone
        IntZone_UintLT32_UintLT32 = SafeIntCompare<T, U>::isBothUnsigned && SafeIntCompare<T, U>::isBothLT32Bit,
        IntZone_Uint32_UintLT64 =
            SafeIntCompare<T, U>::isBothUnsigned && IntTraits<T>::is32Bit && IntTraits<U>::isLT64Bit,
        IntZone_UintLT32_Uint32 =
            SafeIntCompare<T, U>::isBothUnsigned && IntTraits<T>::isLT32Bit && IntTraits<U>::is32Bit,
        IntZone_Uint64_Uint = SafeIntCompare<T, U>::isBothUnsigned && IntTraits<T>::is64Bit,
        IntZone_UintLT64_Uint64 =
            SafeIntCompare<T, U>::isBothUnsigned && IntTraits<T>::isLT64Bit && IntTraits<U>::is64Bit,
        // unsigned-signed
        IntZone_UintLT32_IntLT32 =
            !IntTraits<T>::isSigned && IntTraits<U>::isSigned && SafeIntCompare<T, U>::isBothLT32Bit,
        IntZone_Uint32_IntLT64 = IntTraits<T>::isUint32 && IntTraits<U>::isSigned && IntTraits<U>::isLT64Bit,
        IntZone_UintLT32_Int32 = !IntTraits<T>::isSigned && IntTraits<T>::isLT32Bit && IntTraits<U>::isInt32,
        IntZone_Uint64_Int = IntTraits<T>::isUint64 && IntTraits<U>::isSigned && IntTraits<U>::isLT64Bit,
        IntZone_UintLT64_Int64 = !IntTraits<T>::isSigned && IntTraits<T>::isLT64Bit && IntTraits<U>::isInt64,
        IntZone_Uint64_Int64 = IntTraits<T>::isUint64 && IntTraits<U>::isInt64,
        // signed-signed
        IntZone_IntLT32_IntLT32 = SafeIntCompare<T, U>::isBothSigned && SafeIntCompare<T, U>::isBothLT32Bit,
        IntZone_Int32_IntLT64 = SafeIntCompare<T, U>::isBothSigned && IntTraits<T>::is32Bit && IntTraits<U>::isLT64Bit,
        IntZone_IntLT32_Int32 = SafeIntCompare<T, U>::isBothSigned && IntTraits<T>::isLT32Bit && IntTraits<U>::is32Bit,
        IntZone_Int64_Int64 = SafeIntCompare<T, U>::isBothSigned && IntTraits<T>::isInt64 && IntTraits<U>::isInt64,
        IntZone_Int64_Int = SafeIntCompare<T, U>::isBothSigned && IntTraits<T>::is64Bit && IntTraits<U>::isLT64Bit,
        IntZone_IntLT64_Int64 = SafeIntCompare<T, U>::isBothSigned && IntTraits<T>::isLT64Bit && IntTraits<U>::is64Bit,
        // signed-unsigned
        IntZone_IntLT32_UintLT32 =
            IntTraits<T>::isSigned && !IntTraits<U>::isSigned && SafeIntCompare<T, U>::isBothLT32Bit,
        IntZone_Int32_UintLT32 = IntTraits<T>::isInt32 && !IntTraits<U>::isSigned && IntTraits<U>::isLT32Bit,
        IntZone_IntLT64_Uint32 = IntTraits<T>::isSigned && IntTraits<T>::isLT64Bit && IntTraits<U>::isUint32,
        IntZone_Int64_UintLT64 = IntTraits<T>::isInt64 && !IntTraits<U>::isSigned && IntTraits<U>::isLT64Bit,
        IntZone_Int_Uint64 = IntTraits<T>::isSigned && IntTraits<U>::isUint64 && IntTraits<T>::isLT64Bit,
        IntZone_Int64_Uint64 = IntTraits<T>::isInt64 && IntTraits<U>::isUint64
    };
};

// In all of the following functions, we have two versions
// One for SafeInt, which throws C++ (or possibly SEH) exceptions
// The non-throwing versions are for use by the helper functions that return success and failure.
// Some of the non-throwing functions are not used, but are maintained for completeness.

// There's no real alternative to duplicating logic, but keeping the two versions
// immediately next to one another will help reduce problems

// useful function to help with getting the magnitude of a negative number
enum AbsMethod
{
    AbsMethodInt,
    AbsMethodInt64,
    AbsMethodNoop
};

template<typename T>
class GetAbsMethod
{
public:
    enum
    {
        method = IntTraits<T>::isLT64Bit && IntTraits<T>::isSigned
                     ? AbsMethodInt
                     : IntTraits<T>::isInt64 ? AbsMethodInt64 : AbsMethodNoop
    };
};

// let's go ahead and hard-code a dependency on the
// representation of negative numbers to keep compilers from getting overly
// happy with optimizing away things like -MIN_INT.
template<typename T, int>
class AbsValueHelper;

template<typename T>
class AbsValueHelper<T, AbsMethodInt>
{
public:
    static unsigned __int32 Abs(T t) SAFEINT_NOTHROW
    {
        SAFEINT_ASSERT(t < 0);
        return ~(unsigned __int32)t + 1;
    }
};

template<typename T>
class AbsValueHelper<T, AbsMethodInt64>
{
public:
    static unsigned __int64 Abs(T t) SAFEINT_NOTHROW
    {
        SAFEINT_ASSERT(t < 0);
        return ~(unsigned __int64)t + 1;
    }
};

template<typename T>
class AbsValueHelper<T, AbsMethodNoop>
{
public:
    static T Abs(T t) SAFEINT_NOTHROW
    {
        // Why are you calling Abs on an unsigned number ???
        SAFEINT_ASSERT(false);
        return t;
    }
};

template<typename T, bool>
class NegationHelper;
// Previous versions had an assert that the type being negated was 32-bit or higher
// In retrospect, this seems like something to just document
// Negation will normally upcast to int
// For example -(unsigned short)0xffff == (int)0xffff0001
// This class will retain the type, and will truncate, which may not be what
// you wanted
// If you want normal operator casting behavior, do this:
// SafeInt<unsigned short> ss = 0xffff;
// then:
// -(SafeInt<int>(ss))
// will then emit a signed int with the correct value and bitfield

template<typename T>
class NegationHelper<T, true> // Signed
{
public:
    template<typename E>
    static T NegativeThrow(T t) SAFEINT_CPP_THROW
    {
        // corner case
        if (t != IntTraits<T>::minInt)
        {
            // cast prevents unneeded checks in the case of small ints
            return -t;
        }
        E::SafeIntOnOverflow();
    }

    static bool Negative(T t, T& ret) SAFEINT_NOTHROW
    {
        // corner case
        if (t != IntTraits<T>::minInt)
        {
            // cast prevents unneeded checks in the case of small ints
            ret = -t;
            return true;
        }
        return false;
    }
};

// Helper classes to work keep compilers from
// optimizing away negation
template<typename T>
class SignedNegation;

template<>
class SignedNegation<signed __int32>
{
public:
    static signed __int32 Value(unsigned __int64 in) SAFEINT_NOTHROW
    {
        return (signed __int32)(~(unsigned __int32)in + 1);
    }

    static signed __int32 Value(unsigned __int32 in) SAFEINT_NOTHROW { return (signed __int32)(~in + 1); }
};

template<>
class SignedNegation<signed __int64>
{
public:
    static signed __int64 Value(unsigned __int64 in) SAFEINT_NOTHROW { return (signed __int64)(~in + 1); }
};

template<typename T>
class NegationHelper<T, false> // unsigned
{
public:
    template<typename E>
    static T NegativeThrow(T t) SAFEINT_CPP_THROW
    {
#if defined SAFEINT_DISALLOW_UNSIGNED_NEGATION
        C_ASSERT(sizeof(T) == 0);
#endif

#if SAFEINT_COMPILER == VISUAL_STUDIO_COMPILER
#pragma warning(push)
// this avoids warnings from the unary '-' operator being applied to unsigned numbers
#pragma warning(disable : 4146)
#endif
        // Note - this could be quenched on gcc
        // by doing something like:
        // return (T)-((__int64)t);
        // but it seems like you would want a warning when doing this.
        return (T)-t;

#if SAFEINT_COMPILER == VISUAL_STUDIO_COMPILER
#pragma warning(pop)
#endif
    }

    static bool Negative(T t, T& ret) SAFEINT_NOTHROW
    {
        if (IntTraits<T>::isLT32Bit)
        {
            // See above
            SAFEINT_ASSERT(false);
        }
#if defined SAFEINT_DISALLOW_UNSIGNED_NEGATION
        C_ASSERT(sizeof(T) == 0);
#endif
        // Do it this way to avoid warning
        ret = -t;
        return true;
    }
};

// core logic to determine casting behavior
enum CastMethod
{
    CastOK = 0,
    CastCheckLTZero,
    CastCheckGTMax,
    CastCheckSafeIntMinMaxUnsigned,
    CastCheckSafeIntMinMaxSigned,
    CastToFloat,
    CastFromFloat,
    CastToBool,
    CastFromBool
};

template<typename ToType, typename FromType>
class GetCastMethod
{
public:
    enum
    {
        method = (IntTraits<FromType>::isBool && !IntTraits<ToType>::isBool)
                     ? CastFromBool
                     :

                     (!IntTraits<FromType>::isBool && IntTraits<ToType>::isBool)
                         ? CastToBool
                         :

                         (SafeIntCompare<ToType, FromType>::isCastOK)
                             ? CastOK
                             :

                             ((IntTraits<ToType>::isSigned && !IntTraits<FromType>::isSigned &&
                               sizeof(FromType) >= sizeof(ToType)) ||
                              (SafeIntCompare<ToType, FromType>::isBothUnsigned && sizeof(FromType) > sizeof(ToType)))
                                 ? CastCheckGTMax
                                 :

                                 (!IntTraits<ToType>::isSigned && IntTraits<FromType>::isSigned &&
                                  sizeof(ToType) >= sizeof(FromType))
                                     ? CastCheckLTZero
                                     :

                                     (!IntTraits<ToType>::isSigned) ? CastCheckSafeIntMinMaxUnsigned
                                                                    : CastCheckSafeIntMinMaxSigned
    };
};

template<typename FromType>
class GetCastMethod<float, FromType>
{
public:
    enum
    {
        method = CastOK
    };
};

template<typename FromType>
class GetCastMethod<double, FromType>
{
public:
    enum
    {
        method = CastOK
    };
};

template<typename FromType>
class GetCastMethod<long double, FromType>
{
public:
    enum
    {
        method = CastOK
    };
};

template<typename ToType>
class GetCastMethod<ToType, float>
{
public:
    enum
    {
        method = CastFromFloat
    };
};

template<typename ToType>
class GetCastMethod<ToType, double>
{
public:
    enum
    {
        method = CastFromFloat
    };
};

template<typename ToType>
class GetCastMethod<ToType, long double>
{
public:
    enum
    {
        method = CastFromFloat
    };
};

template<typename T, typename U, int>
class SafeCastHelper;

template<typename T, typename U>
class SafeCastHelper<T, U, CastOK>
{
public:
    static bool Cast(U u, T& t) SAFEINT_NOTHROW
    {
        t = (T)u;
        return true;
    }

    template<typename E>
    static void CastThrow(U u, T& t) SAFEINT_CPP_THROW
    {
        t = (T)u;
    }
};

// special case floats and doubles
// tolerate loss of precision
template<typename T, typename U>
class SafeCastHelper<T, U, CastFromFloat>
{
public:
    static bool Cast(U u, T& t) SAFEINT_NOTHROW
    {
        if (u <= (U)IntTraits<T>::maxInt && u >= (U)IntTraits<T>::minInt)
        {
            t = (T)u;
            return true;
        }
        return false;
    }

    template<typename E>
    static void CastThrow(U u, T& t) SAFEINT_CPP_THROW
    {
        if (u <= (U)IntTraits<T>::maxInt && u >= (U)IntTraits<T>::minInt)
        {
            t = (T)u;
            return;
        }
        E::SafeIntOnOverflow();
    }
};

// Match on any method where a bool is cast to type T
template<typename T>
class SafeCastHelper<T, bool, CastFromBool>
{
public:
    static bool Cast(bool b, T& t) SAFEINT_NOTHROW
    {
        t = (T)(b ? 1 : 0);
        return true;
    }

    template<typename E>
    static void CastThrow(bool b, T& t) SAFEINT_CPP_THROW
    {
        t = (T)(b ? 1 : 0);
    }
};

template<typename T>
class SafeCastHelper<bool, T, CastToBool>
{
public:
    static bool Cast(T t, bool& b) SAFEINT_NOTHROW
    {
        b = !!t;
        return true;
    }

    template<typename E>
    static void CastThrow(bool b, T& t) SAFEINT_CPP_THROW
    {
        b = !!t;
    }
};

template<typename T, typename U>
class SafeCastHelper<T, U, CastCheckLTZero>
{
public:
    static bool Cast(U u, T& t) SAFEINT_NOTHROW
    {
        if (u < 0) return false;

        t = (T)u;
        return true;
    }

    template<typename E>
    static void CastThrow(U u, T& t) SAFEINT_CPP_THROW
    {
        if (u < 0) E::SafeIntOnOverflow();

        t = (T)u;
    }
};

template<typename T, typename U>
class SafeCastHelper<T, U, CastCheckGTMax>
{
public:
    static bool Cast(U u, T& t) SAFEINT_NOTHROW
    {
        if (u > (U)IntTraits<T>::maxInt) return false;

        t = (T)u;
        return true;
    }

    template<typename E>
    static void CastThrow(U u, T& t) SAFEINT_CPP_THROW
    {
        if (u > (U)IntTraits<T>::maxInt) E::SafeIntOnOverflow();

        t = (T)u;
    }
};

template<typename T, typename U>
class SafeCastHelper<T, U, CastCheckSafeIntMinMaxUnsigned>
{
public:
    static bool Cast(U u, T& t) SAFEINT_NOTHROW
    {
        // U is signed - T could be either signed or unsigned
        if (u > IntTraits<T>::maxInt || u < 0) return false;

        t = (T)u;
        return true;
    }

    template<typename E>
    static void CastThrow(U u, T& t) SAFEINT_CPP_THROW
    {
        // U is signed - T could be either signed or unsigned
        if (u > IntTraits<T>::maxInt || u < 0) E::SafeIntOnOverflow();

        t = (T)u;
    }
};

template<typename T, typename U>
class SafeCastHelper<T, U, CastCheckSafeIntMinMaxSigned>
{
public:
    static bool Cast(U u, T& t) SAFEINT_NOTHROW
    {
        // T, U are signed
        if (u > IntTraits<T>::maxInt || u < IntTraits<T>::minInt) return false;

        t = (T)u;
        return true;
    }

    template<typename E>
    static void CastThrow(U u, T& t) SAFEINT_CPP_THROW
    {
        // T, U are signed
        if (u > IntTraits<T>::maxInt || u < IntTraits<T>::minInt) E::SafeIntOnOverflow();

        t = (T)u;
    }
};

// core logic to determine whether a comparison is valid, or needs special treatment
enum ComparisonMethod
{
    ComparisonMethod_Ok = 0,
    ComparisonMethod_CastInt,
    ComparisonMethod_CastInt64,
    ComparisonMethod_UnsignedT,
    ComparisonMethod_UnsignedU
};

// Note - the standard is arguably broken in the case of some integer
// conversion operations
// For example, signed char a = -1 = 0xff
//              unsigned int b = 0xffffffff
// If you then test if a < b, a value-preserving cast
// is made, and you're essentially testing
// (unsigned int)a < b == false
//
// I do not think this makes sense - if you perform
// a cast to an __int64, which can clearly preserve both value and signedness
// then you get a different and intuitively correct answer
// IMHO, -1 should be less than 4 billion
// If you prefer to retain the ANSI standard behavior
// insert #define ANSI_CONVERSIONS into your source
// Behavior differences occur in the following cases:
// 8, 16, and 32-bit signed int, unsigned 32-bit int
// any signed int, unsigned 64-bit int
// Note - the signed int must be negative to show the problem

template<typename T, typename U>
class ValidComparison
{
public:
    enum
    {
#ifdef ANSI_CONVERSIONS
        method = ComparisonMethod_Ok
#else
        method = ((SafeIntCompare<T, U>::isLikeSigned)
                      ? ComparisonMethod_Ok
                      : ((IntTraits<T>::isSigned && sizeof(T) < 8 && sizeof(U) < 4) ||
                         (IntTraits<U>::isSigned && sizeof(T) < 4 && sizeof(U) < 8))
                            ? ComparisonMethod_CastInt
                            : ((IntTraits<T>::isSigned && sizeof(U) < 8) || (IntTraits<U>::isSigned && sizeof(T) < 8))
                                  ? ComparisonMethod_CastInt64
                                  : (!IntTraits<T>::isSigned) ? ComparisonMethod_UnsignedT : ComparisonMethod_UnsignedU)
#endif
    };
};

template<typename T, typename U, int state>
class EqualityTest;

template<typename T, typename U>
class EqualityTest<T, U, ComparisonMethod_Ok>
{
public:
    static bool IsEquals(const T t, const U u) SAFEINT_NOTHROW { return (t == u); }
};

template<typename T, typename U>
class EqualityTest<T, U, ComparisonMethod_CastInt>
{
public:
    static bool IsEquals(const T t, const U u) SAFEINT_NOTHROW { return ((int)t == (int)u); }
};

template<typename T, typename U>
class EqualityTest<T, U, ComparisonMethod_CastInt64>
{
public:
    static bool IsEquals(const T t, const U u) SAFEINT_NOTHROW { return ((__int64)t == (__int64)u); }
};

template<typename T, typename U>
class EqualityTest<T, U, ComparisonMethod_UnsignedT>
{
public:
    static bool IsEquals(const T t, const U u) SAFEINT_NOTHROW
    {
        // one operand is 32 or 64-bit unsigned, and the other is signed and the same size or smaller
        if (u < 0) return false;

        // else safe to cast to type T
        return (t == (T)u);
    }
};

template<typename T, typename U>
class EqualityTest<T, U, ComparisonMethod_UnsignedU>
{
public:
    static bool IsEquals(const T t, const U u) SAFEINT_NOTHROW
    {
        // one operand is 32 or 64-bit unsigned, and the other is signed and the same size or smaller
        if (t < 0) return false;

        // else safe to cast to type U
        return ((U)t == u);
    }
};

template<typename T, typename U, int state>
class GreaterThanTest;

template<typename T, typename U>
class GreaterThanTest<T, U, ComparisonMethod_Ok>
{
public:
    static bool GreaterThan(const T t, const U u) SAFEINT_NOTHROW { return (t > u); }
};

template<typename T, typename U>
class GreaterThanTest<T, U, ComparisonMethod_CastInt>
{
public:
    static bool GreaterThan(const T t, const U u) SAFEINT_NOTHROW { return ((int)t > (int)u); }
};

template<typename T, typename U>
class GreaterThanTest<T, U, ComparisonMethod_CastInt64>
{
public:
    static bool GreaterThan(const T t, const U u) SAFEINT_NOTHROW { return ((__int64)t > (__int64)u); }
};

template<typename T, typename U>
class GreaterThanTest<T, U, ComparisonMethod_UnsignedT>
{
public:
    static bool GreaterThan(const T t, const U u) SAFEINT_NOTHROW
    {
        // one operand is 32 or 64-bit unsigned, and the other is signed and the same size or smaller
        if (u < 0) return true;

        // else safe to cast to type T
        return (t > (T)u);
    }
};

template<typename T, typename U>
class GreaterThanTest<T, U, ComparisonMethod_UnsignedU>
{
public:
    static bool GreaterThan(const T t, const U u) SAFEINT_NOTHROW
    {
        // one operand is 32 or 64-bit unsigned, and the other is signed and the same size or smaller
        if (t < 0) return false;

        // else safe to cast to type U
        return ((U)t > u);
    }
};

// Modulus is simpler than comparison, but follows much the same logic
// using this set of functions, it can't fail except in a div 0 situation
template<typename T, typename U, int method>
class ModulusHelper;

template<typename T, typename U>
class ModulusHelper<T, U, ComparisonMethod_Ok>
{
public:
    static SafeIntError Modulus(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0) return SafeIntDivideByZero;

        // trap corner case
        if (CompileConst<IntTraits<U>::isSigned>::Value())
        {
            // Some compilers don't notice that this only compiles when u is signed
            // Add cast to make them happy
            if (u == (U)-1)
            {
                result = 0;
                return SafeIntNoError;
            }
        }

        result = (T)(t % u);
        return SafeIntNoError;
    }

    template<typename E>
    static void ModulusThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0) E::SafeIntOnDivZero();

        // trap corner case
        if (CompileConst<IntTraits<U>::isSigned>::Value())
        {
            if (u == (U)-1)
            {
                result = 0;
                return;
            }
        }

        result = (T)(t % u);
    }
};

template<typename T, typename U>
class ModulusHelper<T, U, ComparisonMethod_CastInt>
{
public:
    static SafeIntError Modulus(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0) return SafeIntDivideByZero;

        // trap corner case
        if (CompileConst<IntTraits<U>::isSigned>::Value())
        {
            if (u == (U)-1)
            {
                result = 0;
                return SafeIntNoError;
            }
        }

        result = (T)(t % u);
        return SafeIntNoError;
    }

    template<typename E>
    static void ModulusThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0) E::SafeIntOnDivZero();

        // trap corner case
        if (CompileConst<IntTraits<U>::isSigned>::Value())
        {
            if (u == (U)-1)
            {
                result = 0;
                return;
            }
        }

        result = (T)(t % u);
    }
};

template<typename T, typename U>
class ModulusHelper<T, U, ComparisonMethod_CastInt64>
{
public:
    static SafeIntError Modulus(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0) return SafeIntDivideByZero;

        // trap corner case
        if (CompileConst<IntTraits<U>::isSigned>::Value())
        {
            if (u == (U)-1)
            {
                result = 0;
                return SafeIntNoError;
            }
        }

        result = (T)((__int64)t % (__int64)u);
        return SafeIntNoError;
    }

    template<typename E>
    static void ModulusThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0) E::SafeIntOnDivZero();

        if (CompileConst<IntTraits<U>::isSigned>::Value())
        {
            if (u == (U)-1)
            {
                result = 0;
                return;
            }
        }

        result = (T)((__int64)t % (__int64)u);
    }
};

// T is unsigned __int64, U is any signed int
template<typename T, typename U>
class ModulusHelper<T, U, ComparisonMethod_UnsignedT>
{
public:
    static SafeIntError Modulus(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0) return SafeIntDivideByZero;

        // u could be negative - if so, need to convert to positive
        // casts below are always safe due to the way modulus works
        if (u < 0)
            result = (T)(t % AbsValueHelper<U, GetAbsMethod<U>::method>::Abs(u));
        else
            result = (T)(t % u);

        return SafeIntNoError;
    }

    template<typename E>
    static void ModulusThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0) E::SafeIntOnDivZero();

        // u could be negative - if so, need to convert to positive
        if (u < 0)
            result = (T)(t % AbsValueHelper<U, GetAbsMethod<U>::method>::Abs(u));
        else
            result = (T)(t % u);
    }
};

// U is unsigned __int64, T any signed int
template<typename T, typename U>
class ModulusHelper<T, U, ComparisonMethod_UnsignedU>
{
public:
    static SafeIntError Modulus(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0) return SafeIntDivideByZero;

        // t could be negative - if so, need to convert to positive
        if (t < 0)
            result = (T)(~(AbsValueHelper<T, GetAbsMethod<T>::method>::Abs(t) % u) + 1);
        else
            result = (T)((T)t % u);

        return SafeIntNoError;
    }

    template<typename E>
    static void ModulusThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0) E::SafeIntOnDivZero();

        // t could be negative - if so, need to convert to positive
        if (t < 0)
            result = (T)(~(AbsValueHelper<T, GetAbsMethod<T>::method>::Abs(t) % u) + 1);
        else
            result = (T)((T)t % u);
    }
};

// core logic to determine method to check multiplication
enum MultiplicationState
{
    MultiplicationState_CastInt = 0,  // One or both signed, smaller than 32-bit
    MultiplicationState_CastInt64,    // One or both signed, smaller than 64-bit
    MultiplicationState_CastUint,     // Both are unsigned, smaller than 32-bit
    MultiplicationState_CastUint64,   // Both are unsigned, both 32-bit or smaller
    MultiplicationState_Uint64Uint,   // Both are unsigned, lhs 64-bit, rhs 32-bit or smaller
    MultiplicationState_Uint64Uint64, // Both are unsigned int64
    MultiplicationState_Uint64Int,    // lhs is unsigned int64, rhs int32
    MultiplicationState_Uint64Int64,  // lhs is unsigned int64, rhs signed int64
    MultiplicationState_UintUint64,   // Both are unsigned, lhs 32-bit or smaller, rhs 64-bit
    MultiplicationState_UintInt64,    // lhs unsigned 32-bit or less, rhs int64
    MultiplicationState_Int64Uint,    // lhs int64, rhs unsigned int32
    MultiplicationState_Int64Int64,   // lhs int64, rhs int64
    MultiplicationState_Int64Int,     // lhs int64, rhs int32
    MultiplicationState_IntUint64,    // lhs int, rhs unsigned int64
    MultiplicationState_IntInt64,     // lhs int, rhs int64
    MultiplicationState_Int64Uint64,  // lhs int64, rhs uint64
    MultiplicationState_Error
};

template<typename T, typename U>
class MultiplicationMethod
{
public:
    enum
    {
        // unsigned-unsigned
        method =
            (IntRegion<T, U>::IntZone_UintLT32_UintLT32
                 ? MultiplicationState_CastUint
                 : (IntRegion<T, U>::IntZone_Uint32_UintLT64 || IntRegion<T, U>::IntZone_UintLT32_Uint32)
                       ? MultiplicationState_CastUint64
                       : SafeIntCompare<T, U>::isBothUnsigned && IntTraits<T>::isUint64 && IntTraits<U>::isUint64
                             ? MultiplicationState_Uint64Uint64
                             : (IntRegion<T, U>::IntZone_Uint64_Uint)
                                   ? MultiplicationState_Uint64Uint
                                   : (IntRegion<T, U>::IntZone_UintLT64_Uint64) ? MultiplicationState_UintUint64 :
                                                                                // unsigned-signed
                                         (IntRegion<T, U>::IntZone_UintLT32_IntLT32)
                                             ? MultiplicationState_CastInt
                                             : (IntRegion<T, U>::IntZone_Uint32_IntLT64 ||
                                                IntRegion<T, U>::IntZone_UintLT32_Int32)
                                                   ? MultiplicationState_CastInt64
                                                   : (IntRegion<T, U>::IntZone_Uint64_Int)
                                                         ? MultiplicationState_Uint64Int
                                                         : (IntRegion<T, U>::IntZone_UintLT64_Int64)
                                                               ? MultiplicationState_UintInt64
                                                               : (IntRegion<T, U>::IntZone_Uint64_Int64)
                                                                     ? MultiplicationState_Uint64Int64
                                                                     :
                                                                     // signed-signed
                                                                     (IntRegion<T, U>::IntZone_IntLT32_IntLT32)
                                                                         ? MultiplicationState_CastInt
                                                                         : (IntRegion<T, U>::IntZone_Int32_IntLT64 ||
                                                                            IntRegion<T, U>::IntZone_IntLT32_Int32)
                                                                               ? MultiplicationState_CastInt64
                                                                               : (IntRegion<T, U>::IntZone_Int64_Int64)
                                                                                     ? MultiplicationState_Int64Int64
                                                                                     : (IntRegion<T,
                                                                                                  U>::IntZone_Int64_Int)
                                                                                           ? MultiplicationState_Int64Int
                                                                                           : (IntRegion<T, U>::
                                                                                                  IntZone_IntLT64_Int64)
                                                                                                 ? MultiplicationState_IntInt64
                                                                                                 :
                                                                                                 // signed-unsigned
                                                                                                 (IntRegion<T, U>::
                                                                                                      IntZone_IntLT32_UintLT32)
                                                                                                     ? MultiplicationState_CastInt
                                                                                                     : (IntRegion<T,
                                                                                                                  U>::
                                                                                                            IntZone_Int32_UintLT32 ||
                                                                                                        IntRegion<T,
                                                                                                                  U>::
                                                                                                            IntZone_IntLT64_Uint32)
                                                                                                           ? MultiplicationState_CastInt64
                                                                                                           : (IntRegion<
                                                                                                                 T,
                                                                                                                 U>::
                                                                                                                  IntZone_Int64_UintLT64)
                                                                                                                 ? MultiplicationState_Int64Uint
                                                                                                                 : (IntRegion<
                                                                                                                       T,
                                                                                                                       U>::
                                                                                                                        IntZone_Int_Uint64)
                                                                                                                       ? MultiplicationState_IntUint64
                                                                                                                       : (IntRegion<
                                                                                                                              T,
                                                                                                                              U>::IntZone_Int64_Uint64
                                                                                                                              ? MultiplicationState_Int64Uint64
                                                                                                                              : MultiplicationState_Error))
    };
};

template<typename T, typename U, int state>
class MultiplicationHelper;

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_CastInt>
{
public:
    // accepts signed, both less than 32-bit
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        int tmp = t * u;

        if (tmp > IntTraits<T>::maxInt || tmp < IntTraits<T>::minInt) return false;

        ret = (T)tmp;
        return true;
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        int tmp = t * u;

        if (tmp > IntTraits<T>::maxInt || tmp < IntTraits<T>::minInt) E::SafeIntOnOverflow();

        ret = (T)tmp;
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_CastUint>
{
public:
    // accepts unsigned, both less than 32-bit
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        unsigned int tmp = (unsigned int)(t * u);

        if (tmp > IntTraits<T>::maxInt) return false;

        ret = (T)tmp;
        return true;
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        unsigned int tmp = (unsigned int)(t * u);

        if (tmp > IntTraits<T>::maxInt) E::SafeIntOnOverflow();

        ret = (T)tmp;
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_CastInt64>
{
public:
    // mixed signed or both signed where at least one argument is 32-bit, and both a 32-bit or less
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        __int64 tmp = (__int64)t * (__int64)u;

        if (tmp > (__int64)IntTraits<T>::maxInt || tmp < (__int64)IntTraits<T>::minInt) return false;

        ret = (T)tmp;
        return true;
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        __int64 tmp = (__int64)t * (__int64)u;

        if (tmp > (__int64)IntTraits<T>::maxInt || tmp < (__int64)IntTraits<T>::minInt) E::SafeIntOnOverflow();

        ret = (T)tmp;
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_CastUint64>
{
public:
    // both unsigned where at least one argument is 32-bit, and both are 32-bit or less
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        unsigned __int64 tmp = (unsigned __int64)t * (unsigned __int64)u;

        if (tmp > (unsigned __int64)IntTraits<T>::maxInt) return false;

        ret = (T)tmp;
        return true;
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        unsigned __int64 tmp = (unsigned __int64)t * (unsigned __int64)u;

        if (tmp > (unsigned __int64)IntTraits<T>::maxInt) E::SafeIntOnOverflow();

        ret = (T)tmp;
    }
};

// T = left arg and return type
// U = right arg
template<typename T, typename U>
class LargeIntRegMultiply;

#if SAFEINT_USE_INTRINSICS
// As usual, unsigned is easy
inline bool IntrinsicMultiplyUint64(const unsigned __int64& a,
                                    const unsigned __int64& b,
                                    unsigned __int64* pRet) SAFEINT_NOTHROW
{
    unsigned __int64 ulHigh = 0;
    *pRet = _umul128(a, b, &ulHigh);
    return ulHigh == 0;
}

// Signed, is not so easy
inline bool IntrinsicMultiplyInt64(const signed __int64& a,
                                   const signed __int64& b,
                                   signed __int64* pRet) SAFEINT_NOTHROW
{
    __int64 llHigh = 0;
    *pRet = _mul128(a, b, &llHigh);

    // Now we need to figure out what we expect
    // If llHigh is 0, then treat *pRet as unsigned
    // If llHigh is < 0, then treat *pRet as signed

    if ((a ^ b) < 0)
    {
        // Negative result expected
        if (llHigh == -1 && *pRet < 0 || llHigh == 0 && *pRet == 0)
        {
            // Everything is within range
            return true;
        }
    }
    else
    {
        // Result should be positive
        // Check for overflow
        if (llHigh == 0 && (unsigned __int64)*pRet <= IntTraits<signed __int64>::maxInt) return true;
    }
    return false;
}

#endif

template<>
class LargeIntRegMultiply<unsigned __int64, unsigned __int64>
{
public:
    static bool RegMultiply(const unsigned __int64& a,
                            const unsigned __int64& b,
                            unsigned __int64* pRet) SAFEINT_NOTHROW
    {
#if SAFEINT_USE_INTRINSICS
        return IntrinsicMultiplyUint64(a, b, pRet);
#else
        unsigned __int32 aHigh, aLow, bHigh, bLow;

        // Consider that a*b can be broken up into:
        // (aHigh * 2^32 + aLow) * (bHigh * 2^32 + bLow)
        // => (aHigh * bHigh * 2^64) + (aLow * bHigh * 2^32) + (aHigh * bLow * 2^32) + (aLow * bLow)
        // Note - same approach applies for 128 bit math on a 64-bit system

        aHigh = (unsigned __int32)(a >> 32);
        aLow = (unsigned __int32)a;
        bHigh = (unsigned __int32)(b >> 32);
        bLow = (unsigned __int32)b;

        *pRet = 0;

        if (aHigh == 0)
        {
            if (bHigh != 0)
            {
                *pRet = (unsigned __int64)aLow * (unsigned __int64)bHigh;
            }
        }
        else if (bHigh == 0)
        {
            if (aHigh != 0)
            {
                *pRet = (unsigned __int64)aHigh * (unsigned __int64)bLow;
            }
        }
        else
        {
            return false;
        }

        if (*pRet != 0)
        {
            unsigned __int64 tmp;

            if ((unsigned __int32)(*pRet >> 32) != 0) return false;

            *pRet <<= 32;
            tmp = (unsigned __int64)aLow * (unsigned __int64)bLow;
            *pRet += tmp;

            if (*pRet < tmp) return false;

            return true;
        }

        *pRet = (unsigned __int64)aLow * (unsigned __int64)bLow;
        return true;
#endif
    }

    template<typename E>
    static void RegMultiplyThrow(const unsigned __int64& a,
                                 const unsigned __int64& b,
                                 unsigned __int64* pRet) SAFEINT_CPP_THROW
    {
#if SAFEINT_USE_INTRINSICS
        if (!IntrinsicMultiplyUint64(a, b, pRet)) E::SafeIntOnOverflow();
#else
        unsigned __int32 aHigh, aLow, bHigh, bLow;

        // Consider that a*b can be broken up into:
        // (aHigh * 2^32 + aLow) * (bHigh * 2^32 + bLow)
        // => (aHigh * bHigh * 2^64) + (aLow * bHigh * 2^32) + (aHigh * bLow * 2^32) + (aLow * bLow)
        // Note - same approach applies for 128 bit math on a 64-bit system

        aHigh = (unsigned __int32)(a >> 32);
        aLow = (unsigned __int32)a;
        bHigh = (unsigned __int32)(b >> 32);
        bLow = (unsigned __int32)b;

        *pRet = 0;

        if (aHigh == 0)
        {
            if (bHigh != 0)
            {
                *pRet = (unsigned __int64)aLow * (unsigned __int64)bHigh;
            }
        }
        else if (bHigh == 0)
        {
            if (aHigh != 0)
            {
                *pRet = (unsigned __int64)aHigh * (unsigned __int64)bLow;
            }
        }
        else
        {
            E::SafeIntOnOverflow();
        }

        if (*pRet != 0)
        {
            unsigned __int64 tmp;

            if ((unsigned __int32)(*pRet >> 32) != 0) E::SafeIntOnOverflow();

            *pRet <<= 32;
            tmp = (unsigned __int64)aLow * (unsigned __int64)bLow;
            *pRet += tmp;

            if (*pRet < tmp) E::SafeIntOnOverflow();

            return;
        }

        *pRet = (unsigned __int64)aLow * (unsigned __int64)bLow;
#endif
    }
};

template<>
class LargeIntRegMultiply<unsigned __int64, unsigned __int32>
{
public:
    static bool RegMultiply(const unsigned __int64& a, unsigned __int32 b, unsigned __int64* pRet) SAFEINT_NOTHROW
    {
#if SAFEINT_USE_INTRINSICS
        return IntrinsicMultiplyUint64(a, (unsigned __int64)b, pRet);
#else
        unsigned __int32 aHigh, aLow;

        // Consider that a*b can be broken up into:
        // (aHigh * 2^32 + aLow) * b
        // => (aHigh * b * 2^32) + (aLow * b)

        aHigh = (unsigned __int32)(a >> 32);
        aLow = (unsigned __int32)a;

        *pRet = 0;

        if (aHigh != 0)
        {
            *pRet = (unsigned __int64)aHigh * (unsigned __int64)b;

            unsigned __int64 tmp;

            if ((unsigned __int32)(*pRet >> 32) != 0) return false;

            *pRet <<= 32;
            tmp = (unsigned __int64)aLow * (unsigned __int64)b;
            *pRet += tmp;

            if (*pRet < tmp) return false;

            return true;
        }

        *pRet = (unsigned __int64)aLow * (unsigned __int64)b;
        return true;
#endif
    }

    template<typename E>
    static void RegMultiplyThrow(const unsigned __int64& a,
                                 unsigned __int32 b,
                                 unsigned __int64* pRet) SAFEINT_CPP_THROW
    {
#if SAFEINT_USE_INTRINSICS
        if (!IntrinsicMultiplyUint64(a, (unsigned __int64)b, pRet)) E::SafeIntOnOverflow();
#else
        unsigned __int32 aHigh, aLow;

        // Consider that a*b can be broken up into:
        // (aHigh * 2^32 + aLow) * b
        // => (aHigh * b * 2^32) + (aLow * b)

        aHigh = (unsigned __int32)(a >> 32);
        aLow = (unsigned __int32)a;

        *pRet = 0;

        if (aHigh != 0)
        {
            *pRet = (unsigned __int64)aHigh * (unsigned __int64)b;

            unsigned __int64 tmp;

            if ((unsigned __int32)(*pRet >> 32) != 0) E::SafeIntOnOverflow();

            *pRet <<= 32;
            tmp = (unsigned __int64)aLow * (unsigned __int64)b;
            *pRet += tmp;

            if (*pRet < tmp) E::SafeIntOnOverflow();

            return;
        }

        *pRet = (unsigned __int64)aLow * (unsigned __int64)b;
        return;
#endif
    }
};

template<>
class LargeIntRegMultiply<unsigned __int64, signed __int32>
{
public:
    // Intrinsic not needed
    static bool RegMultiply(const unsigned __int64& a, signed __int32 b, unsigned __int64* pRet) SAFEINT_NOTHROW
    {
        if (b < 0 && a != 0) return false;

#if SAFEINT_USE_INTRINSICS
        return IntrinsicMultiplyUint64(a, (unsigned __int64)b, pRet);
#else
        return LargeIntRegMultiply<unsigned __int64, unsigned __int32>::RegMultiply(a, (unsigned __int32)b, pRet);
#endif
    }

    template<typename E>
    static void RegMultiplyThrow(const unsigned __int64& a, signed __int32 b, unsigned __int64* pRet) SAFEINT_CPP_THROW
    {
        if (b < 0 && a != 0) E::SafeIntOnOverflow();

#if SAFEINT_USE_INTRINSICS
        if (!IntrinsicMultiplyUint64(a, (unsigned __int64)b, pRet)) E::SafeIntOnOverflow();
#else
        LargeIntRegMultiply<unsigned __int64, unsigned __int32>::template RegMultiplyThrow<E>(
            a, (unsigned __int32)b, pRet);
#endif
    }
};

template<>
class LargeIntRegMultiply<unsigned __int64, signed __int64>
{
public:
    static bool RegMultiply(const unsigned __int64& a, signed __int64 b, unsigned __int64* pRet) SAFEINT_NOTHROW
    {
        if (b < 0 && a != 0) return false;

#if SAFEINT_USE_INTRINSICS
        return IntrinsicMultiplyUint64(a, (unsigned __int64)b, pRet);
#else
        return LargeIntRegMultiply<unsigned __int64, unsigned __int64>::RegMultiply(a, (unsigned __int64)b, pRet);
#endif
    }

    template<typename E>
    static void RegMultiplyThrow(const unsigned __int64& a, signed __int64 b, unsigned __int64* pRet) SAFEINT_CPP_THROW
    {
        if (b < 0 && a != 0) E::SafeIntOnOverflow();

#if SAFEINT_USE_INTRINSICS
        if (!IntrinsicMultiplyUint64(a, (unsigned __int64)b, pRet)) E::SafeIntOnOverflow();
#else
        LargeIntRegMultiply<unsigned __int64, unsigned __int64>::template RegMultiplyThrow<E>(
            a, (unsigned __int64)b, pRet);
#endif
    }
};

template<>
class LargeIntRegMultiply<signed __int32, unsigned __int64>
{
public:
    // Devolves into ordinary 64-bit calculation
    static bool RegMultiply(signed __int32 a, const unsigned __int64& b, signed __int32* pRet) SAFEINT_NOTHROW
    {
        unsigned __int32 bHigh, bLow;
        bool fIsNegative = false;

        // Consider that a*b can be broken up into:
        // (aHigh * 2^32 + aLow) * (bHigh * 2^32 + bLow)
        // => (aHigh * bHigh * 2^64) + (aLow * bHigh * 2^32) + (aHigh * bLow * 2^32) + (aLow * bLow)
        // aHigh == 0 implies:
        // ( aLow * bHigh * 2^32 ) + ( aLow + bLow )
        // If the first part is != 0, fail

        bHigh = (unsigned __int32)(b >> 32);
        bLow = (unsigned __int32)b;

        *pRet = 0;

        if (bHigh != 0 && a != 0) return false;

        if (a < 0)
        {
            a = (signed __int32)AbsValueHelper<signed __int32, GetAbsMethod<signed __int32>::method>::Abs(a);
            fIsNegative = true;
        }

        unsigned __int64 tmp = (unsigned __int32)a * (unsigned __int64)bLow;

        if (!fIsNegative)
        {
            if (tmp <= (unsigned __int64)IntTraits<signed __int32>::maxInt)
            {
                *pRet = (signed __int32)tmp;
                return true;
            }
        }
        else
        {
            if (tmp <= (unsigned __int64)IntTraits<signed __int32>::maxInt + 1)
            {
                *pRet = SignedNegation<signed __int32>::Value(tmp);
                return true;
            }
        }

        return false;
    }

    template<typename E>
    static void RegMultiplyThrow(signed __int32 a, const unsigned __int64& b, signed __int32* pRet) SAFEINT_CPP_THROW
    {
        unsigned __int32 bHigh, bLow;
        bool fIsNegative = false;

        // Consider that a*b can be broken up into:
        // (aHigh * 2^32 + aLow) * (bHigh * 2^32 + bLow)
        // => (aHigh * bHigh * 2^64) + (aLow * bHigh * 2^32) + (aHigh * bLow * 2^32) + (aLow * bLow)

        bHigh = (unsigned __int32)(b >> 32);
        bLow = (unsigned __int32)b;

        *pRet = 0;

        if (bHigh != 0 && a != 0) E::SafeIntOnOverflow();

        if (a < 0)
        {
            a = (signed __int32)AbsValueHelper<signed __int32, GetAbsMethod<signed __int32>::method>::Abs(a);
            fIsNegative = true;
        }

        unsigned __int64 tmp = (unsigned __int32)a * (unsigned __int64)bLow;

        if (!fIsNegative)
        {
            if (tmp <= (unsigned __int64)IntTraits<signed __int32>::maxInt)
            {
                *pRet = (signed __int32)tmp;
                return;
            }
        }
        else
        {
            if (tmp <= (unsigned __int64)IntTraits<signed __int32>::maxInt + 1)
            {
                *pRet = SignedNegation<signed __int32>::Value(tmp);
                return;
            }
        }

        E::SafeIntOnOverflow();
    }
};

template<>
class LargeIntRegMultiply<unsigned __int32, unsigned __int64>
{
public:
    // Becomes ordinary 64-bit multiplication, intrinsic not needed
    static bool RegMultiply(unsigned __int32 a, const unsigned __int64& b, unsigned __int32* pRet) SAFEINT_NOTHROW
    {
        // Consider that a*b can be broken up into:
        // (bHigh * 2^32 + bLow) * a
        // => (bHigh * a * 2^32) + (bLow * a)
        // In this case, the result must fit into 32-bits
        // If bHigh != 0 && a != 0, immediate error.

        if ((unsigned __int32)(b >> 32) != 0 && a != 0) return false;

        unsigned __int64 tmp = b * (unsigned __int64)a;

        if ((unsigned __int32)(tmp >> 32) != 0) // overflow
            return false;

        *pRet = (unsigned __int32)tmp;
        return true;
    }

    template<typename E>
    static void RegMultiplyThrow(unsigned __int32 a,
                                 const unsigned __int64& b,
                                 unsigned __int32* pRet) SAFEINT_CPP_THROW
    {
        if ((unsigned __int32)(b >> 32) != 0 && a != 0) E::SafeIntOnOverflow();

        unsigned __int64 tmp = b * (unsigned __int64)a;

        if ((unsigned __int32)(tmp >> 32) != 0) // overflow
            E::SafeIntOnOverflow();

        *pRet = (unsigned __int32)tmp;
    }
};

template<>
class LargeIntRegMultiply<unsigned __int32, signed __int64>
{
public:
    static bool RegMultiply(unsigned __int32 a, const signed __int64& b, unsigned __int32* pRet) SAFEINT_NOTHROW
    {
        if (b < 0 && a != 0) return false;
        return LargeIntRegMultiply<unsigned __int32, unsigned __int64>::RegMultiply(a, (unsigned __int64)b, pRet);
    }

    template<typename E>
    static void RegMultiplyThrow(unsigned __int32 a, const signed __int64& b, unsigned __int32* pRet) SAFEINT_CPP_THROW
    {
        if (b < 0 && a != 0) E::SafeIntOnOverflow();

        LargeIntRegMultiply<unsigned __int32, unsigned __int64>::template RegMultiplyThrow<E>(
            a, (unsigned __int64)b, pRet);
    }
};

template<>
class LargeIntRegMultiply<signed __int64, signed __int64>
{
public:
    static bool RegMultiply(const signed __int64& a, const signed __int64& b, signed __int64* pRet) SAFEINT_NOTHROW
    {
#if SAFEINT_USE_INTRINSICS
        return IntrinsicMultiplyInt64(a, b, pRet);
#else
        bool aNegative = false;
        bool bNegative = false;

        unsigned __int64 tmp;
        __int64 a1 = a;
        __int64 b1 = b;

        if (a1 < 0)
        {
            aNegative = true;
            a1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(a1);
        }

        if (b1 < 0)
        {
            bNegative = true;
            b1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(b1);
        }

        if (LargeIntRegMultiply<unsigned __int64, unsigned __int64>::RegMultiply(
                (unsigned __int64)a1, (unsigned __int64)b1, &tmp))
        {
            // The unsigned multiplication didn't overflow
            if (aNegative ^ bNegative)
            {
                // Result must be negative
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::minInt)
                {
                    *pRet = SignedNegation<signed __int64>::Value(tmp);
                    return true;
                }
            }
            else
            {
                // Result must be positive
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::maxInt)
                {
                    *pRet = (signed __int64)tmp;
                    return true;
                }
            }
        }

        return false;
#endif
    }

    template<typename E>
    static void RegMultiplyThrow(const signed __int64& a,
                                 const signed __int64& b,
                                 signed __int64* pRet) SAFEINT_CPP_THROW
    {
#if SAFEINT_USE_INTRINSICS
        if (!IntrinsicMultiplyInt64(a, b, pRet)) E::SafeIntOnOverflow();
#else
        bool aNegative = false;
        bool bNegative = false;

        unsigned __int64 tmp;
        __int64 a1 = a;
        __int64 b1 = b;

        if (a1 < 0)
        {
            aNegative = true;
            a1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(a1);
        }

        if (b1 < 0)
        {
            bNegative = true;
            b1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(b1);
        }

        LargeIntRegMultiply<unsigned __int64, unsigned __int64>::template RegMultiplyThrow<E>(
            (unsigned __int64)a1, (unsigned __int64)b1, &tmp);

        // The unsigned multiplication didn't overflow or we'd be in the exception handler
        if (aNegative ^ bNegative)
        {
            // Result must be negative
            if (tmp <= (unsigned __int64)IntTraits<signed __int64>::minInt)
            {
                *pRet = SignedNegation<signed __int64>::Value(tmp);
                return;
            }
        }
        else
        {
            // Result must be positive
            if (tmp <= (unsigned __int64)IntTraits<signed __int64>::maxInt)
            {
                *pRet = (signed __int64)tmp;
                return;
            }
        }

        E::SafeIntOnOverflow();
#endif
    }
};

template<>
class LargeIntRegMultiply<signed __int64, unsigned __int32>
{
public:
    static bool RegMultiply(const signed __int64& a, unsigned __int32 b, signed __int64* pRet) SAFEINT_NOTHROW
    {
#if SAFEINT_USE_INTRINSICS
        return IntrinsicMultiplyInt64(a, (signed __int64)b, pRet);
#else
        bool aNegative = false;
        unsigned __int64 tmp;
        __int64 a1 = a;

        if (a1 < 0)
        {
            aNegative = true;
            a1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(a1);
        }

        if (LargeIntRegMultiply<unsigned __int64, unsigned __int32>::RegMultiply((unsigned __int64)a1, b, &tmp))
        {
            // The unsigned multiplication didn't overflow
            if (aNegative)
            {
                // Result must be negative
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::minInt)
                {
                    *pRet = SignedNegation<signed __int64>::Value(tmp);
                    return true;
                }
            }
            else
            {
                // Result must be positive
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::maxInt)
                {
                    *pRet = (signed __int64)tmp;
                    return true;
                }
            }
        }

        return false;
#endif
    }

    template<typename E>
    static void RegMultiplyThrow(const signed __int64& a, unsigned __int32 b, signed __int64* pRet) SAFEINT_CPP_THROW
    {
#if SAFEINT_USE_INTRINSICS
        if (!IntrinsicMultiplyInt64(a, (signed __int64)b, pRet)) E::SafeIntOnOverflow();
#else
        bool aNegative = false;
        unsigned __int64 tmp;
        __int64 a1 = a;

        if (a1 < 0)
        {
            aNegative = true;
            a1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(a1);
        }

        LargeIntRegMultiply<unsigned __int64, unsigned __int32>::template RegMultiplyThrow<E>(
            (unsigned __int64)a1, b, &tmp);

        // The unsigned multiplication didn't overflow
        if (aNegative)
        {
            // Result must be negative
            if (tmp <= (unsigned __int64)IntTraits<signed __int64>::minInt)
            {
                *pRet = SignedNegation<signed __int64>::Value(tmp);
                return;
            }
        }
        else
        {
            // Result must be positive
            if (tmp <= (unsigned __int64)IntTraits<signed __int64>::maxInt)
            {
                *pRet = (signed __int64)tmp;
                return;
            }
        }

        E::SafeIntOnOverflow();
#endif
    }
};

template<>
class LargeIntRegMultiply<signed __int64, signed __int32>
{
public:
    static bool RegMultiply(const signed __int64& a, signed __int32 b, signed __int64* pRet) SAFEINT_NOTHROW
    {
#if SAFEINT_USE_INTRINSICS
        return IntrinsicMultiplyInt64(a, (signed __int64)b, pRet);
#else
        bool aNegative = false;
        bool bNegative = false;

        unsigned __int64 tmp;
        __int64 a1 = a;
        __int64 b1 = b;

        if (a1 < 0)
        {
            aNegative = true;
            a1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(a1);
        }

        if (b1 < 0)
        {
            bNegative = true;
            b1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(b1);
        }

        if (LargeIntRegMultiply<unsigned __int64, unsigned __int32>::RegMultiply(
                (unsigned __int64)a1, (unsigned __int32)b1, &tmp))
        {
            // The unsigned multiplication didn't overflow
            if (aNegative ^ bNegative)
            {
                // Result must be negative
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::minInt)
                {
                    *pRet = SignedNegation<signed __int64>::Value(tmp);
                    return true;
                }
            }
            else
            {
                // Result must be positive
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::maxInt)
                {
                    *pRet = (signed __int64)tmp;
                    return true;
                }
            }
        }

        return false;
#endif
    }

    template<typename E>
    static void RegMultiplyThrow(signed __int64 a, signed __int32 b, signed __int64* pRet) SAFEINT_CPP_THROW
    {
#if SAFEINT_USE_INTRINSICS
        if (!IntrinsicMultiplyInt64(a, (signed __int64)b, pRet)) E::SafeIntOnOverflow();
#else
        bool aNegative = false;
        bool bNegative = false;

        unsigned __int64 tmp;

        if (a < 0)
        {
            aNegative = true;
            a = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(a);
        }

        if (b < 0)
        {
            bNegative = true;
            b = (signed __int32)AbsValueHelper<signed __int32, GetAbsMethod<signed __int32>::method>::Abs(b);
        }

        LargeIntRegMultiply<unsigned __int64, unsigned __int32>::template RegMultiplyThrow<E>(
            (unsigned __int64)a, (unsigned __int32)b, &tmp);

        // The unsigned multiplication didn't overflow
        if (aNegative ^ bNegative)
        {
            // Result must be negative
            if (tmp <= (unsigned __int64)IntTraits<signed __int64>::minInt)
            {
                *pRet = SignedNegation<signed __int64>::Value(tmp);
                return;
            }
        }
        else
        {
            // Result must be positive
            if (tmp <= (unsigned __int64)IntTraits<signed __int64>::maxInt)
            {
                *pRet = (signed __int64)tmp;
                return;
            }
        }

        E::SafeIntOnOverflow();
#endif
    }
};

template<>
class LargeIntRegMultiply<signed __int32, signed __int64>
{
public:
    static bool RegMultiply(signed __int32 a, const signed __int64& b, signed __int32* pRet) SAFEINT_NOTHROW
    {
#if SAFEINT_USE_INTRINSICS
        __int64 tmp;

        if (IntrinsicMultiplyInt64(a, b, &tmp))
        {
            if (tmp > IntTraits<signed __int32>::maxInt || tmp < IntTraits<signed __int32>::minInt)
            {
                return false;
            }

            *pRet = (__int32)tmp;
            return true;
        }
        return false;
#else
        bool aNegative = false;
        bool bNegative = false;

        unsigned __int32 tmp;
        __int64 b1 = b;

        if (a < 0)
        {
            aNegative = true;
            a = (signed __int32)AbsValueHelper<signed __int32, GetAbsMethod<signed __int32>::method>::Abs(a);
        }

        if (b1 < 0)
        {
            bNegative = true;
            b1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(b1);
        }

        if (LargeIntRegMultiply<unsigned __int32, unsigned __int64>::RegMultiply(
                (unsigned __int32)a, (unsigned __int64)b1, &tmp))
        {
            // The unsigned multiplication didn't overflow
            if (aNegative ^ bNegative)
            {
                // Result must be negative
                if (tmp <= (unsigned __int32)IntTraits<signed __int32>::minInt)
                {
                    *pRet = SignedNegation<signed __int32>::Value(tmp);
                    return true;
                }
            }
            else
            {
                // Result must be positive
                if (tmp <= (unsigned __int32)IntTraits<signed __int32>::maxInt)
                {
                    *pRet = (signed __int32)tmp;
                    return true;
                }
            }
        }

        return false;
#endif
    }

    template<typename E>
    static void RegMultiplyThrow(signed __int32 a, const signed __int64& b, signed __int32* pRet) SAFEINT_CPP_THROW
    {
#if SAFEINT_USE_INTRINSICS
        __int64 tmp;

        if (IntrinsicMultiplyInt64(a, b, &tmp))
        {
            if (tmp > IntTraits<signed __int32>::maxInt || tmp < IntTraits<signed __int32>::minInt)
            {
                E::SafeIntOnOverflow();
            }

            *pRet = (__int32)tmp;
            return;
        }
        E::SafeIntOnOverflow();
#else
        bool aNegative = false;
        bool bNegative = false;

        unsigned __int32 tmp;
        signed __int64 b2 = b;

        if (a < 0)
        {
            aNegative = true;
            a = (signed __int32)AbsValueHelper<signed __int32, GetAbsMethod<signed __int32>::method>::Abs(a);
        }

        if (b < 0)
        {
            bNegative = true;
            b2 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(b2);
        }

        LargeIntRegMultiply<unsigned __int32, unsigned __int64>::template RegMultiplyThrow<E>(
            (unsigned __int32)a, (unsigned __int64)b2, &tmp);

        // The unsigned multiplication didn't overflow
        if (aNegative ^ bNegative)
        {
            // Result must be negative
            if (tmp <= (unsigned __int32)IntTraits<signed __int32>::minInt)
            {
                *pRet = SignedNegation<signed __int32>::Value(tmp);
                return;
            }
        }
        else
        {
            // Result must be positive
            if (tmp <= (unsigned __int32)IntTraits<signed __int32>::maxInt)
            {
                *pRet = (signed __int32)tmp;
                return;
            }
        }

        E::SafeIntOnOverflow();
#endif
    }
};

template<>
class LargeIntRegMultiply<signed __int64, unsigned __int64>
{
public:
    // Leave this one as-is - will call unsigned intrinsic internally
    static bool RegMultiply(const signed __int64& a, const unsigned __int64& b, signed __int64* pRet) SAFEINT_NOTHROW
    {
        bool aNegative = false;

        unsigned __int64 tmp;
        __int64 a1 = a;

        if (a1 < 0)
        {
            aNegative = true;
            a1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(a1);
        }

        if (LargeIntRegMultiply<unsigned __int64, unsigned __int64>::RegMultiply(
                (unsigned __int64)a1, (unsigned __int64)b, &tmp))
        {
            // The unsigned multiplication didn't overflow
            if (aNegative)
            {
                // Result must be negative
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::minInt)
                {
                    *pRet = SignedNegation<signed __int64>::Value(tmp);
                    return true;
                }
            }
            else
            {
                // Result must be positive
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::maxInt)
                {
                    *pRet = (signed __int64)tmp;
                    return true;
                }
            }
        }

        return false;
    }

    template<typename E>
    static void RegMultiplyThrow(const signed __int64& a,
                                 const unsigned __int64& b,
                                 signed __int64* pRet) SAFEINT_CPP_THROW
    {
        bool aNegative = false;
        unsigned __int64 tmp;
        __int64 a1 = a;

        if (a1 < 0)
        {
            aNegative = true;
            a1 = (signed __int64)AbsValueHelper<signed __int64, GetAbsMethod<signed __int64>::method>::Abs(a1);
        }

        if (LargeIntRegMultiply<unsigned __int64, unsigned __int64>::RegMultiply(
                (unsigned __int64)a1, (unsigned __int64)b, &tmp))
        {
            // The unsigned multiplication didn't overflow
            if (aNegative)
            {
                // Result must be negative
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::minInt)
                {
                    *pRet = SignedNegation<signed __int64>::Value(tmp);
                    return;
                }
            }
            else
            {
                // Result must be positive
                if (tmp <= (unsigned __int64)IntTraits<signed __int64>::maxInt)
                {
                    *pRet = (signed __int64)tmp;
                    return;
                }
            }
        }

        E::SafeIntOnOverflow();
    }
};

// In all of the following functions where LargeIntRegMultiply methods are called,
// we need to properly transition types. The methods need __int64, __int32, etc.
// but the variables being passed to us could be long long, long int, or long, depending on
// the compiler. Microsoft compiler knows that long long is the same type as __int64, but gcc doesn't

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_Uint64Uint64>
{
public:
    // T, U are unsigned __int64
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isUint64 && IntTraits<U>::isUint64);
        unsigned __int64 t1 = t;
        unsigned __int64 u1 = u;
        return LargeIntRegMultiply<unsigned __int64, unsigned __int64>::RegMultiply(
            t1, u1, reinterpret_cast<unsigned __int64*>(&ret));
    }

    template<typename E>
    static void MultiplyThrow(const unsigned __int64& t, const unsigned __int64& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isUint64 && IntTraits<U>::isUint64);
        unsigned __int64 t1 = t;
        unsigned __int64 u1 = u;
        LargeIntRegMultiply<unsigned __int64, unsigned __int64>::template RegMultiplyThrow<E>(
            t1, u1, reinterpret_cast<unsigned __int64*>(&ret));
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_Uint64Uint>
{
public:
    // T is unsigned __int64
    // U is any unsigned int 32-bit or less
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isUint64);
        unsigned __int64 t1 = t;
        return LargeIntRegMultiply<unsigned __int64, unsigned __int32>::RegMultiply(
            t1, (unsigned __int32)u, reinterpret_cast<unsigned __int64*>(&ret));
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isUint64);
        unsigned __int64 t1 = t;
        LargeIntRegMultiply<unsigned __int64, unsigned __int32>::template RegMultiplyThrow<E>(
            t1, (unsigned __int32)u, reinterpret_cast<unsigned __int64*>(&ret));
    }
};

// converse of the previous function
template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_UintUint64>
{
public:
    // T is any unsigned int up to 32-bit
    // U is unsigned __int64
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<U>::isUint64);
        unsigned __int64 u1 = u;
        unsigned __int32 tmp;

        if (LargeIntRegMultiply<unsigned __int32, unsigned __int64>::RegMultiply(t, u1, &tmp) &&
            SafeCastHelper<T, unsigned __int32, GetCastMethod<T, unsigned __int32>::method>::Cast(tmp, ret))
        {
            return true;
        }

        return false;
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<U>::isUint64);
        unsigned __int64 u1 = u;
        unsigned __int32 tmp;

        LargeIntRegMultiply<unsigned __int32, unsigned __int64>::template RegMultiplyThrow<E>(t, u1, &tmp);
        SafeCastHelper<T, unsigned __int32, GetCastMethod<T, unsigned __int32>::method>::template CastThrow<E>(tmp,
                                                                                                               ret);
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_Uint64Int>
{
public:
    // T is unsigned __int64
    // U is any signed int, up to 64-bit
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isUint64);
        unsigned __int64 t1 = t;
        return LargeIntRegMultiply<unsigned __int64, signed __int32>::RegMultiply(
            t1, (signed __int32)u, reinterpret_cast<unsigned __int64*>(&ret));
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isUint64);
        unsigned __int64 t1 = t;
        LargeIntRegMultiply<unsigned __int64, signed __int32>::template RegMultiplyThrow<E>(
            t1, (signed __int32)u, reinterpret_cast<unsigned __int64*>(&ret));
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_Uint64Int64>
{
public:
    // T is unsigned __int64
    // U is __int64
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isUint64 && IntTraits<U>::isInt64);
        unsigned __int64 t1 = t;
        __int64 u1 = u;
        return LargeIntRegMultiply<unsigned __int64, __int64>::RegMultiply(
            t1, u1, reinterpret_cast<unsigned __int64*>(&ret));
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isUint64 && IntTraits<U>::isInt64);
        unsigned __int64 t1 = t;
        __int64 u1 = u;
        LargeIntRegMultiply<unsigned __int64, __int64>::template RegMultiplyThrow<E>(
            t1, u1, reinterpret_cast<unsigned __int64*>(&ret));
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_UintInt64>
{
public:
    // T is unsigned up to 32-bit
    // U is __int64
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<U>::isInt64);
        __int64 u1 = u;
        unsigned __int32 tmp;

        if (LargeIntRegMultiply<unsigned __int32, __int64>::RegMultiply((unsigned __int32)t, u1, &tmp) &&
            SafeCastHelper<T, unsigned __int32, GetCastMethod<T, unsigned __int32>::method>::Cast(tmp, ret))
        {
            return true;
        }

        return false;
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<U>::isInt64);
        __int64 u1 = u;
        unsigned __int32 tmp;

        LargeIntRegMultiply<unsigned __int32, __int64>::template RegMultiplyThrow<E>((unsigned __int32)t, u1, &tmp);
        SafeCastHelper<T, unsigned __int32, GetCastMethod<T, unsigned __int32>::method>::template CastThrow<E>(tmp,
                                                                                                               ret);
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_Int64Uint>
{
public:
    // T is __int64
    // U is unsigned up to 32-bit
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isInt64);
        __int64 t1 = t;
        return LargeIntRegMultiply<__int64, unsigned __int32>::RegMultiply(
            t1, (unsigned __int32)u, reinterpret_cast<__int64*>(&ret));
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isInt64);
        __int64 t1 = t;
        LargeIntRegMultiply<__int64, unsigned __int32>::template RegMultiplyThrow<E>(
            t1, (unsigned __int32)u, reinterpret_cast<__int64*>(&ret));
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_Int64Int64>
{
public:
    // T, U are __int64
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isInt64 && IntTraits<U>::isInt64);
        __int64 t1 = t;
        __int64 u1 = u;
        return LargeIntRegMultiply<__int64, __int64>::RegMultiply(t1, u1, reinterpret_cast<__int64*>(&ret));
    }

    template<typename E>
    static void MultiplyThrow(const T& t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isInt64 && IntTraits<U>::isInt64);
        __int64 t1 = t;
        __int64 u1 = u;
        LargeIntRegMultiply<__int64, __int64>::template RegMultiplyThrow<E>(t1, u1, reinterpret_cast<__int64*>(&ret));
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_Int64Int>
{
public:
    // T is __int64
    // U is signed up to 32-bit
    static bool Multiply(const T& t, U u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isInt64);
        __int64 t1 = t;
        return LargeIntRegMultiply<__int64, __int32>::RegMultiply(t1, (__int32)u, reinterpret_cast<__int64*>(&ret));
    }

    template<typename E>
    static void MultiplyThrow(const __int64& t, U u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isInt64);
        __int64 t1 = t;
        LargeIntRegMultiply<__int64, __int32>::template RegMultiplyThrow<E>(
            t1, (__int32)u, reinterpret_cast<__int64*>(&ret));
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_IntUint64>
{
public:
    // T is signed up to 32-bit
    // U is unsigned __int64
    static bool Multiply(T t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<U>::isUint64);
        unsigned __int64 u1 = u;
        __int32 tmp;

        if (LargeIntRegMultiply<__int32, unsigned __int64>::RegMultiply((__int32)t, u1, &tmp) &&
            SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::Cast(tmp, ret))
        {
            return true;
        }

        return false;
    }

    template<typename E>
    static void MultiplyThrow(T t, const unsigned __int64& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<U>::isUint64);
        unsigned __int64 u1 = u;
        __int32 tmp;

        LargeIntRegMultiply<__int32, unsigned __int64>::template RegMultiplyThrow<E>((__int32)t, u1, &tmp);
        SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::template CastThrow<E>(tmp, ret);
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_Int64Uint64>
{
public:
    // T is __int64
    // U is unsigned __int64
    static bool Multiply(const T& t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isInt64 && IntTraits<U>::isUint64);
        __int64 t1 = t;
        unsigned __int64 u1 = u;
        return LargeIntRegMultiply<__int64, unsigned __int64>::RegMultiply(t1, u1, reinterpret_cast<__int64*>(&ret));
    }

    template<typename E>
    static void MultiplyThrow(const __int64& t, const unsigned __int64& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isInt64 && IntTraits<U>::isUint64);
        __int64 t1 = t;
        unsigned __int64 u1 = u;
        LargeIntRegMultiply<__int64, unsigned __int64>::template RegMultiplyThrow<E>(
            t1, u1, reinterpret_cast<__int64*>(&ret));
    }
};

template<typename T, typename U>
class MultiplicationHelper<T, U, MultiplicationState_IntInt64>
{
public:
    // T is signed, up to 32-bit
    // U is __int64
    static bool Multiply(T t, const U& u, T& ret) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<U>::isInt64);
        __int64 u1 = u;
        __int32 tmp;

        if (LargeIntRegMultiply<__int32, __int64>::RegMultiply((__int32)t, u1, &tmp) &&
            SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::Cast(tmp, ret))
        {
            return true;
        }

        return false;
    }

    template<typename E>
    static void MultiplyThrow(T t, const U& u, T& ret) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<U>::isInt64);
        __int64 u1 = u;
        __int32 tmp;

        LargeIntRegMultiply<__int32, __int64>::template RegMultiplyThrow<E>((__int32)t, u1, &tmp);
        SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::template CastThrow<E>(tmp, ret);
    }
};

enum DivisionState
{
    DivisionState_OK,
    DivisionState_UnsignedSigned,
    DivisionState_SignedUnsigned32,
    DivisionState_SignedUnsigned64,
    DivisionState_SignedUnsigned,
    DivisionState_SignedSigned
};

template<typename T, typename U>
class DivisionMethod
{
public:
    enum
    {
        method =
            (SafeIntCompare<T, U>::isBothUnsigned
                 ? DivisionState_OK
                 : (!IntTraits<T>::isSigned && IntTraits<U>::isSigned)
                       ? DivisionState_UnsignedSigned
                       : (IntTraits<T>::isSigned && IntTraits<U>::isUint32 && IntTraits<T>::isLT64Bit)
                             ? DivisionState_SignedUnsigned32
                             : (IntTraits<T>::isSigned && IntTraits<U>::isUint64)
                                   ? DivisionState_SignedUnsigned64
                                   : (IntTraits<T>::isSigned && !IntTraits<U>::isSigned) ? DivisionState_SignedUnsigned
                                                                                         : DivisionState_SignedSigned)
    };
};

template<typename T, typename U, int state>
class DivisionHelper;

template<typename T, typename U>
class DivisionHelper<T, U, DivisionState_OK>
{
public:
    static SafeIntError Divide(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0) return SafeIntDivideByZero;

        if (t == 0)
        {
            result = 0;
            return SafeIntNoError;
        }

        result = (T)(t / u);
        return SafeIntNoError;
    }

    template<typename E>
    static void DivideThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0) E::SafeIntOnDivZero();

        if (t == 0)
        {
            result = 0;
            return;
        }

        result = (T)(t / u);
    }
};

template<typename T, typename U>
class DivisionHelper<T, U, DivisionState_UnsignedSigned>
{
public:
    static SafeIntError Divide(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0) return SafeIntDivideByZero;

        if (t == 0)
        {
            result = 0;
            return SafeIntNoError;
        }

        if (u > 0)
        {
            result = (T)(t / u);
            return SafeIntNoError;
        }

        // it is always an error to try and divide an unsigned number by a negative signed number
        // unless u is bigger than t
        if (AbsValueHelper<U, GetAbsMethod<U>::method>::Abs(u) > t)
        {
            result = 0;
            return SafeIntNoError;
        }

        return SafeIntArithmeticOverflow;
    }

    template<typename E>
    static void DivideThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0) E::SafeIntOnDivZero();

        if (t == 0)
        {
            result = 0;
            return;
        }

        if (u > 0)
        {
            result = (T)(t / u);
            return;
        }

        // it is always an error to try and divide an unsigned number by a negative signed number
        // unless u is bigger than t
        if (AbsValueHelper<U, GetAbsMethod<U>::method>::Abs(u) > t)
        {
            result = 0;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class DivisionHelper<T, U, DivisionState_SignedUnsigned32>
{
public:
    static SafeIntError Divide(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0) return SafeIntDivideByZero;

        if (t == 0)
        {
            result = 0;
            return SafeIntNoError;
        }

        // Test for t > 0
        // If t < 0, must explicitly upcast, or implicit upcast to ulong will cause errors
        // As it turns out, 32-bit division is about twice as fast, which justifies the extra conditional

        if (t > 0)
            result = (T)(t / u);
        else
            result = (T)((__int64)t / (__int64)u);

        return SafeIntNoError;
    }

    template<typename E>
    static void DivideThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0)
        {
            E::SafeIntOnDivZero();
        }

        if (t == 0)
        {
            result = 0;
            return;
        }

        // Test for t > 0
        // If t < 0, must explicitly upcast, or implicit upcast to ulong will cause errors
        // As it turns out, 32-bit division is about twice as fast, which justifies the extra conditional

        if (t > 0)
            result = (T)(t / u);
        else
            result = (T)((__int64)t / (__int64)u);
    }
};

template<typename T, typename U>
class DivisionHelper<T, U, DivisionState_SignedUnsigned64>
{
public:
    static SafeIntError Divide(const T& t, const unsigned __int64& u, T& result) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<U>::isUint64);

        if (u == 0)
        {
            return SafeIntDivideByZero;
        }

        if (t == 0)
        {
            result = 0;
            return SafeIntNoError;
        }

        if (u <= (unsigned __int64)IntTraits<T>::maxInt)
        {
            // Else u can safely be cast to T
            if (CompileConst<sizeof(T) < sizeof(__int64)>::Value())
                result = (T)((int)t / (int)u);
            else
                result = (T)((__int64)t / (__int64)u);
        }
        else // Corner case
            if (t == IntTraits<T>::minInt && u == (unsigned __int64)IntTraits<T>::minInt)
        {
            // Min int divided by it's own magnitude is -1
            result = -1;
        }
        else
        {
            result = 0;
        }
        return SafeIntNoError;
    }

    template<typename E>
    static void DivideThrow(const T& t, const unsigned __int64& u, T& result) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<U>::isUint64);

        if (u == 0)
        {
            E::SafeIntOnDivZero();
        }

        if (t == 0)
        {
            result = 0;
            return;
        }

        if (u <= (unsigned __int64)IntTraits<T>::maxInt)
        {
            // Else u can safely be cast to T
            if (CompileConst<sizeof(T) < sizeof(__int64)>::Value())
                result = (T)((int)t / (int)u);
            else
                result = (T)((__int64)t / (__int64)u);
        }
        else // Corner case
            if (t == IntTraits<T>::minInt && u == (unsigned __int64)IntTraits<T>::minInt)
        {
            // Min int divided by it's own magnitude is -1
            result = -1;
        }
        else
        {
            result = 0;
        }
    }
};

template<typename T, typename U>
class DivisionHelper<T, U, DivisionState_SignedUnsigned>
{
public:
    // T is any signed, U is unsigned and smaller than 32-bit
    // In this case, standard operator casting is correct
    static SafeIntError Divide(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0)
        {
            return SafeIntDivideByZero;
        }

        if (t == 0)
        {
            result = 0;
            return SafeIntNoError;
        }

        result = (T)(t / u);
        return SafeIntNoError;
    }

    template<typename E>
    static void DivideThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0)
        {
            E::SafeIntOnDivZero();
        }

        if (t == 0)
        {
            result = 0;
            return;
        }

        result = (T)(t / u);
    }
};

template<typename T, typename U>
class DivisionHelper<T, U, DivisionState_SignedSigned>
{
public:
    static SafeIntError Divide(const T& t, const U& u, T& result) SAFEINT_NOTHROW
    {
        if (u == 0)
        {
            return SafeIntDivideByZero;
        }

        if (t == 0)
        {
            result = 0;
            return SafeIntNoError;
        }

        // Must test for corner case
        if (t == IntTraits<T>::minInt && u == (U)-1) return SafeIntArithmeticOverflow;

        result = (T)(t / u);
        return SafeIntNoError;
    }

    template<typename E>
    static void DivideThrow(const T& t, const U& u, T& result) SAFEINT_CPP_THROW
    {
        if (u == 0)
        {
            E::SafeIntOnDivZero();
        }

        if (t == 0)
        {
            result = 0;
            return;
        }

        // Must test for corner case
        if (t == IntTraits<T>::minInt && u == (U)-1) E::SafeIntOnOverflow();

        result = (T)(t / u);
    }
};

enum AdditionState
{
    AdditionState_CastIntCheckMax,
    AdditionState_CastUintCheckOverflow,
    AdditionState_CastUintCheckOverflowMax,
    AdditionState_CastUint64CheckOverflow,
    AdditionState_CastUint64CheckOverflowMax,
    AdditionState_CastIntCheckSafeIntMinMax,
    AdditionState_CastInt64CheckSafeIntMinMax,
    AdditionState_CastInt64CheckMax,
    AdditionState_CastUint64CheckSafeIntMinMax,
    AdditionState_CastUint64CheckSafeIntMinMax2,
    AdditionState_CastInt64CheckOverflow,
    AdditionState_CastInt64CheckOverflowSafeIntMinMax,
    AdditionState_CastInt64CheckOverflowMax,
    AdditionState_ManualCheckInt64Uint64,
    AdditionState_ManualCheck,
    AdditionState_Error
};

template<typename T, typename U>
class AdditionMethod
{
public:
    enum
    {
        // unsigned-unsigned
        method =
            (IntRegion<T, U>::IntZone_UintLT32_UintLT32
                 ? AdditionState_CastIntCheckMax
                 : (IntRegion<T, U>::IntZone_Uint32_UintLT64)
                       ? AdditionState_CastUintCheckOverflow
                       : (IntRegion<T, U>::IntZone_UintLT32_Uint32)
                             ? AdditionState_CastUintCheckOverflowMax
                             : (IntRegion<T, U>::IntZone_Uint64_Uint)
                                   ? AdditionState_CastUint64CheckOverflow
                                   : (IntRegion<T, U>::IntZone_UintLT64_Uint64)
                                         ? AdditionState_CastUint64CheckOverflowMax
                                         :
                                         // unsigned-signed
                                         (IntRegion<T, U>::IntZone_UintLT32_IntLT32)
                                             ? AdditionState_CastIntCheckSafeIntMinMax
                                             : (IntRegion<T, U>::IntZone_Uint32_IntLT64 ||
                                                IntRegion<T, U>::IntZone_UintLT32_Int32)
                                                   ? AdditionState_CastInt64CheckSafeIntMinMax
                                                   : (IntRegion<T, U>::IntZone_Uint64_Int ||
                                                      IntRegion<T, U>::IntZone_Uint64_Int64)
                                                         ? AdditionState_CastUint64CheckSafeIntMinMax
                                                         : (IntRegion<T, U>::IntZone_UintLT64_Int64)
                                                               ? AdditionState_CastUint64CheckSafeIntMinMax2
                                                               :
                                                               // signed-signed
                                                               (IntRegion<T, U>::IntZone_IntLT32_IntLT32)
                                                                   ? AdditionState_CastIntCheckSafeIntMinMax
                                                                   : (IntRegion<T, U>::IntZone_Int32_IntLT64 ||
                                                                      IntRegion<T, U>::IntZone_IntLT32_Int32)
                                                                         ? AdditionState_CastInt64CheckSafeIntMinMax
                                                                         : (IntRegion<T, U>::IntZone_Int64_Int ||
                                                                            IntRegion<T, U>::IntZone_Int64_Int64)
                                                                               ? AdditionState_CastInt64CheckOverflow
                                                                               : (IntRegion<T,
                                                                                            U>::IntZone_IntLT64_Int64)
                                                                                     ? AdditionState_CastInt64CheckOverflowSafeIntMinMax
                                                                                     :
                                                                                     // signed-unsigned
                                                                                     (IntRegion<T, U>::
                                                                                          IntZone_IntLT32_UintLT32)
                                                                                         ? AdditionState_CastIntCheckMax
                                                                                         : (IntRegion<T, U>::
                                                                                                IntZone_Int32_UintLT32 ||
                                                                                            IntRegion<T, U>::
                                                                                                IntZone_IntLT64_Uint32)
                                                                                               ? AdditionState_CastInt64CheckMax
                                                                                               : (IntRegion<T, U>::
                                                                                                      IntZone_Int64_UintLT64)
                                                                                                     ? AdditionState_CastInt64CheckOverflowMax
                                                                                                     : (IntRegion<T,
                                                                                                                  U>::
                                                                                                            IntZone_Int64_Uint64)
                                                                                                           ? AdditionState_ManualCheckInt64Uint64
                                                                                                           : (IntRegion<
                                                                                                                 T,
                                                                                                                 U>::
                                                                                                                  IntZone_Int_Uint64)
                                                                                                                 ? AdditionState_ManualCheck
                                                                                                                 : AdditionState_Error)
    };
};

template<typename T, typename U, int method>
class AdditionHelper;

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastIntCheckMax>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // 16-bit or less unsigned addition
        __int32 tmp = lhs + rhs;

        if (tmp <= (__int32)IntTraits<T>::maxInt)
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // 16-bit or less unsigned addition
        __int32 tmp = lhs + rhs;

        if (tmp <= (__int32)IntTraits<T>::maxInt)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastUintCheckOverflow>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // 32-bit or less - both are unsigned
        unsigned __int32 tmp = (unsigned __int32)lhs + (unsigned __int32)rhs;

        // we added didn't get smaller
        if (tmp >= lhs)
        {
            result = (T)tmp;
            return true;
        }
        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // 32-bit or less - both are unsigned
        unsigned __int32 tmp = (unsigned __int32)lhs + (unsigned __int32)rhs;

        // we added didn't get smaller
        if (tmp >= lhs)
        {
            result = (T)tmp;
            return;
        }
        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastUintCheckOverflowMax>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // 32-bit or less - both are unsigned
        unsigned __int32 tmp = (unsigned __int32)lhs + (unsigned __int32)rhs;

        // We added and it didn't get smaller or exceed maxInt
        if (tmp >= lhs && tmp <= IntTraits<T>::maxInt)
        {
            result = (T)tmp;
            return true;
        }
        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // 32-bit or less - both are unsigned
        unsigned __int32 tmp = (unsigned __int32)lhs + (unsigned __int32)rhs;

        // We added and it didn't get smaller or exceed maxInt
        if (tmp >= lhs && tmp <= IntTraits<T>::maxInt)
        {
            result = (T)tmp;
            return;
        }
        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastUint64CheckOverflow>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs unsigned __int64, rhs unsigned
        unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

        // We added and it didn't get smaller
        if (tmp >= lhs)
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs unsigned __int64, rhs unsigned
        unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

        // We added and it didn't get smaller
        if (tmp >= lhs)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastUint64CheckOverflowMax>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs unsigned __int64, rhs unsigned
        unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

        // We added and it didn't get smaller
        if (tmp >= lhs && tmp <= IntTraits<T>::maxInt)
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs unsigned __int64, rhs unsigned
        unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

        // We added and it didn't get smaller
        if (tmp >= lhs && tmp <= IntTraits<T>::maxInt)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastIntCheckSafeIntMinMax>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // 16-bit or less - one or both are signed
        __int32 tmp = lhs + rhs;

        if (tmp <= (__int32)IntTraits<T>::maxInt && tmp >= (__int32)IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // 16-bit or less - one or both are signed
        __int32 tmp = lhs + rhs;

        if (tmp <= (__int32)IntTraits<T>::maxInt && tmp >= (__int32)IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastInt64CheckSafeIntMinMax>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // 32-bit or less - one or both are signed
        __int64 tmp = (__int64)lhs + (__int64)rhs;

        if (tmp <= (__int64)IntTraits<T>::maxInt && tmp >= (__int64)IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // 32-bit or less - one or both are signed
        __int64 tmp = (__int64)lhs + (__int64)rhs;

        if (tmp <= (__int64)IntTraits<T>::maxInt && tmp >= (__int64)IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastInt64CheckMax>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // 32-bit or less - lhs signed, rhs unsigned
        __int64 tmp = (__int64)lhs + (__int64)rhs;

        if (tmp <= IntTraits<T>::maxInt)
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // 32-bit or less - lhs signed, rhs unsigned
        __int64 tmp = (__int64)lhs + (__int64)rhs;

        if (tmp <= IntTraits<T>::maxInt)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastUint64CheckSafeIntMinMax>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is unsigned __int64, rhs signed
        unsigned __int64 tmp;

        if (rhs < 0)
        {
            // So we're effectively subtracting
            tmp = AbsValueHelper<U, GetAbsMethod<U>::method>::Abs(rhs);

            if (tmp <= lhs)
            {
                result = lhs - tmp;
                return true;
            }
        }
        else
        {
            // now we know that rhs can be safely cast into an unsigned __int64
            tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

            // We added and it did not become smaller
            if (tmp >= lhs)
            {
                result = (T)tmp;
                return true;
            }
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is unsigned __int64, rhs signed
        unsigned __int64 tmp;

        if (rhs < 0)
        {
            // So we're effectively subtracting
            tmp = AbsValueHelper<U, GetAbsMethod<U>::method>::Abs(rhs);

            if (tmp <= lhs)
            {
                result = lhs - tmp;
                return;
            }
        }
        else
        {
            // now we know that rhs can be safely cast into an unsigned __int64
            tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

            // We added and it did not become smaller
            if (tmp >= lhs)
            {
                result = (T)tmp;
                return;
            }
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastUint64CheckSafeIntMinMax2>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is unsigned and < 64-bit, rhs signed __int64
        if (rhs < 0)
        {
            if (lhs >= ~(unsigned __int64)(rhs) + 1) // negation is safe, since rhs is 64-bit
            {
                result = (T)(lhs + rhs);
                return true;
            }
        }
        else
        {
            // now we know that rhs can be safely cast into an unsigned __int64
            unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

            // special case - rhs cannot be larger than 0x7fffffffffffffff, lhs cannot be larger than 0xffffffff
            // it is not possible for the operation above to overflow, so just check max
            if (tmp <= IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return true;
            }
        }
        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is unsigned and < 64-bit, rhs signed __int64
        if (rhs < 0)
        {
            if (lhs >= ~(unsigned __int64)(rhs) + 1) // negation is safe, since rhs is 64-bit
            {
                result = (T)(lhs + rhs);
                return;
            }
        }
        else
        {
            // now we know that rhs can be safely cast into an unsigned __int64
            unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

            // special case - rhs cannot be larger than 0x7fffffffffffffff, lhs cannot be larger than 0xffffffff
            // it is not possible for the operation above to overflow, so just check max
            if (tmp <= IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return;
            }
        }
        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastInt64CheckOverflow>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is signed __int64, rhs signed
        __int64 tmp = (__int64)((unsigned __int64)lhs + (unsigned __int64)rhs);

        if (lhs >= 0)
        {
            // mixed sign cannot overflow
            if (rhs >= 0 && tmp < lhs) return false;
        }
        else
        {
            // lhs negative
            if (rhs < 0 && tmp > lhs) return false;
        }

        result = (T)tmp;
        return true;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is signed __int64, rhs signed
        __int64 tmp = (__int64)((unsigned __int64)lhs + (unsigned __int64)rhs);

        if (lhs >= 0)
        {
            // mixed sign cannot overflow
            if (rhs >= 0 && tmp < lhs) E::SafeIntOnOverflow();
        }
        else
        {
            // lhs negative
            if (rhs < 0 && tmp > lhs) E::SafeIntOnOverflow();
        }

        result = (T)tmp;
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastInt64CheckOverflowSafeIntMinMax>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // rhs is signed __int64, lhs signed
        __int64 tmp;

        if (AdditionHelper<__int64, __int64, AdditionState_CastInt64CheckOverflow>::Addition(
                (__int64)lhs, (__int64)rhs, tmp) &&
            tmp <= IntTraits<T>::maxInt && tmp >= IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // rhs is signed __int64, lhs signed
        __int64 tmp;

        AdditionHelper<__int64, __int64, AdditionState_CastInt64CheckOverflow>::AdditionThrow<E>(
            (__int64)lhs, (__int64)rhs, tmp);

        if (tmp <= IntTraits<T>::maxInt && tmp >= IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_CastInt64CheckOverflowMax>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is signed __int64, rhs unsigned < 64-bit
        unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

        if ((__int64)tmp >= lhs)
        {
            result = (T)(__int64)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is signed __int64, rhs unsigned < 64-bit
        // Some compilers get optimization-happy, let's thwart them

        unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)rhs;

        if ((__int64)tmp >= lhs)
        {
            result = (T)(__int64)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_ManualCheckInt64Uint64>
{
public:
    static bool Addition(const __int64& lhs, const unsigned __int64& rhs, __int64& result) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isInt64 && IntTraits<U>::isUint64);
        // rhs is unsigned __int64, lhs __int64
        // cast everything to unsigned, perform addition, then
        // cast back for check - this is done to stop optimizers from removing the code
        unsigned __int64 tmp = (unsigned __int64)lhs + rhs;

        if ((__int64)tmp >= lhs)
        {
            result = (__int64)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const __int64& lhs, const unsigned __int64& rhs, T& result) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isInt64 && IntTraits<U>::isUint64);
        // rhs is unsigned __int64, lhs __int64
        unsigned __int64 tmp = (unsigned __int64)lhs + rhs;

        if ((__int64)tmp >= lhs)
        {
            result = (__int64)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class AdditionHelper<T, U, AdditionState_ManualCheck>
{
public:
    static bool Addition(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // rhs is unsigned __int64, lhs signed, 32-bit or less
        if ((unsigned __int32)(rhs >> 32) == 0)
        {
            // Now it just happens to work out that the standard behavior does what we want
            // Adding explicit casts to show exactly what's happening here
            // Note - this is tweaked to keep optimizers from tossing out the code.
            unsigned __int32 tmp = (unsigned __int32)rhs + (unsigned __int32)lhs;

            if ((__int32)tmp >= lhs &&
                SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::Cast((__int32)tmp, result))
                return true;
        }

        return false;
    }

    template<typename E>
    static void AdditionThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // rhs is unsigned __int64, lhs signed, 32-bit or less

        if ((unsigned __int32)(rhs >> 32) == 0)
        {
            // Now it just happens to work out that the standard behavior does what we want
            // Adding explicit casts to show exactly what's happening here
            unsigned __int32 tmp = (unsigned __int32)rhs + (unsigned __int32)lhs;

            if ((__int32)tmp >= lhs)
            {
                SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::template CastThrow<E>((__int32)tmp,
                                                                                                     result);
                return;
            }
        }
        E::SafeIntOnOverflow();
    }
};

enum SubtractionState
{
    SubtractionState_BothUnsigned,
    SubtractionState_CastIntCheckSafeIntMinMax,
    SubtractionState_CastIntCheckMin,
    SubtractionState_CastInt64CheckSafeIntMinMax,
    SubtractionState_CastInt64CheckMin,
    SubtractionState_Uint64Int,
    SubtractionState_UintInt64,
    SubtractionState_Int64Int,
    SubtractionState_IntInt64,
    SubtractionState_Int64Uint,
    SubtractionState_IntUint64,
    SubtractionState_Int64Uint64,
    // states for SubtractionMethod2
    SubtractionState_BothUnsigned2,
    SubtractionState_CastIntCheckSafeIntMinMax2,
    SubtractionState_CastInt64CheckSafeIntMinMax2,
    SubtractionState_Uint64Int2,
    SubtractionState_UintInt642,
    SubtractionState_Int64Int2,
    SubtractionState_IntInt642,
    SubtractionState_Int64Uint2,
    SubtractionState_IntUint642,
    SubtractionState_Int64Uint642,
    SubtractionState_Error
};

template<typename T, typename U>
class SubtractionMethod
{
public:
    enum
    {
        // unsigned-unsigned
        method =
            ((IntRegion<T, U>::IntZone_UintLT32_UintLT32 || (IntRegion<T, U>::IntZone_Uint32_UintLT64) ||
              (IntRegion<T, U>::IntZone_UintLT32_Uint32) || (IntRegion<T, U>::IntZone_Uint64_Uint) ||
              (IntRegion<T, U>::IntZone_UintLT64_Uint64))
                 ? SubtractionState_BothUnsigned
                 :
                 // unsigned-signed
                 (IntRegion<T, U>::IntZone_UintLT32_IntLT32)
                     ? SubtractionState_CastIntCheckSafeIntMinMax
                     : (IntRegion<T, U>::IntZone_Uint32_IntLT64 || IntRegion<T, U>::IntZone_UintLT32_Int32)
                           ? SubtractionState_CastInt64CheckSafeIntMinMax
                           : (IntRegion<T, U>::IntZone_Uint64_Int || IntRegion<T, U>::IntZone_Uint64_Int64)
                                 ? SubtractionState_Uint64Int
                                 : (IntRegion<T, U>::IntZone_UintLT64_Int64) ? SubtractionState_UintInt64 :
                                                                             // signed-signed
                                       (IntRegion<T, U>::IntZone_IntLT32_IntLT32)
                                           ? SubtractionState_CastIntCheckSafeIntMinMax
                                           : (IntRegion<T, U>::IntZone_Int32_IntLT64 ||
                                              IntRegion<T, U>::IntZone_IntLT32_Int32)
                                                 ? SubtractionState_CastInt64CheckSafeIntMinMax
                                                 : (IntRegion<T, U>::IntZone_Int64_Int ||
                                                    IntRegion<T, U>::IntZone_Int64_Int64)
                                                       ? SubtractionState_Int64Int
                                                       : (IntRegion<T, U>::IntZone_IntLT64_Int64)
                                                             ? SubtractionState_IntInt64
                                                             :
                                                             // signed-unsigned
                                                             (IntRegion<T, U>::IntZone_IntLT32_UintLT32)
                                                                 ? SubtractionState_CastIntCheckMin
                                                                 : (IntRegion<T, U>::IntZone_Int32_UintLT32 ||
                                                                    IntRegion<T, U>::IntZone_IntLT64_Uint32)
                                                                       ? SubtractionState_CastInt64CheckMin
                                                                       : (IntRegion<T, U>::IntZone_Int64_UintLT64)
                                                                             ? SubtractionState_Int64Uint
                                                                             : (IntRegion<T, U>::IntZone_Int_Uint64)
                                                                                   ? SubtractionState_IntUint64
                                                                                   : (IntRegion<T, U>::
                                                                                          IntZone_Int64_Uint64)
                                                                                         ? SubtractionState_Int64Uint64
                                                                                         : SubtractionState_Error)
    };
};

// this is for the case of U - SafeInt< T, E >
template<typename T, typename U>
class SubtractionMethod2
{
public:
    enum
    {
        // unsigned-unsigned
        method =
            ((IntRegion<T, U>::IntZone_UintLT32_UintLT32 || (IntRegion<T, U>::IntZone_Uint32_UintLT64) ||
              (IntRegion<T, U>::IntZone_UintLT32_Uint32) || (IntRegion<T, U>::IntZone_Uint64_Uint) ||
              (IntRegion<T, U>::IntZone_UintLT64_Uint64))
                 ? SubtractionState_BothUnsigned2
                 :
                 // unsigned-signed
                 (IntRegion<T, U>::IntZone_UintLT32_IntLT32)
                     ? SubtractionState_CastIntCheckSafeIntMinMax2
                     : (IntRegion<T, U>::IntZone_Uint32_IntLT64 || IntRegion<T, U>::IntZone_UintLT32_Int32)
                           ? SubtractionState_CastInt64CheckSafeIntMinMax2
                           : (IntRegion<T, U>::IntZone_Uint64_Int || IntRegion<T, U>::IntZone_Uint64_Int64)
                                 ? SubtractionState_Uint64Int2
                                 : (IntRegion<T, U>::IntZone_UintLT64_Int64) ? SubtractionState_UintInt642 :
                                                                             // signed-signed
                                       (IntRegion<T, U>::IntZone_IntLT32_IntLT32)
                                           ? SubtractionState_CastIntCheckSafeIntMinMax2
                                           : (IntRegion<T, U>::IntZone_Int32_IntLT64 ||
                                              IntRegion<T, U>::IntZone_IntLT32_Int32)
                                                 ? SubtractionState_CastInt64CheckSafeIntMinMax2
                                                 : (IntRegion<T, U>::IntZone_Int64_Int ||
                                                    IntRegion<T, U>::IntZone_Int64_Int64)
                                                       ? SubtractionState_Int64Int2
                                                       : (IntRegion<T, U>::IntZone_IntLT64_Int64)
                                                             ? SubtractionState_IntInt642
                                                             :
                                                             // signed-unsigned
                                                             (IntRegion<T, U>::IntZone_IntLT32_UintLT32)
                                                                 ? SubtractionState_CastIntCheckSafeIntMinMax2
                                                                 : (IntRegion<T, U>::IntZone_Int32_UintLT32 ||
                                                                    IntRegion<T, U>::IntZone_IntLT64_Uint32)
                                                                       ? SubtractionState_CastInt64CheckSafeIntMinMax2
                                                                       : (IntRegion<T, U>::IntZone_Int64_UintLT64)
                                                                             ? SubtractionState_Int64Uint2
                                                                             : (IntRegion<T, U>::IntZone_Int_Uint64)
                                                                                   ? SubtractionState_IntUint642
                                                                                   : (IntRegion<T, U>::
                                                                                          IntZone_Int64_Uint64)
                                                                                         ? SubtractionState_Int64Uint642
                                                                                         : SubtractionState_Error)
    };
};

template<typename T, typename U, int method>
class SubtractionHelper;

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_BothUnsigned>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // both are unsigned - easy case
        if (rhs <= lhs)
        {
            result = (T)(lhs - rhs);
            return true;
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // both are unsigned - easy case
        if (rhs <= lhs)
        {
            result = (T)(lhs - rhs);
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_BothUnsigned2>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, U& result) SAFEINT_NOTHROW
    {
        // both are unsigned - easy case
        // Except we do have to check for overflow - lhs could be larger than result can hold
        if (rhs <= lhs)
        {
            T tmp = (T)(lhs - rhs);
            return SafeCastHelper<U, T, GetCastMethod<U, T>::method>::Cast(tmp, result);
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, U& result) SAFEINT_CPP_THROW
    {
        // both are unsigned - easy case
        if (rhs <= lhs)
        {
            T tmp = (T)(lhs - rhs);
            SafeCastHelper<U, T, GetCastMethod<U, T>::method>::template CastThrow<E>(tmp, result);
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_CastIntCheckSafeIntMinMax>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // both values are 16-bit or less
        // rhs is signed, so could end up increasing or decreasing
        __int32 tmp = lhs - rhs;

        if (SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::Cast(tmp, result))
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // both values are 16-bit or less
        // rhs is signed, so could end up increasing or decreasing
        __int32 tmp = lhs - rhs;

        SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::template CastThrow<E>(tmp, result);
    }
};

template<typename U, typename T>
class SubtractionHelper<U, T, SubtractionState_CastIntCheckSafeIntMinMax2>
{
public:
    static bool Subtract(const U& lhs, const T& rhs, T& result) SAFEINT_NOTHROW
    {
        // both values are 16-bit or less
        // rhs is signed, so could end up increasing or decreasing
        __int32 tmp = lhs - rhs;

        return SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::Cast(tmp, result);
    }

    template<typename E>
    static void SubtractThrow(const U& lhs, const T& rhs, T& result) SAFEINT_CPP_THROW
    {
        // both values are 16-bit or less
        // rhs is signed, so could end up increasing or decreasing
        __int32 tmp = lhs - rhs;

        SafeCastHelper<T, __int32, GetCastMethod<T, __int32>::method>::template CastThrow<E>(tmp, result);
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_CastIntCheckMin>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // both values are 16-bit or less
        // rhs is unsigned - check only minimum
        __int32 tmp = lhs - rhs;

        if (tmp >= (__int32)IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // both values are 16-bit or less
        // rhs is unsigned - check only minimum
        __int32 tmp = lhs - rhs;

        if (tmp >= (__int32)IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_CastInt64CheckSafeIntMinMax>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // both values are 32-bit or less
        // rhs is signed, so could end up increasing or decreasing
        __int64 tmp = (__int64)lhs - (__int64)rhs;

        return SafeCastHelper<T, __int64, GetCastMethod<T, __int64>::method>::Cast(tmp, result);
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // both values are 32-bit or less
        // rhs is signed, so could end up increasing or decreasing
        __int64 tmp = (__int64)lhs - (__int64)rhs;

        SafeCastHelper<T, __int64, GetCastMethod<T, __int64>::method>::template CastThrow<E>(tmp, result);
    }
};

template<typename U, typename T>
class SubtractionHelper<U, T, SubtractionState_CastInt64CheckSafeIntMinMax2>
{
public:
    static bool Subtract(const U& lhs, const T& rhs, T& result) SAFEINT_NOTHROW
    {
        // both values are 32-bit or less
        // rhs is signed, so could end up increasing or decreasing
        __int64 tmp = (__int64)lhs - (__int64)rhs;

        return SafeCastHelper<T, __int64, GetCastMethod<T, __int64>::method>::Cast(tmp, result);
    }

    template<typename E>
    static void SubtractThrow(const U& lhs, const T& rhs, T& result) SAFEINT_CPP_THROW
    {
        // both values are 32-bit or less
        // rhs is signed, so could end up increasing or decreasing
        __int64 tmp = (__int64)lhs - (__int64)rhs;

        SafeCastHelper<T, __int64, GetCastMethod<T, __int64>::method>::template CastThrow<E>(tmp, result);
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_CastInt64CheckMin>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // both values are 32-bit or less
        // rhs is unsigned - check only minimum
        __int64 tmp = (__int64)lhs - (__int64)rhs;

        if (tmp >= (__int64)IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // both values are 32-bit or less
        // rhs is unsigned - check only minimum
        __int64 tmp = (__int64)lhs - (__int64)rhs;

        if (tmp >= (__int64)IntTraits<T>::minInt)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_Uint64Int>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is an unsigned __int64, rhs signed
        // must first see if rhs is positive or negative
        if (rhs >= 0)
        {
            if ((unsigned __int64)rhs <= lhs)
            {
                result = (T)(lhs - (unsigned __int64)rhs);
                return true;
            }
        }
        else
        {
            T tmp = lhs;
            // we're now effectively adding
            result = lhs + AbsValueHelper<U, GetAbsMethod<U>::method>::Abs(rhs);

            if (result >= tmp) return true;
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is an unsigned __int64, rhs signed
        // must first see if rhs is positive or negative
        if (rhs >= 0)
        {
            if ((unsigned __int64)rhs <= lhs)
            {
                result = (T)(lhs - (unsigned __int64)rhs);
                return;
            }
        }
        else
        {
            T tmp = lhs;
            // we're now effectively adding
            result = lhs + AbsValueHelper<U, GetAbsMethod<U>::method>::Abs(rhs);

            if (result >= tmp) return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename U, typename T>
class SubtractionHelper<U, T, SubtractionState_Uint64Int2>
{
public:
    static bool Subtract(const U& lhs, const T& rhs, T& result) SAFEINT_NOTHROW
    {
        // U is unsigned __int64, T is signed
        if (rhs < 0)
        {
            // treat this as addition
            unsigned __int64 tmp;

            tmp = lhs + (unsigned __int64)AbsValueHelper<T, GetAbsMethod<T>::method>::Abs(rhs);

            // must check for addition overflow and max
            if (tmp >= lhs && tmp <= IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return true;
            }
        }
        else if ((unsigned __int64)rhs > lhs) // now both are positive, so comparison always works
        {
            // result is negative
            // implies that lhs must fit into T, and result cannot overflow
            // Also allows us to drop to 32-bit math, which is faster on a 32-bit system
            result = (T)lhs - (T)rhs;
            return true;
        }
        else
        {
            // result is positive
            unsigned __int64 tmp = (unsigned __int64)lhs - (unsigned __int64)rhs;

            if (tmp <= IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return true;
            }
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const U& lhs, const T& rhs, T& result) SAFEINT_CPP_THROW
    {
        // U is unsigned __int64, T is signed
        if (rhs < 0)
        {
            // treat this as addition
            unsigned __int64 tmp;

            tmp = lhs + (unsigned __int64)AbsValueHelper<T, GetAbsMethod<T>::method>::Abs(rhs);

            // must check for addition overflow and max
            if (tmp >= lhs && tmp <= IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return;
            }
        }
        else if ((unsigned __int64)rhs > lhs) // now both are positive, so comparison always works
        {
            // result is negative
            // implies that lhs must fit into T, and result cannot overflow
            // Also allows us to drop to 32-bit math, which is faster on a 32-bit system
            result = (T)lhs - (T)rhs;
            return;
        }
        else
        {
            // result is positive
            unsigned __int64 tmp = (unsigned __int64)lhs - (unsigned __int64)rhs;

            if (tmp <= IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return;
            }
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_UintInt64>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is an unsigned int32 or smaller, rhs signed __int64
        // must first see if rhs is positive or negative
        if (rhs >= 0)
        {
            if ((unsigned __int64)rhs <= lhs)
            {
                result = (T)(lhs - (T)rhs);
                return true;
            }
        }
        else
        {
            // we're now effectively adding
            // since lhs is 32-bit, and rhs cannot exceed 2^63
            // this addition cannot overflow
            unsigned __int64 tmp = lhs + ~(unsigned __int64)(rhs) + 1; // negation safe

            // but we could exceed MaxInt
            if (tmp <= IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return true;
            }
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is an unsigned int32 or smaller, rhs signed __int64
        // must first see if rhs is positive or negative
        if (rhs >= 0)
        {
            if ((unsigned __int64)rhs <= lhs)
            {
                result = (T)(lhs - (T)rhs);
                return;
            }
        }
        else
        {
            // we're now effectively adding
            // since lhs is 32-bit, and rhs cannot exceed 2^63
            // this addition cannot overflow
            unsigned __int64 tmp = lhs + ~(unsigned __int64)(rhs) + 1; // negation safe

            // but we could exceed MaxInt
            if (tmp <= IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return;
            }
        }

        E::SafeIntOnOverflow();
    }
};

template<typename U, typename T>
class SubtractionHelper<U, T, SubtractionState_UintInt642>
{
public:
    static bool Subtract(const U& lhs, const T& rhs, T& result) SAFEINT_NOTHROW
    {
        // U unsigned 32-bit or less, T __int64
        if (rhs >= 0)
        {
            // overflow not possible
            result = (T)((__int64)lhs - rhs);
            return true;
        }
        else
        {
            // we effectively have an addition
            // which cannot overflow internally
            unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)(-rhs);

            if (tmp <= (unsigned __int64)IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return true;
            }
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const U& lhs, const T& rhs, T& result) SAFEINT_CPP_THROW
    {
        // U unsigned 32-bit or less, T __int64
        if (rhs >= 0)
        {
            // overflow not possible
            result = (T)((__int64)lhs - rhs);
            return;
        }
        else
        {
            // we effectively have an addition
            // which cannot overflow internally
            unsigned __int64 tmp = (unsigned __int64)lhs + (unsigned __int64)(-rhs);

            if (tmp <= (unsigned __int64)IntTraits<T>::maxInt)
            {
                result = (T)tmp;
                return;
            }
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_Int64Int>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is an __int64, rhs signed (up to 64-bit)
        // we have essentially 4 cases:
        //
        // 1) lhs positive, rhs positive - overflow not possible
        // 2) lhs positive, rhs negative - equivalent to addition - result >= lhs or error
        // 3) lhs negative, rhs positive - check result <= lhs
        // 4) lhs negative, rhs negative - overflow not possible

        __int64 tmp = (__int64)((unsigned __int64)lhs - (unsigned __int64)rhs);

        // Note - ideally, we can order these so that true conditionals
        // lead to success, which enables better pipelining
        // It isn't practical here
        if ((lhs >= 0 && rhs < 0 && tmp < lhs) || // condition 2
            (rhs >= 0 && tmp > lhs))              // condition 3
        {
            return false;
        }

        result = (T)tmp;
        return true;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is an __int64, rhs signed (up to 64-bit)
        // we have essentially 4 cases:
        //
        // 1) lhs positive, rhs positive - overflow not possible
        // 2) lhs positive, rhs negative - equivalent to addition - result >= lhs or error
        // 3) lhs negative, rhs positive - check result <= lhs
        // 4) lhs negative, rhs negative - overflow not possible

        __int64 tmp = (__int64)((unsigned __int64)lhs - (unsigned __int64)rhs);

        // Note - ideally, we can order these so that true conditionals
        // lead to success, which enables better pipelining
        // It isn't practical here
        if ((lhs >= 0 && rhs < 0 && tmp < lhs) || // condition 2
            (rhs >= 0 && tmp > lhs))              // condition 3
        {
            E::SafeIntOnOverflow();
        }

        result = (T)tmp;
    }
};

template<typename U, typename T>
class SubtractionHelper<U, T, SubtractionState_Int64Int2>
{
public:
    static bool Subtract(const U& lhs, const T& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs __int64, rhs any signed int (including __int64)
        __int64 tmp = lhs - rhs;

        // we have essentially 4 cases:
        //
        // 1) lhs positive, rhs positive - overflow not possible in tmp
        // 2) lhs positive, rhs negative - equivalent to addition - result >= lhs or error
        // 3) lhs negative, rhs positive - check result <= lhs
        // 4) lhs negative, rhs negative - overflow not possible in tmp

        if (lhs >= 0)
        {
            // if both positive, overflow to negative not possible
            // which is why we'll explicitly check maxInt, and not call SafeCast
            if ((IntTraits<T>::isLT64Bit && tmp > IntTraits<T>::maxInt) || (rhs < 0 && tmp < lhs))
            {
                return false;
            }
        }
        else
        {
            // lhs negative
            if ((IntTraits<T>::isLT64Bit && tmp < IntTraits<T>::minInt) || (rhs >= 0 && tmp > lhs))
            {
                return false;
            }
        }

        result = (T)tmp;
        return true;
    }

    template<typename E>
    static void SubtractThrow(const U& lhs, const T& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs __int64, rhs any signed int (including __int64)
        __int64 tmp = lhs - rhs;

        // we have essentially 4 cases:
        //
        // 1) lhs positive, rhs positive - overflow not possible in tmp
        // 2) lhs positive, rhs negative - equivalent to addition - result >= lhs or error
        // 3) lhs negative, rhs positive - check result <= lhs
        // 4) lhs negative, rhs negative - overflow not possible in tmp

        if (lhs >= 0)
        {
            // if both positive, overflow to negative not possible
            // which is why we'll explicitly check maxInt, and not call SafeCast
            if ((CompileConst<IntTraits<T>::isLT64Bit>::Value() && tmp > IntTraits<T>::maxInt) ||
                (rhs < 0 && tmp < lhs))
            {
                E::SafeIntOnOverflow();
            }
        }
        else
        {
            // lhs negative
            if ((CompileConst<IntTraits<T>::isLT64Bit>::Value() && tmp < IntTraits<T>::minInt) ||
                (rhs >= 0 && tmp > lhs))
            {
                E::SafeIntOnOverflow();
            }
        }

        result = (T)tmp;
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_IntInt64>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is a 32-bit int or less, rhs __int64
        // we have essentially 4 cases:
        //
        // lhs positive, rhs positive - rhs could be larger than lhs can represent
        // lhs positive, rhs negative - additive case - check tmp >= lhs and tmp > max int
        // lhs negative, rhs positive - check tmp <= lhs and tmp < min int
        // lhs negative, rhs negative - addition cannot internally overflow, check against max

        __int64 tmp = (__int64)((unsigned __int64)lhs - (unsigned __int64)rhs);

        if (lhs >= 0)
        {
            // first case
            if (rhs >= 0)
            {
                if (tmp >= IntTraits<T>::minInt)
                {
                    result = (T)tmp;
                    return true;
                }
            }
            else
            {
                // second case
                if (tmp >= lhs && tmp <= IntTraits<T>::maxInt)
                {
                    result = (T)tmp;
                    return true;
                }
            }
        }
        else
        {
            // lhs < 0
            // third case
            if (rhs >= 0)
            {
                if (tmp <= lhs && tmp >= IntTraits<T>::minInt)
                {
                    result = (T)tmp;
                    return true;
                }
            }
            else
            {
                // fourth case
                if (tmp <= IntTraits<T>::maxInt)
                {
                    result = (T)tmp;
                    return true;
                }
            }
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is a 32-bit int or less, rhs __int64
        // we have essentially 4 cases:
        //
        // lhs positive, rhs positive - rhs could be larger than lhs can represent
        // lhs positive, rhs negative - additive case - check tmp >= lhs and tmp > max int
        // lhs negative, rhs positive - check tmp <= lhs and tmp < min int
        // lhs negative, rhs negative - addition cannot internally overflow, check against max

        __int64 tmp = (__int64)((unsigned __int64)lhs - (unsigned __int64)rhs);

        if (lhs >= 0)
        {
            // first case
            if (rhs >= 0)
            {
                if (tmp >= IntTraits<T>::minInt)
                {
                    result = (T)tmp;
                    return;
                }
            }
            else
            {
                // second case
                if (tmp >= lhs && tmp <= IntTraits<T>::maxInt)
                {
                    result = (T)tmp;
                    return;
                }
            }
        }
        else
        {
            // lhs < 0
            // third case
            if (rhs >= 0)
            {
                if (tmp <= lhs && tmp >= IntTraits<T>::minInt)
                {
                    result = (T)tmp;
                    return;
                }
            }
            else
            {
                // fourth case
                if (tmp <= IntTraits<T>::maxInt)
                {
                    result = (T)tmp;
                    return;
                }
            }
        }

        E::SafeIntOnOverflow();
    }
};

template<typename U, typename T>
class SubtractionHelper<U, T, SubtractionState_IntInt642>
{
public:
    static bool Subtract(const U& lhs, const T& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is any signed int32 or smaller, rhs is int64
        __int64 tmp = (__int64)lhs - rhs;

        if ((lhs >= 0 && rhs < 0 && tmp < lhs) || (rhs > 0 && tmp > lhs))
        {
            return false;
            // else OK
        }

        result = (T)tmp;
        return true;
    }

    template<typename E>
    static void SubtractThrow(const U& lhs, const T& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is any signed int32 or smaller, rhs is int64
        __int64 tmp = (__int64)lhs - rhs;

        if ((lhs >= 0 && rhs < 0 && tmp < lhs) || (rhs > 0 && tmp > lhs))
        {
            E::SafeIntOnOverflow();
            // else OK
        }

        result = (T)tmp;
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_Int64Uint>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is a 64-bit int, rhs unsigned int32 or smaller
        // perform test as unsigned to prevent unwanted optimizations
        unsigned __int64 tmp = (unsigned __int64)lhs - (unsigned __int64)rhs;

        if ((__int64)tmp <= lhs)
        {
            result = (T)(__int64)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is a 64-bit int, rhs unsigned int32 or smaller
        // perform test as unsigned to prevent unwanted optimizations
        unsigned __int64 tmp = (unsigned __int64)lhs - (unsigned __int64)rhs;

        if ((__int64)tmp <= lhs)
        {
            result = (T)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename U, typename T>
class SubtractionHelper<U, T, SubtractionState_Int64Uint2>
{
public:
    // lhs is __int64, rhs is unsigned 32-bit or smaller
    static bool Subtract(const U& lhs, const T& rhs, T& result) SAFEINT_NOTHROW
    {
        // Do this as unsigned to prevent unwanted optimizations
        unsigned __int64 tmp = (unsigned __int64)lhs - (unsigned __int64)rhs;

        if ((__int64)tmp <= IntTraits<T>::maxInt && (__int64)tmp >= IntTraits<T>::minInt)
        {
            result = (T)(__int64)tmp;
            return true;
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const U& lhs, const T& rhs, T& result) SAFEINT_CPP_THROW
    {
        // Do this as unsigned to prevent unwanted optimizations
        unsigned __int64 tmp = (unsigned __int64)lhs - (unsigned __int64)rhs;

        if ((__int64)tmp <= IntTraits<T>::maxInt && (__int64)tmp >= IntTraits<T>::minInt)
        {
            result = (T)(__int64)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_IntUint64>
{
public:
    static bool Subtract(const T& lhs, const U& rhs, T& result) SAFEINT_NOTHROW
    {
        // lhs is any signed int, rhs unsigned int64
        // check against available range

        // We need the absolute value of IntTraits< T >::minInt
        // This will give it to us without extraneous compiler warnings
        const unsigned __int64 AbsMinIntT = (unsigned __int64)IntTraits<T>::maxInt + 1;

        if (lhs < 0)
        {
            if (rhs <= AbsMinIntT - AbsValueHelper<T, GetAbsMethod<T>::method>::Abs(lhs))
            {
                result = (T)(lhs - rhs);
                return true;
            }
        }
        else
        {
            if (rhs <= AbsMinIntT + (unsigned __int64)lhs)
            {
                result = (T)(lhs - rhs);
                return true;
            }
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const T& lhs, const U& rhs, T& result) SAFEINT_CPP_THROW
    {
        // lhs is any signed int, rhs unsigned int64
        // check against available range

        // We need the absolute value of IntTraits< T >::minInt
        // This will give it to us without extraneous compiler warnings
        const unsigned __int64 AbsMinIntT = (unsigned __int64)IntTraits<T>::maxInt + 1;

        if (lhs < 0)
        {
            if (rhs <= AbsMinIntT - AbsValueHelper<T, GetAbsMethod<T>::method>::Abs(lhs))
            {
                result = (T)(lhs - rhs);
                return;
            }
        }
        else
        {
            if (rhs <= AbsMinIntT + (unsigned __int64)lhs)
            {
                result = (T)(lhs - rhs);
                return;
            }
        }

        E::SafeIntOnOverflow();
    }
};

template<typename U, typename T>
class SubtractionHelper<U, T, SubtractionState_IntUint642>
{
public:
    static bool Subtract(const U& lhs, const T& rhs, T& result) SAFEINT_NOTHROW
    {
        // We run into upcasting problems on comparison - needs 2 checks
        if (lhs >= 0 && (T)lhs >= rhs)
        {
            result = (T)((U)lhs - (U)rhs);
            return true;
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const U& lhs, const T& rhs, T& result) SAFEINT_CPP_THROW
    {
        // We run into upcasting problems on comparison - needs 2 checks
        if (lhs >= 0 && (T)lhs >= rhs)
        {
            result = (T)((U)lhs - (U)rhs);
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U>
class SubtractionHelper<T, U, SubtractionState_Int64Uint64>
{
public:
    static bool Subtract(const __int64& lhs, const unsigned __int64& rhs, __int64& result) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isInt64 && IntTraits<U>::isUint64);
        // if we subtract, and it gets larger, there's a problem
        // Perform test as unsigned to prevent unwanted optimizations
        unsigned __int64 tmp = (unsigned __int64)lhs - rhs;

        if ((__int64)tmp <= lhs)
        {
            result = (__int64)tmp;
            return true;
        }
        return false;
    }

    template<typename E>
    static void SubtractThrow(const __int64& lhs, const unsigned __int64& rhs, T& result) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isInt64 && IntTraits<U>::isUint64);
        // if we subtract, and it gets larger, there's a problem
        // Perform test as unsigned to prevent unwanted optimizations
        unsigned __int64 tmp = (unsigned __int64)lhs - rhs;

        if ((__int64)tmp <= lhs)
        {
            result = (__int64)tmp;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename U, typename T>
class SubtractionHelper<U, T, SubtractionState_Int64Uint642>
{
public:
    // If lhs is negative, immediate problem - return must be positive, and subtracting only makes it
    // get smaller. If rhs > lhs, then it would also go negative, which is the other case
    static bool Subtract(const __int64& lhs, const unsigned __int64& rhs, T& result) SAFEINT_NOTHROW
    {
        C_ASSERT(IntTraits<T>::isUint64 && IntTraits<U>::isInt64);
        if (lhs >= 0 && (unsigned __int64)lhs >= rhs)
        {
            result = (unsigned __int64)lhs - rhs;
            return true;
        }

        return false;
    }

    template<typename E>
    static void SubtractThrow(const __int64& lhs, const unsigned __int64& rhs, T& result) SAFEINT_CPP_THROW
    {
        C_ASSERT(IntTraits<T>::isUint64 && IntTraits<U>::isInt64);
        if (lhs >= 0 && (unsigned __int64)lhs >= rhs)
        {
            result = (unsigned __int64)lhs - rhs;
            return;
        }

        E::SafeIntOnOverflow();
    }
};

enum BinaryState
{
    BinaryState_OK,
    BinaryState_Int8,
    BinaryState_Int16,
    BinaryState_Int32
};

template<typename T, typename U>
class BinaryMethod
{
public:
    enum
    {
        // If both operands are unsigned OR
        //    return type is smaller than rhs OR
        //    return type is larger and rhs is unsigned
        // Then binary operations won't produce unexpected results
        method = (sizeof(T) <= sizeof(U) || SafeIntCompare<T, U>::isBothUnsigned || !IntTraits<U>::isSigned)
                     ? BinaryState_OK
                     : IntTraits<U>::isInt8 ? BinaryState_Int8
                                            : IntTraits<U>::isInt16 ? BinaryState_Int16 : BinaryState_Int32
    };
};

#ifdef SAFEINT_DISABLE_BINARY_ASSERT
#define BinaryAssert(x)
#else
#define BinaryAssert(x) SAFEINT_ASSERT(x)
#endif

template<typename T, typename U, int method>
class BinaryAndHelper;

template<typename T, typename U>
class BinaryAndHelper<T, U, BinaryState_OK>
{
public:
    static T And(T lhs, U rhs) SAFEINT_NOTHROW { return (T)(lhs & rhs); }
};

template<typename T, typename U>
class BinaryAndHelper<T, U, BinaryState_Int8>
{
public:
    static T And(T lhs, U rhs) SAFEINT_NOTHROW
    {
        // cast forces sign extension to be zeros
        BinaryAssert((lhs & rhs) == (lhs & (unsigned __int8)rhs));
        return (T)(lhs & (unsigned __int8)rhs);
    }
};

template<typename T, typename U>
class BinaryAndHelper<T, U, BinaryState_Int16>
{
public:
    static T And(T lhs, U rhs) SAFEINT_NOTHROW
    {
        // cast forces sign extension to be zeros
        BinaryAssert((lhs & rhs) == (lhs & (unsigned __int16)rhs));
        return (T)(lhs & (unsigned __int16)rhs);
    }
};

template<typename T, typename U>
class BinaryAndHelper<T, U, BinaryState_Int32>
{
public:
    static T And(T lhs, U rhs) SAFEINT_NOTHROW
    {
        // cast forces sign extension to be zeros
        BinaryAssert((lhs & rhs) == (lhs & (unsigned __int32)rhs));
        return (T)(lhs & (unsigned __int32)rhs);
    }
};

template<typename T, typename U, int method>
class BinaryOrHelper;

template<typename T, typename U>
class BinaryOrHelper<T, U, BinaryState_OK>
{
public:
    static T Or(T lhs, U rhs) SAFEINT_NOTHROW { return (T)(lhs | rhs); }
};

template<typename T, typename U>
class BinaryOrHelper<T, U, BinaryState_Int8>
{
public:
    static T Or(T lhs, U rhs) SAFEINT_NOTHROW
    {
        // cast forces sign extension to be zeros
        BinaryAssert((lhs | rhs) == (lhs | (unsigned __int8)rhs));
        return (T)(lhs | (unsigned __int8)rhs);
    }
};

template<typename T, typename U>
class BinaryOrHelper<T, U, BinaryState_Int16>
{
public:
    static T Or(T lhs, U rhs) SAFEINT_NOTHROW
    {
        // cast forces sign extension to be zeros
        BinaryAssert((lhs | rhs) == (lhs | (unsigned __int16)rhs));
        return (T)(lhs | (unsigned __int16)rhs);
    }
};

template<typename T, typename U>
class BinaryOrHelper<T, U, BinaryState_Int32>
{
public:
    static T Or(T lhs, U rhs) SAFEINT_NOTHROW
    {
        // cast forces sign extension to be zeros
        BinaryAssert((lhs | rhs) == (lhs | (unsigned __int32)rhs));
        return (T)(lhs | (unsigned __int32)rhs);
    }
};

template<typename T, typename U, int method>
class BinaryXorHelper;

template<typename T, typename U>
class BinaryXorHelper<T, U, BinaryState_OK>
{
public:
    static T Xor(T lhs, U rhs) SAFEINT_NOTHROW { return (T)(lhs ^ rhs); }
};

template<typename T, typename U>
class BinaryXorHelper<T, U, BinaryState_Int8>
{
public:
    static T Xor(T lhs, U rhs) SAFEINT_NOTHROW
    {
        // cast forces sign extension to be zeros
        BinaryAssert((lhs ^ rhs) == (lhs ^ (unsigned __int8)rhs));
        return (T)(lhs ^ (unsigned __int8)rhs);
    }
};

template<typename T, typename U>
class BinaryXorHelper<T, U, BinaryState_Int16>
{
public:
    static T Xor(T lhs, U rhs) SAFEINT_NOTHROW
    {
        // cast forces sign extension to be zeros
        BinaryAssert((lhs ^ rhs) == (lhs ^ (unsigned __int16)rhs));
        return (T)(lhs ^ (unsigned __int16)rhs);
    }
};

template<typename T, typename U>
class BinaryXorHelper<T, U, BinaryState_Int32>
{
public:
    static T Xor(T lhs, U rhs) SAFEINT_NOTHROW
    {
        // cast forces sign extension to be zeros
        BinaryAssert((lhs ^ rhs) == (lhs ^ (unsigned __int32)rhs));
        return (T)(lhs ^ (unsigned __int32)rhs);
    }
};

/*****************  External functions ****************************************/

// External functions that can be used where you only need to check one operation
// non-class helper function so that you can check for a cast's validity
// and handle errors how you like
template<typename T, typename U>
inline bool SafeCast(const T From, U& To) SAFEINT_NOTHROW
{
    return SafeCastHelper<U, T, GetCastMethod<U, T>::method>::Cast(From, To);
}

template<typename T, typename U>
inline bool SafeEquals(const T t, const U u) SAFEINT_NOTHROW
{
    return EqualityTest<T, U, ValidComparison<T, U>::method>::IsEquals(t, u);
}

template<typename T, typename U>
inline bool SafeNotEquals(const T t, const U u) SAFEINT_NOTHROW
{
    return !EqualityTest<T, U, ValidComparison<T, U>::method>::IsEquals(t, u);
}

template<typename T, typename U>
inline bool SafeGreaterThan(const T t, const U u) SAFEINT_NOTHROW
{
    return GreaterThanTest<T, U, ValidComparison<T, U>::method>::GreaterThan(t, u);
}

template<typename T, typename U>
inline bool SafeGreaterThanEquals(const T t, const U u) SAFEINT_NOTHROW
{
    return !GreaterThanTest<U, T, ValidComparison<U, T>::method>::GreaterThan(u, t);
}

template<typename T, typename U>
inline bool SafeLessThan(const T t, const U u) SAFEINT_NOTHROW
{
    return GreaterThanTest<U, T, ValidComparison<U, T>::method>::GreaterThan(u, t);
}

template<typename T, typename U>
inline bool SafeLessThanEquals(const T t, const U u) SAFEINT_NOTHROW
{
    return !GreaterThanTest<T, U, ValidComparison<T, U>::method>::GreaterThan(t, u);
}

template<typename T, typename U>
inline bool SafeModulus(const T& t, const U& u, T& result) SAFEINT_NOTHROW
{
    return (ModulusHelper<T, U, ValidComparison<T, U>::method>::Modulus(t, u, result) == SafeIntNoError);
}

template<typename T, typename U>
inline bool SafeMultiply(T t, U u, T& result) SAFEINT_NOTHROW
{
    return MultiplicationHelper<T, U, MultiplicationMethod<T, U>::method>::Multiply(t, u, result);
}

template<typename T, typename U>
inline bool SafeDivide(T t, U u, T& result) SAFEINT_NOTHROW
{
    return (DivisionHelper<T, U, DivisionMethod<T, U>::method>::Divide(t, u, result) == SafeIntNoError);
}

template<typename T, typename U>
inline bool SafeAdd(T t, U u, T& result) SAFEINT_NOTHROW
{
    return AdditionHelper<T, U, AdditionMethod<T, U>::method>::Addition(t, u, result);
}

template<typename T, typename U>
inline bool SafeSubtract(T t, U u, T& result) SAFEINT_NOTHROW
{
    return SubtractionHelper<T, U, SubtractionMethod<T, U>::method>::Subtract(t, u, result);
}

/*****************  end external functions ************************************/

// Main SafeInt class
// Assumes exceptions can be thrown
template<typename T, typename E = SafeIntDefaultExceptionHandler>
class SafeInt
{
public:
    SafeInt() SAFEINT_NOTHROW
    {
        C_ASSERT(NumericType<T>::isInt);
        m_int = 0;
    }

    // Having a constructor for every type of int
    // avoids having the compiler evade our checks when doing implicit casts -
    // e.g., SafeInt<char> s = 0x7fffffff;
    SafeInt(const T& i) SAFEINT_NOTHROW
    {
        C_ASSERT(NumericType<T>::isInt);
        // always safe
        m_int = i;
    }

    // provide explicit boolean converter
    SafeInt(bool b) SAFEINT_NOTHROW
    {
        C_ASSERT(NumericType<T>::isInt);
        m_int = (T)(b ? 1 : 0);
    }

    template<typename U>
    SafeInt(const SafeInt<U, E>& u) SAFEINT_CPP_THROW
    {
        C_ASSERT(NumericType<T>::isInt);
        *this = SafeInt<T, E>((U)u);
    }

    template<typename U>
    SafeInt(const U& i) SAFEINT_CPP_THROW
    {
        C_ASSERT(NumericType<T>::isInt);
        // SafeCast will throw exceptions if i won't fit in type T
        SafeCastHelper<T, U, GetCastMethod<T, U>::method>::template CastThrow<E>(i, m_int);
    }

    // The destructor is intentionally commented out - no destructor
    // vs. a do-nothing destructor makes a huge difference in
    // inlining characteristics. It wasn't doing anything anyway.
    // ~SafeInt(){};

    // now start overloading operators
    // assignment operator
    // constructors exist for all int types and will ensure safety

    template<typename U>
    SafeInt<T, E>& operator=(const U& rhs) SAFEINT_CPP_THROW
    {
        // use constructor to test size
        // constructor is optimized to do minimal checking based
        // on whether T can contain U
        // note - do not change this
        *this = SafeInt<T, E>(rhs);
        return *this;
    }

    SafeInt<T, E>& operator=(const T& rhs) SAFEINT_NOTHROW
    {
        m_int = rhs;
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator=(const SafeInt<U, E>& rhs) SAFEINT_CPP_THROW
    {
        SafeCastHelper<T, U, GetCastMethod<T, U>::method>::template CastThrow<E>(rhs.Ref(), m_int);
        return *this;
    }

    SafeInt<T, E>& operator=(const SafeInt<T, E>& rhs) SAFEINT_NOTHROW
    {
        m_int = rhs.m_int;
        return *this;
    }

    // Casting operators

    operator bool() const SAFEINT_NOTHROW { return !!m_int; }

    operator char() const SAFEINT_CPP_THROW
    {
        char val;
        SafeCastHelper<char, T, GetCastMethod<char, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    operator signed char() const SAFEINT_CPP_THROW
    {
        signed char val;
        SafeCastHelper<signed char, T, GetCastMethod<signed char, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    operator unsigned char() const SAFEINT_CPP_THROW
    {
        unsigned char val;
        SafeCastHelper<unsigned char, T, GetCastMethod<unsigned char, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    operator __int16() const SAFEINT_CPP_THROW
    {
        __int16 val;
        SafeCastHelper<__int16, T, GetCastMethod<__int16, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    operator unsigned __int16() const SAFEINT_CPP_THROW
    {
        unsigned __int16 val;
        SafeCastHelper<unsigned __int16, T, GetCastMethod<unsigned __int16, T>::method>::template CastThrow<E>(m_int,
                                                                                                               val);
        return val;
    }

    operator __int32() const SAFEINT_CPP_THROW
    {
        __int32 val;
        SafeCastHelper<__int32, T, GetCastMethod<__int32, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    operator unsigned __int32() const SAFEINT_CPP_THROW
    {
        unsigned __int32 val;
        SafeCastHelper<unsigned __int32, T, GetCastMethod<unsigned __int32, T>::method>::template CastThrow<E>(m_int,
                                                                                                               val);
        return val;
    }

    // The compiler knows that int == __int32
    // but not that long == __int32
    operator long() const SAFEINT_CPP_THROW
    {
        long val;
        SafeCastHelper<long, T, GetCastMethod<long, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    operator unsigned long() const SAFEINT_CPP_THROW
    {
        unsigned long val;
        SafeCastHelper<unsigned long, T, GetCastMethod<unsigned long, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    operator __int64() const SAFEINT_CPP_THROW
    {
        __int64 val;
        SafeCastHelper<__int64, T, GetCastMethod<__int64, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    operator unsigned __int64() const SAFEINT_CPP_THROW
    {
        unsigned __int64 val;
        SafeCastHelper<unsigned __int64, T, GetCastMethod<unsigned __int64, T>::method>::template CastThrow<E>(m_int,
                                                                                                               val);
        return val;
    }

#if defined SAFEINT_USE_WCHAR_T || defined _NATIVE_WCHAR_T_DEFINED
    operator wchar_t() const SAFEINT_CPP_THROW
    {
        wchar_t val;
        SafeCastHelper<wchar_t, T, GetCastMethod<wchar_t, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }
#endif

#ifdef SIZE_T_CAST_NEEDED
    // We also need an explicit cast to size_t, or the compiler will complain
    // Apparently, only SOME compilers complain, and cl 14.00.50727.42 isn't one of them
    // Leave here in case we decide to backport this to an earlier compiler
    operator size_t() const SAFEINT_CPP_THROW
    {
        size_t val;
        SafeCastHelper<size_t, T, GetCastMethod<size_t, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }
#endif

    // Also provide a cast operator for floating point types
    operator float() const SAFEINT_CPP_THROW
    {
        float val;
        SafeCastHelper<float, T, GetCastMethod<float, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    operator double() const SAFEINT_CPP_THROW
    {
        double val;
        SafeCastHelper<double, T, GetCastMethod<double, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }
    operator long double() const SAFEINT_CPP_THROW
    {
        long double val;
        SafeCastHelper<long double, T, GetCastMethod<long double, T>::method>::template CastThrow<E>(m_int, val);
        return val;
    }

    // If you need a pointer to the data
    // this could be dangerous, but allows you to correctly pass
    // instances of this class to APIs that take a pointer to an integer
    // also see overloaded address-of operator below
    T* Ptr() SAFEINT_NOTHROW { return &m_int; }
    const T* Ptr() const SAFEINT_NOTHROW { return &m_int; }
    const T& Ref() const SAFEINT_NOTHROW { return m_int; }

    // Or if SafeInt< T, E >::Ptr() is inconvenient, use the overload
    // operator &
    // This allows you to do unsafe things!
    // It is meant to allow you to more easily
    // pass a SafeInt into things like ReadFile
    T* operator&() SAFEINT_NOTHROW { return &m_int; }
    const T* operator&() const SAFEINT_NOTHROW { return &m_int; }

    // Unary operators
    bool operator!() const SAFEINT_NOTHROW { return (!m_int) ? true : false; }

    // operator + (unary)
    // note - normally, the '+' and '-' operators will upcast to a signed int
    // for T < 32 bits. This class changes behavior to preserve type
    const SafeInt<T, E>& operator+() const SAFEINT_NOTHROW { return *this; }

    // unary  -

    SafeInt<T, E> operator-() const SAFEINT_CPP_THROW
    {
        // Note - unsigned still performs the bitwise manipulation
        // will warn at level 2 or higher if the value is 32-bit or larger
        return SafeInt<T, E>(NegationHelper<T, IntTraits<T>::isSigned>::template NegativeThrow<E>(m_int));
    }

    // prefix increment operator
    SafeInt<T, E>& operator++() SAFEINT_CPP_THROW
    {
        if (m_int != IntTraits<T>::maxInt)
        {
            ++m_int;
            return *this;
        }
        E::SafeIntOnOverflow();
    }

    // prefix decrement operator
    SafeInt<T, E>& operator--() SAFEINT_CPP_THROW
    {
        if (m_int != IntTraits<T>::minInt)
        {
            --m_int;
            return *this;
        }
        E::SafeIntOnOverflow();
    }

    // note that postfix operators have inherently worse perf
    // characteristics

    // postfix increment operator
    SafeInt<T, E> operator++(int) SAFEINT_CPP_THROW // dummy arg to comply with spec
    {
        if (m_int != IntTraits<T>::maxInt)
        {
            SafeInt<T, E> tmp(m_int);

            m_int++;
            return tmp;
        }
        E::SafeIntOnOverflow();
    }

    // postfix decrement operator
    SafeInt<T, E> operator--(int) SAFEINT_CPP_THROW // dummy arg to comply with spec
    {
        if (m_int != IntTraits<T>::minInt)
        {
            SafeInt<T, E> tmp(m_int);
            m_int--;
            return tmp;
        }
        E::SafeIntOnOverflow();
    }

    // One's complement
    // Note - this operator will normally change size to an int
    // cast in return improves perf and maintains type
    SafeInt<T, E> operator~() const SAFEINT_NOTHROW { return SafeInt<T, E>((T)~m_int); }

    // Binary operators
    //
    // arithmetic binary operators
    // % modulus
    // * multiplication
    // / division
    // + addition
    // - subtraction
    //
    // For each of the arithmetic operators, you will need to
    // use them as follows:
    //
    // SafeInt<char> c = 2;
    // SafeInt<int>  i = 3;
    //
    // SafeInt<int> i2 = i op (char)c;
    // OR
    // SafeInt<char> i2 = (int)i op c;
    //
    // The base problem is that if the lhs and rhs inputs are different SafeInt types
    // it is not possible in this implementation to determine what type of SafeInt
    // should be returned. You have to let the class know which of the two inputs
    // need to be the return type by forcing the other value to the base integer type.
    //
    // Note - as per feedback from Scott Meyers, I'm exploring how to get around this.
    // 3.0 update - I'm still thinking about this. It can be done with template metaprogramming,
    // but it is tricky, and there's a perf vs. correctness tradeoff where the right answer
    // is situational.
    //
    // The case of:
    //
    // SafeInt< T, E > i, j, k;
    // i = j op k;
    //
    // works just fine and no unboxing is needed because the return type is not ambiguous.

    // Modulus
    // Modulus has some convenient properties -
    // first, the magnitude of the return can never be
    // larger than the lhs operand, and it must be the same sign
    // as well. It does, however, suffer from the same promotion
    // problems as comparisons, division and other operations
    template<typename U>
    SafeInt<T, E> operator%(U rhs) const SAFEINT_CPP_THROW
    {
        T result;
        ModulusHelper<T, U, ValidComparison<T, U>::method>::template ModulusThrow<E>(m_int, rhs, result);
        return SafeInt<T, E>(result);
    }

    SafeInt<T, E> operator%(SafeInt<T, E> rhs) const SAFEINT_CPP_THROW
    {
        T result;
        ModulusHelper<T, T, ValidComparison<T, T>::method>::template ModulusThrow<E>(m_int, rhs, result);
        return SafeInt<T, E>(result);
    }

    // Modulus assignment
    template<typename U>
    SafeInt<T, E>& operator%=(U rhs) SAFEINT_CPP_THROW
    {
        ModulusHelper<T, U, ValidComparison<T, U>::method>::template ModulusThrow<E>(m_int, rhs, m_int);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator%=(SafeInt<U, E> rhs) SAFEINT_CPP_THROW
    {
        ModulusHelper<T, U, ValidComparison<T, U>::method>::template ModulusThrow<E>(m_int, (U)rhs, m_int);
        return *this;
    }

    // Multiplication
    template<typename U>
    SafeInt<T, E> operator*(U rhs) const SAFEINT_CPP_THROW
    {
        T ret(0);
        MultiplicationHelper<T, U, MultiplicationMethod<T, U>::method>::template MultiplyThrow<E>(m_int, rhs, ret);
        return SafeInt<T, E>(ret);
    }

    SafeInt<T, E> operator*(SafeInt<T, E> rhs) const SAFEINT_CPP_THROW
    {
        T ret(0);
        MultiplicationHelper<T, T, MultiplicationMethod<T, T>::method>::template MultiplyThrow<E>(m_int, (T)rhs, ret);
        return SafeInt<T, E>(ret);
    }

    // Multiplication assignment
    SafeInt<T, E>& operator*=(SafeInt<T, E> rhs) SAFEINT_CPP_THROW
    {
        MultiplicationHelper<T, T, MultiplicationMethod<T, T>::method>::template MultiplyThrow<E>(m_int, (T)rhs, m_int);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator*=(U rhs) SAFEINT_CPP_THROW
    {
        MultiplicationHelper<T, U, MultiplicationMethod<T, U>::method>::template MultiplyThrow<E>(m_int, rhs, m_int);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator*=(SafeInt<U, E> rhs) SAFEINT_CPP_THROW
    {
        MultiplicationHelper<T, U, MultiplicationMethod<T, U>::method>::template MultiplyThrow<E>(
            m_int, rhs.Ref(), m_int);
        return *this;
    }

    // Division
    template<typename U>
    SafeInt<T, E> operator/(U rhs) const SAFEINT_CPP_THROW
    {
        T ret(0);
        DivisionHelper<T, U, DivisionMethod<T, U>::method>::template DivideThrow<E>(m_int, rhs, ret);
        return SafeInt<T, E>(ret);
    }

    SafeInt<T, E> operator/(SafeInt<T, E> rhs) const SAFEINT_CPP_THROW
    {
        T ret(0);
        DivisionHelper<T, T, DivisionMethod<T, T>::method>::template DivideThrow<E>(m_int, (T)rhs, ret);
        return SafeInt<T, E>(ret);
    }

    // Division assignment
    SafeInt<T, E>& operator/=(SafeInt<T, E> i) SAFEINT_CPP_THROW
    {
        DivisionHelper<T, T, DivisionMethod<T, T>::method>::template DivideThrow<E>(m_int, (T)i, m_int);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator/=(U i) SAFEINT_CPP_THROW
    {
        DivisionHelper<T, U, DivisionMethod<T, U>::method>::template DivideThrow<E>(m_int, i, m_int);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator/=(SafeInt<U, E> i)
    {
        DivisionHelper<T, U, DivisionMethod<T, U>::method>::template DivideThrow<E>(m_int, (U)i, m_int);
        return *this;
    }

    // For addition and subtraction

    // Addition
    SafeInt<T, E> operator+(SafeInt<T, E> rhs) const SAFEINT_CPP_THROW
    {
        T ret(0);
        AdditionHelper<T, T, AdditionMethod<T, T>::method>::template AdditionThrow<E>(m_int, (T)rhs, ret);
        return SafeInt<T, E>(ret);
    }

    template<typename U>
    SafeInt<T, E> operator+(U rhs) const SAFEINT_CPP_THROW
    {
        T ret(0);
        AdditionHelper<T, U, AdditionMethod<T, U>::method>::template AdditionThrow<E>(m_int, rhs, ret);
        return SafeInt<T, E>(ret);
    }

    // addition assignment
    SafeInt<T, E>& operator+=(SafeInt<T, E> rhs) SAFEINT_CPP_THROW
    {
        AdditionHelper<T, T, AdditionMethod<T, T>::method>::template AdditionThrow<E>(m_int, (T)rhs, m_int);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator+=(U rhs) SAFEINT_CPP_THROW
    {
        AdditionHelper<T, U, AdditionMethod<T, U>::method>::template AdditionThrow<E>(m_int, rhs, m_int);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator+=(SafeInt<U, E> rhs) SAFEINT_CPP_THROW
    {
        AdditionHelper<T, U, AdditionMethod<T, U>::method>::template AdditionThrow<E>(m_int, (U)rhs, m_int);
        return *this;
    }

    // Subtraction
    template<typename U>
    SafeInt<T, E> operator-(U rhs) const SAFEINT_CPP_THROW
    {
        T ret(0);
        SubtractionHelper<T, U, SubtractionMethod<T, U>::method>::template SubtractThrow<E>(m_int, rhs, ret);
        return SafeInt<T, E>(ret);
    }

    SafeInt<T, E> operator-(SafeInt<T, E> rhs) const SAFEINT_CPP_THROW
    {
        T ret(0);
        SubtractionHelper<T, T, SubtractionMethod<T, T>::method>::template SubtractThrow<E>(m_int, (T)rhs, ret);
        return SafeInt<T, E>(ret);
    }

    // Subtraction assignment
    SafeInt<T, E>& operator-=(SafeInt<T, E> rhs) SAFEINT_CPP_THROW
    {
        SubtractionHelper<T, T, SubtractionMethod<T, T>::method>::template SubtractThrow<E>(m_int, (T)rhs, m_int);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator-=(U rhs) SAFEINT_CPP_THROW
    {
        SubtractionHelper<T, U, SubtractionMethod<T, U>::method>::template SubtractThrow<E>(m_int, rhs, m_int);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator-=(SafeInt<U, E> rhs) SAFEINT_CPP_THROW
    {
        SubtractionHelper<T, U, SubtractionMethod<T, U>::method>::template SubtractThrow<E>(m_int, (U)rhs, m_int);
        return *this;
    }

    // Shift operators
    // Note - shift operators ALWAYS return the same type as the lhs
    // specific version for SafeInt< T, E > not needed -
    // code path is exactly the same as for SafeInt< U, E > as rhs

    // Left shift
    // Also, shifting > bitcount is undefined - trap in debug
#ifdef SAFEINT_DISABLE_SHIFT_ASSERT
#define ShiftAssert(x)
#else
#define ShiftAssert(x) SAFEINT_ASSERT(x)
#endif

    template<typename U>
    SafeInt<T, E> operator<<(U bits) const SAFEINT_NOTHROW
    {
        ShiftAssert(!IntTraits<U>::isSigned || bits >= 0);
        ShiftAssert(bits < (int)IntTraits<T>::bitCount);

        return SafeInt<T, E>((T)(m_int << bits));
    }

    template<typename U>
    SafeInt<T, E> operator<<(SafeInt<U, E> bits) const SAFEINT_NOTHROW
    {
        ShiftAssert(!IntTraits<U>::isSigned || (U)bits >= 0);
        ShiftAssert((U)bits < (int)IntTraits<T>::bitCount);

        return SafeInt<T, E>((T)(m_int << (U)bits));
    }

    // Left shift assignment

    template<typename U>
    SafeInt<T, E>& operator<<=(U bits) SAFEINT_NOTHROW
    {
        ShiftAssert(!IntTraits<U>::isSigned || bits >= 0);
        ShiftAssert(bits < (int)IntTraits<T>::bitCount);

        m_int <<= bits;
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator<<=(SafeInt<U, E> bits) SAFEINT_NOTHROW
    {
        ShiftAssert(!IntTraits<U>::isSigned || (U)bits >= 0);
        ShiftAssert((U)bits < (int)IntTraits<T>::bitCount);

        m_int <<= (U)bits;
        return *this;
    }

    // Right shift
    template<typename U>
    SafeInt<T, E> operator>>(U bits) const SAFEINT_NOTHROW
    {
        ShiftAssert(!IntTraits<U>::isSigned || bits >= 0);
        ShiftAssert(bits < (int)IntTraits<T>::bitCount);

        return SafeInt<T, E>((T)(m_int >> bits));
    }

    template<typename U>
    SafeInt<T, E> operator>>(SafeInt<U, E> bits) const SAFEINT_NOTHROW
    {
        ShiftAssert(!IntTraits<U>::isSigned || (U)bits >= 0);
        ShiftAssert(bits < (int)IntTraits<T>::bitCount);

        return SafeInt<T, E>((T)(m_int >> (U)bits));
    }

    // Right shift assignment
    template<typename U>
    SafeInt<T, E>& operator>>=(U bits) SAFEINT_NOTHROW
    {
        ShiftAssert(!IntTraits<U>::isSigned || bits >= 0);
        ShiftAssert(bits < (int)IntTraits<T>::bitCount);

        m_int >>= bits;
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator>>=(SafeInt<U, E> bits) SAFEINT_NOTHROW
    {
        ShiftAssert(!IntTraits<U>::isSigned || (U)bits >= 0);
        ShiftAssert((U)bits < (int)IntTraits<T>::bitCount);

        m_int >>= (U)bits;
        return *this;
    }

    // Bitwise operators
    // This only makes sense if we're dealing with the same type and size
    // demand a type T, or something that fits into a type T

    // Bitwise &
    SafeInt<T, E> operator&(SafeInt<T, E> rhs) const SAFEINT_NOTHROW { return SafeInt<T, E>(m_int & (T)rhs); }

    template<typename U>
    SafeInt<T, E> operator&(U rhs) const SAFEINT_NOTHROW
    {
        // we want to avoid setting bits by surprise
        // consider the case of lhs = int, value = 0xffffffff
        //                      rhs = char, value = 0xff
        //
        // programmer intent is to get only the lower 8 bits
        // normal behavior is to upcast both sides to an int
        // which then sign extends rhs, setting all the bits

        // If you land in the assert, this is because the bitwise operator
        // was causing unexpected behavior. Fix is to properly cast your inputs
        // so that it works like you meant, not unexpectedly

        return SafeInt<T, E>(BinaryAndHelper<T, U, BinaryMethod<T, U>::method>::And(m_int, rhs));
    }

    // Bitwise & assignment
    SafeInt<T, E>& operator&=(SafeInt<T, E> rhs) SAFEINT_NOTHROW
    {
        m_int &= (T)rhs;
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator&=(U rhs) SAFEINT_NOTHROW
    {
        m_int = BinaryAndHelper<T, U, BinaryMethod<T, U>::method>::And(m_int, rhs);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator&=(SafeInt<U, E> rhs) SAFEINT_NOTHROW
    {
        m_int = BinaryAndHelper<T, U, BinaryMethod<T, U>::method>::And(m_int, (U)rhs);
        return *this;
    }

    // XOR
    SafeInt<T, E> operator^(SafeInt<T, E> rhs) const SAFEINT_NOTHROW { return SafeInt<T, E>((T)(m_int ^ (T)rhs)); }

    template<typename U>
    SafeInt<T, E> operator^(U rhs) const SAFEINT_NOTHROW
    {
        // If you land in the assert, this is because the bitwise operator
        // was causing unexpected behavior. Fix is to properly cast your inputs
        // so that it works like you meant, not unexpectedly

        return SafeInt<T, E>(BinaryXorHelper<T, U, BinaryMethod<T, U>::method>::Xor(m_int, rhs));
    }

    // XOR assignment
    SafeInt<T, E>& operator^=(SafeInt<T, E> rhs) SAFEINT_NOTHROW
    {
        m_int ^= (T)rhs;
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator^=(U rhs) SAFEINT_NOTHROW
    {
        m_int = BinaryXorHelper<T, U, BinaryMethod<T, U>::method>::Xor(m_int, rhs);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator^=(SafeInt<U, E> rhs) SAFEINT_NOTHROW
    {
        m_int = BinaryXorHelper<T, U, BinaryMethod<T, U>::method>::Xor(m_int, (U)rhs);
        return *this;
    }

    // bitwise OR
    SafeInt<T, E> operator|(SafeInt<T, E> rhs) const SAFEINT_NOTHROW { return SafeInt<T, E>((T)(m_int | (T)rhs)); }

    template<typename U>
    SafeInt<T, E> operator|(U rhs) const SAFEINT_NOTHROW
    {
        return SafeInt<T, E>(BinaryOrHelper<T, U, BinaryMethod<T, U>::method>::Or(m_int, rhs));
    }

    // bitwise OR assignment
    SafeInt<T, E>& operator|=(SafeInt<T, E> rhs) SAFEINT_NOTHROW
    {
        m_int |= (T)rhs;
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator|=(U rhs) SAFEINT_NOTHROW
    {
        m_int = BinaryOrHelper<T, U, BinaryMethod<T, U>::method>::Or(m_int, rhs);
        return *this;
    }

    template<typename U>
    SafeInt<T, E>& operator|=(SafeInt<U, E> rhs) SAFEINT_NOTHROW
    {
        m_int = BinaryOrHelper<T, U, BinaryMethod<T, U>::method>::Or(m_int, (U)rhs);
        return *this;
    }

    // Miscellaneous helper functions
    SafeInt<T, E> Min(SafeInt<T, E> test, const T floor = IntTraits<T>::minInt) const SAFEINT_NOTHROW
    {
        T tmp = test < m_int ? (T)test : m_int;
        return tmp < floor ? floor : tmp;
    }

    SafeInt<T, E> Max(SafeInt<T, E> test, const T upper = IntTraits<T>::maxInt) const SAFEINT_NOTHROW
    {
        T tmp = test > m_int ? (T)test : m_int;
        return tmp > upper ? upper : tmp;
    }

    void Swap(SafeInt<T, E>& with) SAFEINT_NOTHROW
    {
        T temp(m_int);
        m_int = with.m_int;
        with.m_int = temp;
    }

    static SafeInt<T, E> SafeAtoI(const char* input) SAFEINT_CPP_THROW { return SafeTtoI(input); }

    static SafeInt<T, E> SafeWtoI(const wchar_t* input) { return SafeTtoI(input); }

    enum alignBits
    {
        align2 = 1,
        align4 = 2,
        align8 = 3,
        align16 = 4,
        align32 = 5,
        align64 = 6,
        align128 = 7,
        align256 = 8
    };

    template<alignBits bits>
    const SafeInt<T, E>& Align() SAFEINT_CPP_THROW
    {
        // Zero is always aligned
        if (m_int == 0) return *this;

        // We don't support aligning negative numbers at this time
        // Can't align unsigned numbers on bitCount (e.g., 8 bits = 256, unsigned char max = 255)
        // or signed numbers on bitCount-1 (e.g., 7 bits = 128, signed char max = 127).
        // Also makes no sense to try to align on negative or no bits.

        ShiftAssert(((IntTraits<T>::isSigned && bits < (int)IntTraits<T>::bitCount - 1) ||
                     (!IntTraits<T>::isSigned && bits < (int)IntTraits<T>::bitCount)) &&
                    bits >= 0 && (!IntTraits<T>::isSigned || m_int > 0));

        const T AlignValue = ((T)1 << bits) - 1;

        m_int = (T)((m_int + AlignValue) & ~AlignValue);

        if (m_int <= 0) E::SafeIntOnOverflow();

        return *this;
    }

    // Commonly needed alignments:
    const SafeInt<T, E>& Align2() { return Align<align2>(); }
    const SafeInt<T, E>& Align4() { return Align<align4>(); }
    const SafeInt<T, E>& Align8() { return Align<align8>(); }
    const SafeInt<T, E>& Align16() { return Align<align16>(); }
    const SafeInt<T, E>& Align32() { return Align<align32>(); }
    const SafeInt<T, E>& Align64() { return Align<align64>(); }

private:
    // This is almost certainly not the best optimized version of atoi,
    // but it does not display a typical bug where it isn't possible to set MinInt
    // and it won't allow you to overflow your integer.
    // This is here because it is useful, and it is an example of what
    // can be done easily with SafeInt.
    template<typename U>
    static SafeInt<T, E> SafeTtoI(U* input) SAFEINT_CPP_THROW
    {
        U* tmp = input;
        SafeInt<T, E> s;
        bool negative = false;

        // Bad input, or empty string
        if (input == nullptr || input[0] == 0) E::SafeIntOnOverflow();

        switch (*tmp)
        {
            case '-':
                tmp++;
                negative = true;
                break;
            case '+': tmp++; break;
        }

        while (*tmp != 0)
        {
            if (*tmp < '0' || *tmp > '9') break;

            if ((T)s != 0) s *= (T)10;

            if (!negative)
                s += (T)(*tmp - '0');
            else
                s -= (T)(*tmp - '0');

            tmp++;
        }

        return s;
    }

    T m_int;
};

// Helper function used to subtract pointers.
// Used to squelch warnings
template<typename P>
SafeInt<ptrdiff_t, SafeIntDefaultExceptionHandler> SafePtrDiff(const P* p1, const P* p2) SAFEINT_CPP_THROW
{
    return SafeInt<ptrdiff_t, SafeIntDefaultExceptionHandler>(p1 - p2);
}

// Comparison operators

// Less than
template<typename T, typename U, typename E>
bool operator<(U lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return GreaterThanTest<T, U, ValidComparison<T, U>::method>::GreaterThan((T)rhs, lhs);
}

template<typename T, typename U, typename E>
bool operator<(SafeInt<T, E> lhs, U rhs) SAFEINT_NOTHROW
{
    return GreaterThanTest<U, T, ValidComparison<U, T>::method>::GreaterThan(rhs, (T)lhs);
}

template<typename T, typename U, typename E>
bool operator<(SafeInt<U, E> lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return GreaterThanTest<T, U, ValidComparison<T, U>::method>::GreaterThan((T)rhs, (U)lhs);
}

// Greater than
template<typename T, typename U, typename E>
bool operator>(U lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return GreaterThanTest<U, T, ValidComparison<U, T>::method>::GreaterThan(lhs, (T)rhs);
}

template<typename T, typename U, typename E>
bool operator>(SafeInt<T, E> lhs, U rhs) SAFEINT_NOTHROW
{
    return GreaterThanTest<T, U, ValidComparison<T, U>::method>::GreaterThan((T)lhs, rhs);
}

template<typename T, typename U, typename E>
bool operator>(SafeInt<T, E> lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    return GreaterThanTest<T, U, ValidComparison<T, U>::method>::GreaterThan((T)lhs, (U)rhs);
}

// Greater than or equal
template<typename T, typename U, typename E>
bool operator>=(U lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return !GreaterThanTest<T, U, ValidComparison<T, U>::method>::GreaterThan((T)rhs, lhs);
}

template<typename T, typename U, typename E>
bool operator>=(SafeInt<T, E> lhs, U rhs) SAFEINT_NOTHROW
{
    return !GreaterThanTest<U, T, ValidComparison<U, T>::method>::GreaterThan(rhs, (T)lhs);
}

template<typename T, typename U, typename E>
bool operator>=(SafeInt<T, E> lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    return !GreaterThanTest<U, T, ValidComparison<U, T>::method>::GreaterThan((U)rhs, (T)lhs);
}

// Less than or equal
template<typename T, typename U, typename E>
bool operator<=(U lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return !GreaterThanTest<U, T, ValidComparison<U, T>::method>::GreaterThan(lhs, (T)rhs);
}

template<typename T, typename U, typename E>
bool operator<=(SafeInt<T, E> lhs, U rhs) SAFEINT_NOTHROW
{
    return !GreaterThanTest<T, U, ValidComparison<T, U>::method>::GreaterThan((T)lhs, rhs);
}

template<typename T, typename U, typename E>
bool operator<=(SafeInt<T, E> lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    return !GreaterThanTest<T, U, ValidComparison<T, U>::method>::GreaterThan((T)lhs, (U)rhs);
}

// equality
// explicit overload for bool
template<typename T, typename E>
bool operator==(bool lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return lhs == ((T)rhs == 0 ? false : true);
}

template<typename T, typename E>
bool operator==(SafeInt<T, E> lhs, bool rhs) SAFEINT_NOTHROW
{
    return rhs == ((T)lhs == 0 ? false : true);
}

template<typename T, typename U, typename E>
bool operator==(U lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return EqualityTest<T, U, ValidComparison<T, U>::method>::IsEquals((T)rhs, lhs);
}

template<typename T, typename U, typename E>
bool operator==(SafeInt<T, E> lhs, U rhs) SAFEINT_NOTHROW
{
    return EqualityTest<T, U, ValidComparison<T, U>::method>::IsEquals((T)lhs, rhs);
}

template<typename T, typename U, typename E>
bool operator==(SafeInt<T, E> lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    return EqualityTest<T, U, ValidComparison<T, U>::method>::IsEquals((T)lhs, (U)rhs);
}

// not equals
template<typename T, typename U, typename E>
bool operator!=(U lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return !EqualityTest<T, U, ValidComparison<T, U>::method>::IsEquals((T)rhs, lhs);
}

template<typename T, typename U, typename E>
bool operator!=(SafeInt<T, E> lhs, U rhs) SAFEINT_NOTHROW
{
    return !EqualityTest<T, U, ValidComparison<T, U>::method>::IsEquals((T)lhs, rhs);
}

template<typename T, typename U, typename E>
bool operator!=(SafeInt<T, E> lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    return !EqualityTest<T, U, ValidComparison<T, U>::method>::IsEquals(lhs, rhs);
}

template<typename T, typename E>
bool operator!=(bool lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return ((T)rhs == 0 ? false : true) != lhs;
}

template<typename T, typename E>
bool operator!=(SafeInt<T, E> lhs, bool rhs) SAFEINT_NOTHROW
{
    return ((T)lhs == 0 ? false : true) != rhs;
}

template<typename T, typename U, typename E, int method>
class ModulusSimpleCaseHelper;

template<typename T, typename E, int method>
class ModulusSignedCaseHelper;

template<typename T, typename E>
class ModulusSignedCaseHelper<T, E, true>
{
public:
    static bool SignedCase(SafeInt<T, E> rhs, SafeInt<T, E>& result) SAFEINT_NOTHROW
    {
        if ((T)rhs == (T)-1)
        {
            result = 0;
            return true;
        }
        return false;
    }
};

template<typename T, typename E>
class ModulusSignedCaseHelper<T, E, false>
{
public:
    static bool SignedCase(SafeInt<T, E> /*rhs*/, SafeInt<T, E>& /*result*/) SAFEINT_NOTHROW { return false; }
};

template<typename T, typename U, typename E>
class ModulusSimpleCaseHelper<T, U, E, true>
{
public:
    static bool ModulusSimpleCase(U lhs, SafeInt<T, E> rhs, SafeInt<T, E>& result) SAFEINT_CPP_THROW
    {
        if (rhs != 0)
        {
            if (ModulusSignedCaseHelper<T, E, IntTraits<T>::isSigned>::SignedCase(rhs, result)) return true;

            result = SafeInt<T, E>((T)(lhs % (T)rhs));
            return true;
        }

        E::SafeIntOnDivZero();
    }
};

template<typename T, typename U, typename E>
class ModulusSimpleCaseHelper<T, U, E, false>
{
public:
    static bool ModulusSimpleCase(U /*lhs*/, SafeInt<T, E> /*rhs*/, SafeInt<T, E>& /*result*/) SAFEINT_NOTHROW
    {
        return false;
    }
};

// Modulus
template<typename T, typename U, typename E>
SafeInt<T, E> operator%(U lhs, SafeInt<T, E> rhs) SAFEINT_CPP_THROW
{
    // Value of return depends on sign of lhs
    // This one may not be safe - bounds check in constructor
    // if lhs is negative and rhs is unsigned, this will throw an exception.

    // Fast-track the simple case
    // same size and same sign
    SafeInt<T, E> result;

    if (ModulusSimpleCaseHelper < T,
        U,
        E,
        sizeof(T) == sizeof(U) &&
            (bool)IntTraits<T>::isSigned == (bool)IntTraits<U>::isSigned > ::ModulusSimpleCase(lhs, rhs, result))
        return result;

    return SafeInt<T, E>((SafeInt<U, E>(lhs) % (T)rhs));
}

// Multiplication
template<typename T, typename U, typename E>
SafeInt<T, E> operator*(U lhs, SafeInt<T, E> rhs)SAFEINT_CPP_THROW
{
    T ret(0);
    MultiplicationHelper<T, U, MultiplicationMethod<T, U>::method>::template MultiplyThrow<E>((T)rhs, lhs, ret);
    return SafeInt<T, E>(ret);
}

template<typename T, typename U, typename E, int method>
class DivisionNegativeCornerCaseHelper;

template<typename T, typename U, typename E>
class DivisionNegativeCornerCaseHelper<T, U, E, true>
{
public:
    static bool NegativeCornerCase(U lhs, SafeInt<T, E> rhs, SafeInt<T, E>& result) SAFEINT_CPP_THROW
    {
        // Problem case - normal casting behavior changes meaning
        // flip rhs to positive
        // any operator casts now do the right thing
        U tmp;

        if (CompileConst<sizeof(T) == 4>::Value())
            tmp = lhs / (U)(~(unsigned __int32)(T)rhs + 1);
        else
            tmp = lhs / (U)(~(unsigned __int64)(T)rhs + 1);

        if (tmp <= (U)IntTraits<T>::maxInt)
        {
            result = SafeInt<T, E>((T)(~(unsigned __int64)tmp + 1));
            return true;
        }

        // Corner case
        T maxT = IntTraits<T>::maxInt;
        if (tmp == (U)maxT + 1)
        {
            T minT = IntTraits<T>::minInt;
            result = SafeInt<T, E>(minT);
            return true;
        }

        E::SafeIntOnOverflow();
    }
};

template<typename T, typename U, typename E>
class DivisionNegativeCornerCaseHelper<T, U, E, false>
{
public:
    static bool NegativeCornerCase(U /*lhs*/, SafeInt<T, E> /*rhs*/, SafeInt<T, E>& /*result*/) SAFEINT_NOTHROW
    {
        return false;
    }
};

template<typename T, typename U, typename E, int method>
class DivisionCornerCaseHelper;

template<typename T, typename U, typename E>
class DivisionCornerCaseHelper<T, U, E, true>
{
public:
    static bool DivisionCornerCase1(U lhs, SafeInt<T, E> rhs, SafeInt<T, E>& result) SAFEINT_CPP_THROW
    {
        if ((T)rhs > 0)
        {
            result = SafeInt<T, E>(lhs / (T)rhs);
            return true;
        }

        // Now rhs is either negative, or zero
        if ((T)rhs != 0)
        {
            if (DivisionNegativeCornerCaseHelper < T,
                U,
                E,
                sizeof(U) >= 4 && sizeof(T) <= sizeof(U) > ::NegativeCornerCase(lhs, rhs, result))
                return true;

            result = SafeInt<T, E>(lhs / (T)rhs);
            return true;
        }

        E::SafeIntOnDivZero();
    }
};

template<typename T, typename U, typename E>
class DivisionCornerCaseHelper<T, U, E, false>
{
public:
    static bool DivisionCornerCase1(U /*lhs*/, SafeInt<T, E> /*rhs*/, SafeInt<T, E>& /*result*/) SAFEINT_NOTHROW
    {
        return false;
    }
};

template<typename T, typename U, typename E, int method>
class DivisionCornerCaseHelper2;

template<typename T, typename U, typename E>
class DivisionCornerCaseHelper2<T, U, E, true>
{
public:
    static bool DivisionCornerCase2(U lhs, SafeInt<T, E> rhs, SafeInt<T, E>& result) SAFEINT_CPP_THROW
    {
        if (lhs == IntTraits<U>::minInt && (T)rhs == -1)
        {
            // corner case of a corner case - lhs = min int, rhs = -1,
            // but rhs is the return type, so in essence, we can return -lhs
            // if rhs is a larger type than lhs
            // If types are wrong, throws

#if SAFEINT_COMPILER == VISUAL_STUDIO_COMPILER
#pragma warning(push)
// cast truncates constant value
#pragma warning(disable : 4310)
#endif

            if (CompileConst<sizeof(U) < sizeof(T)>::Value())
                result = SafeInt<T, E>((T)(-(T)IntTraits<U>::minInt));
            else
                E::SafeIntOnOverflow();

#if SAFEINT_COMPILER == VISUAL_STUDIO_COMPILER
#pragma warning(pop)
#endif

            return true;
        }

        return false;
    }
};

template<typename T, typename U, typename E>
class DivisionCornerCaseHelper2<T, U, E, false>
{
public:
    static bool DivisionCornerCase2(U /*lhs*/, SafeInt<T, E> /*rhs*/, SafeInt<T, E>& /*result*/) SAFEINT_NOTHROW
    {
        return false;
    }
};

// Division
template<typename T, typename U, typename E>
SafeInt<T, E> operator/(U lhs, SafeInt<T, E> rhs) SAFEINT_CPP_THROW
{
    // Corner case - has to be handled seperately
    SafeInt<T, E> result;
    if (DivisionCornerCaseHelper<T, U, E, (int)DivisionMethod<U, T>::method == (int)DivisionState_UnsignedSigned>::
            DivisionCornerCase1(lhs, rhs, result))
        return result;

    if (DivisionCornerCaseHelper2<T, U, E, SafeIntCompare<T, U>::isBothSigned>::DivisionCornerCase2(lhs, rhs, result))
        return result;

    // Otherwise normal logic works with addition of bounds check when casting from U->T
    U ret;
    DivisionHelper<U, T, DivisionMethod<U, T>::method>::template DivideThrow<E>(lhs, (T)rhs, ret);
    return SafeInt<T, E>(ret);
}

// Addition
template<typename T, typename U, typename E>
SafeInt<T, E> operator+(U lhs, SafeInt<T, E> rhs) SAFEINT_CPP_THROW
{
    T ret(0);
    AdditionHelper<T, U, AdditionMethod<T, U>::method>::template AdditionThrow<E>((T)rhs, lhs, ret);
    return SafeInt<T, E>(ret);
}

// Subtraction
template<typename T, typename U, typename E>
SafeInt<T, E> operator-(U lhs, SafeInt<T, E> rhs) SAFEINT_CPP_THROW
{
    T ret(0);
    SubtractionHelper<U, T, SubtractionMethod2<U, T>::method>::template SubtractThrow<E>(lhs, rhs.Ref(), ret);

    return SafeInt<T, E>(ret);
}

// Overrides designed to deal with cases where a SafeInt is assigned out
// to a normal int - this at least makes the last operation safe
// +=
template<typename T, typename U, typename E>
T& operator+=(T& lhs, SafeInt<U, E> rhs) SAFEINT_CPP_THROW
{
    T ret(0);
    AdditionHelper<T, U, AdditionMethod<T, U>::method>::template AdditionThrow<E>(lhs, (U)rhs, ret);
    lhs = ret;
    return lhs;
}

template<typename T, typename U, typename E>
T& operator-=(T& lhs, SafeInt<U, E> rhs) SAFEINT_CPP_THROW
{
    T ret(0);
    SubtractionHelper<T, U, SubtractionMethod<T, U>::method>::template SubtractThrow<E>(lhs, (U)rhs, ret);
    lhs = ret;
    return lhs;
}

template<typename T, typename U, typename E>
T& operator*=(T& lhs, SafeInt<U, E> rhs) SAFEINT_CPP_THROW
{
    T ret(0);
    MultiplicationHelper<T, U, MultiplicationMethod<T, U>::method>::template MultiplyThrow<E>(lhs, (U)rhs, ret);
    lhs = ret;
    return lhs;
}

template<typename T, typename U, typename E>
T& operator/=(T& lhs, SafeInt<U, E> rhs) SAFEINT_CPP_THROW
{
    T ret(0);
    DivisionHelper<T, U, DivisionMethod<T, U>::method>::template DivideThrow<E>(lhs, (U)rhs, ret);
    lhs = ret;
    return lhs;
}

template<typename T, typename U, typename E>
T& operator%=(T& lhs, SafeInt<U, E> rhs) SAFEINT_CPP_THROW
{
    T ret(0);
    ModulusHelper<T, U, ValidComparison<T, U>::method>::template ModulusThrow<E>(lhs, (U)rhs, ret);
    lhs = ret;
    return lhs;
}

template<typename T, typename U, typename E>
T& operator&=(T& lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    lhs = BinaryAndHelper<T, U, BinaryMethod<T, U>::method>::And(lhs, (U)rhs);
    return lhs;
}

template<typename T, typename U, typename E>
T& operator^=(T& lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    lhs = BinaryXorHelper<T, U, BinaryMethod<T, U>::method>::Xor(lhs, (U)rhs);
    return lhs;
}

template<typename T, typename U, typename E>
T& operator|=(T& lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    lhs = BinaryOrHelper<T, U, BinaryMethod<T, U>::method>::Or(lhs, (U)rhs);
    return lhs;
}

template<typename T, typename U, typename E>
T& operator<<=(T& lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    lhs = (T)(SafeInt<T, E>(lhs) << (U)rhs);
    return lhs;
}

template<typename T, typename U, typename E>
T& operator>>=(T& lhs, SafeInt<U, E> rhs) SAFEINT_NOTHROW
{
    lhs = (T)(SafeInt<T, E>(lhs) >> (U)rhs);
    return lhs;
}

// Specific pointer overrides
// Note - this function makes no attempt to ensure
// that the resulting pointer is still in the buffer, only
// that no int overflows happened on the way to getting the new pointer
template<typename T, typename U, typename E>
T*& operator+=(T*& lhs, SafeInt<U, E> rhs) SAFEINT_CPP_THROW
{
    // Cast the pointer to a number so we can do arithmetic
    SafeInt<size_t, E> ptr_val = reinterpret_cast<size_t>(lhs);
    // Check first that rhs is valid for the type of ptrdiff_t
    // and that multiplying by sizeof( T ) doesn't overflow a ptrdiff_t
    // Next, we need to add 2 SafeInts of different types, so unbox the ptr_diff
    // Finally, cast the number back to a pointer of the correct type
    lhs = reinterpret_cast<T*>((size_t)(ptr_val + (ptrdiff_t)(SafeInt<ptrdiff_t, E>(rhs) * sizeof(T))));
    return lhs;
}

template<typename T, typename U, typename E>
T*& operator-=(T*& lhs, SafeInt<U, E> rhs) SAFEINT_CPP_THROW
{
    // Cast the pointer to a number so we can do arithmetic
    SafeInt<size_t, E> ptr_val = reinterpret_cast<size_t>(lhs);
    // See above for comments
    lhs = reinterpret_cast<T*>((size_t)(ptr_val - (ptrdiff_t)(SafeInt<ptrdiff_t, E>(rhs) * sizeof(T))));
    return lhs;
}

template<typename T, typename U, typename E>
T*& operator*=(T*& lhs, SafeInt<U, E>) SAFEINT_NOTHROW
{
    // This operator explicitly not supported
    C_ASSERT(sizeof(T) == 0);
    return (lhs = NULL);
}

template<typename T, typename U, typename E>
T*& operator/=(T*& lhs, SafeInt<U, E>) SAFEINT_NOTHROW
{
    // This operator explicitly not supported
    C_ASSERT(sizeof(T) == 0);
    return (lhs = NULL);
}

template<typename T, typename U, typename E>
T*& operator%=(T*& lhs, SafeInt<U, E>) SAFEINT_NOTHROW
{
    // This operator explicitly not supported
    C_ASSERT(sizeof(T) == 0);
    return (lhs = NULL);
}

template<typename T, typename U, typename E>
T*& operator&=(T*& lhs, SafeInt<U, E>) SAFEINT_NOTHROW
{
    // This operator explicitly not supported
    C_ASSERT(sizeof(T) == 0);
    return (lhs = NULL);
}

template<typename T, typename U, typename E>
T*& operator^=(T*& lhs, SafeInt<U, E>) SAFEINT_NOTHROW
{
    // This operator explicitly not supported
    C_ASSERT(sizeof(T) == 0);
    return (lhs = NULL);
}

template<typename T, typename U, typename E>
T*& operator|=(T*& lhs, SafeInt<U, E>) SAFEINT_NOTHROW
{
    // This operator explicitly not supported
    C_ASSERT(sizeof(T) == 0);
    return (lhs = NULL);
}

template<typename T, typename U, typename E>
T*& operator<<=(T*& lhs, SafeInt<U, E>) SAFEINT_NOTHROW
{
    // This operator explicitly not supported
    C_ASSERT(sizeof(T) == 0);
    return (lhs = NULL);
}

template<typename T, typename U, typename E>
T*& operator>>=(T*& lhs, SafeInt<U, E>) SAFEINT_NOTHROW
{
    // This operator explicitly not supported
    C_ASSERT(sizeof(T) == 0);
    return (lhs = NULL);
}

// Shift operators
// NOTE - shift operators always return the type of the lhs argument

// Left shift
template<typename T, typename U, typename E>
SafeInt<U, E> operator<<(U lhs, SafeInt<T, E> bits) SAFEINT_NOTHROW
{
    ShiftAssert(!IntTraits<T>::isSigned || (T)bits >= 0);
    ShiftAssert((T)bits < (int)IntTraits<U>::bitCount);

    return SafeInt<U, E>((U)(lhs << (T)bits));
}

// Right shift
template<typename T, typename U, typename E>
SafeInt<U, E> operator>>(U lhs, SafeInt<T, E> bits) SAFEINT_NOTHROW
{
    ShiftAssert(!IntTraits<T>::isSigned || (T)bits >= 0);
    ShiftAssert((T)bits < (int)IntTraits<U>::bitCount);

    return SafeInt<U, E>((U)(lhs >> (T)bits));
}

// Bitwise operators
// This only makes sense if we're dealing with the same type and size
// demand a type T, or something that fits into a type T.

// Bitwise &
template<typename T, typename U, typename E>
SafeInt<T, E> operator&(U lhs, SafeInt<T, E> rhs)SAFEINT_NOTHROW
{
    return SafeInt<T, E>(BinaryAndHelper<T, U, BinaryMethod<T, U>::method>::And((T)rhs, lhs));
}

// Bitwise XOR
template<typename T, typename U, typename E>
SafeInt<T, E> operator^(U lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return SafeInt<T, E>(BinaryXorHelper<T, U, BinaryMethod<T, U>::method>::Xor((T)rhs, lhs));
}

// Bitwise OR
template<typename T, typename U, typename E>
SafeInt<T, E> operator|(U lhs, SafeInt<T, E> rhs) SAFEINT_NOTHROW
{
    return SafeInt<T, E>(BinaryOrHelper<T, U, BinaryMethod<T, U>::method>::Or((T)rhs, lhs));
}

#if SAFEINT_COMPILER == GCC_COMPILER
#pragma GCC diagnostic pop
#endif

#if SAFEINT_COMPILER == CLANG_COMPILER
#pragma clang diagnostic pop
#endif

#ifdef C_ASSERT_DEFINED_SAFEINT
#undef C_ASSERT
#undef C_ASSERT_DEFINED_SAFEINT
#endif // C_ASSERT_DEFINED_SAFEINT

} // namespace safeint3
} // namespace msl
