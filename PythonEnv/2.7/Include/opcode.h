#ifndef Py_OPCODE_H
#define Py_OPCODE_H
#ifdef __cplusplus
extern "C" {
#endif


/* Instruction opcodes for compiled code */

#define STOP_CODE	0
#define POP_TOP		1
#define ROT_TWO		2
#define ROT_THREE	3
#define DUP_TOP		4
#define ROT_FOUR	5
#define NOP		9

#define UNARY_POSITIVE	10
#define UNARY_NEGATIVE	11
#define UNARY_NOT	12
#define UNARY_CONVERT	13

#define UNARY_INVERT	15

#define BINARY_POWER	19

#define BINARY_MULTIPLY	20
#define BINARY_DIVIDE	21
#define BINARY_MODULO	22
#define BINARY_ADD	23
#define BINARY_SUBTRACT	24
#define BINARY_SUBSCR	25
#define BINARY_FLOOR_DIVIDE 26
#define BINARY_TRUE_DIVIDE 27
#define INPLACE_FLOOR_DIVIDE 28
#define INPLACE_TRUE_DIVIDE 29

#define SLICE		30
/* Also uses 31-33 */

#define STORE_SLICE	40
/* Also uses 41-43 */

#define DELETE_SLICE	50
/* Also uses 51-53 */

#define STORE_MAP	54
#define INPLACE_ADD	55
#define INPLACE_SUBTRACT	56
#define INPLACE_MULTIPLY	57
#define INPLACE_DIVIDE	58
#define INPLACE_MODULO	59
#define STORE_SUBSCR	60
#define DELETE_SUBSCR	61

#define BINARY_LSHIFT	62
#define BINARY_RSHIFT	63
#define BINARY_AND	64
#define BINARY_XOR	65
#define BINARY_OR	66
#define INPLACE_POWER	67
#define GET_ITER	68

#define PRINT_EXPR	70
#define PRINT_ITEM	71
#define PRINT_NEWLINE	72
#define PRINT_ITEM_TO   73
#define PRINT_NEWLINE_TO 74
#define INPLACE_LSHIFT	75
#define INPLACE_RSHIFT	76
#define INPLACE_AND	77
#define INPLACE_XOR	78
#define INPLACE_OR	79
#define BREAK_LOOP	80
#define WITH_CLEANUP    81
#define LOAD_LOCALS	82
#define RETURN_VALUE	83
#define IMPORT_STAR	84
#define EXEC_STMT	85
#define YIELD_VALUE	86
#define POP_BLOCK	87
#define END_FINALLY	88
#define BUILD_CLASS	89

#define HAVE_ARGUMENT	90	/* Opcodes from here have an argument: */

#define STORE_NAME	90	/* Index in name list */
#define DELETE_NAME	91	/* "" */
#define UNPACK_SEQUENCE	92	/* Number of sequence items */
#define FOR_ITER	93
#define LIST_APPEND	94

#define STORE_ATTR	95	/* Index in name list */
#define DELETE_ATTR	96	/* "" */
#define STORE_GLOBAL	97	/* "" */
#define DELETE_GLOBAL	98	/* "" */
#define DUP_TOPX	99	/* number of items to duplicate */
#define LOAD_CONST	100	/* Index in const list */
#define LOAD_NAME	101	/* Index in name list */
#define BUILD_TUPLE	102	/* Number of tuple items */
#define BUILD_LIST	103	/* Number of list items */
#define BUILD_SET	104     /* Number of set items */
#define BUILD_MAP	105	/* Always zero for now */
#define LOAD_ATTR	106	/* Index in name list */
#define COMPARE_OP	107	/* Comparison operator */
#define IMPORT_NAME	108	/* Index in name list */
#define IMPORT_FROM	109	/* Index in name list */
#define JUMP_FORWARD	110	/* Number of bytes to skip */

#define JUMP_IF_FALSE_OR_POP 111 /* Target byte offset from beginning
                                    of code */
#define JUMP_IF_TRUE_OR_POP 112	/* "" */
#define JUMP_ABSOLUTE	113	/* "" */
#define POP_JUMP_IF_FALSE 114	/* "" */
#define POP_JUMP_IF_TRUE 115	/* "" */

#define LOAD_GLOBAL	116	/* Index in name list */

#define CONTINUE_LOOP	119	/* Start of loop (absolute) */
#define SETUP_LOOP	120	/* Target address (relative) */
#define SETUP_EXCEPT	121	/* "" */
#define SETUP_FINALLY	122	/* "" */

#define LOAD_FAST	124	/* Local variable number */
#define STORE_FAST	125	/* Local variable number */
#define DELETE_FAST	126	/* Local variable number */

#define RAISE_VARARGS	130	/* Number of raise arguments (1, 2 or 3) */
/* CALL_FUNCTION_XXX opcodes defined below depend on this definition */
#define CALL_FUNCTION	131	/* #args + (#kwargs<<8) */
#define MAKE_FUNCTION	132	/* #defaults */
#define BUILD_SLICE 	133	/* Number of items */

#define MAKE_CLOSURE    134     /* #free vars */
#define LOAD_CLOSURE    135     /* Load free variable from closure */
#define LOAD_DEREF      136     /* Load and dereference from closure cell */ 
#define STORE_DEREF     137     /* Store into cell */ 

/* The next 3 opcodes must be contiguous and satisfy
   (CALL_FUNCTION_VAR - CALL_FUNCTION) & 3 == 1  */
#define CALL_FUNCTION_VAR          140	/* #args + (#kwargs<<8) */
#define CALL_FUNCTION_KW           141	/* #args + (#kwargs<<8) */
#define CALL_FUNCTION_VAR_KW       142	/* #args + (#kwargs<<8) */

#define SETUP_WITH 143

/* Support for opargs more than 16 bits long */
#define EXTENDED_ARG  145

#define SET_ADD         146
#define MAP_ADD         147


enum cmp_op {PyCmp_LT=Py_LT, PyCmp_LE=Py_LE, PyCmp_EQ=Py_EQ, PyCmp_NE=Py_NE, PyCmp_GT=Py_GT, PyCmp_GE=Py_GE,
	     PyCmp_IN, PyCmp_NOT_IN, PyCmp_IS, PyCmp_IS_NOT, PyCmp_EXC_MATCH, PyCmp_BAD};

#define HAS_ARG(op) ((op) >= HAVE_ARGUMENT)

#ifdef __cplusplus
}
#endif
#endif /* !Py_OPCODE_H */
