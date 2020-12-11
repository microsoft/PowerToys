#define _CRT_SECURE_NO_WARNINGS

#include <windows.h>
#include <stdio.h>
#include <tchar.h>
#include "zip.h"


// THIS FILE is almost entirely based upon code by info-zip.
// It has been modified by Lucian Wischik. The modifications
// were a complete rewrite of the bit of code that generates the
// layout of the zipfile, and support for zipping to/from memory
// or handles or pipes or pagefile or diskfiles, encryption, unicode.
// The original code may be found at http://www.info-zip.org
// The original copyright text follows.
//
//
//
// This is version 1999-Oct-05 of the Info-ZIP copyright and license.
// The definitive version of this document should be available at
// ftp://ftp.cdrom.com/pub/infozip/license.html indefinitely.
//
// Copyright (c) 1990-1999 Info-ZIP.  All rights reserved.
//
// For the purposes of this copyright and license, "Info-ZIP" is defined as
// the following set of individuals:
//
//   Mark Adler, John Bush, Karl Davis, Harald Denker, Jean-Michel Dubois,
//   Jean-loup Gailly, Hunter Goatley, Ian Gorman, Chris Herborth, Dirk Haase,
//   Greg Hartwig, Robert Heath, Jonathan Hudson, Paul Kienitz, David Kirschbaum,
//   Johnny Lee, Onno van der Linden, Igor Mandrichenko, Steve P. Miller,
//   Sergio Monesi, Keith Owens, George Petrov, Greg Roelofs, Kai Uwe Rommel,
//   Steve Salisbury, Dave Smith, Christian Spieler, Antoine Verheijen,
//   Paul von Behren, Rich Wales, Mike White
//
// This software is provided "as is," without warranty of any kind, express
// or implied.  In no event shall Info-ZIP or its contributors be held liable
// for any direct, indirect, incidental, special or consequential damages
// arising out of the use of or inability to use this software.
//
// Permission is granted to anyone to use this software for any purpose,
// including commercial applications, and to alter it and redistribute it
// freely, subject to the following restrictions:
//
//    1. Redistributions of source code must retain the above copyright notice,
//       definition, disclaimer, and this list of conditions.
//
//    2. Redistributions in binary form must reproduce the above copyright
//       notice, definition, disclaimer, and this list of conditions in
//       documentation and/or other materials provided with the distribution.
//
//    3. Altered versions--including, but not limited to, ports to new operating
//       systems, existing ports with new graphical interfaces, and dynamic,
//       shared, or static library versions--must be plainly marked as such
//       and must not be misrepresented as being the original source.  Such
//       altered versions also must not be misrepresented as being Info-ZIP
//       releases--including, but not limited to, labeling of the altered
//       versions with the names "Info-ZIP" (or any variation thereof, including,
//       but not limited to, different capitalizations), "Pocket UnZip," "WiZ"
//       or "MacZip" without the explicit permission of Info-ZIP.  Such altered
//       versions are further prohibited from misrepresentative use of the
//       Zip-Bugs or Info-ZIP e-mail addresses or of the Info-ZIP URL(s).
//
//    4. Info-ZIP retains the right to use the names "Info-ZIP," "Zip," "UnZip,"
//       "WiZ," "Pocket UnZip," "Pocket Zip," and "MacZip" for its own source and
//       binary releases.
//


typedef unsigned char uch;      // unsigned 8-bit value
typedef unsigned short ush;     // unsigned 16-bit value
typedef unsigned long ulg;      // unsigned 32-bit value
typedef size_t extent;          // file size
typedef unsigned Pos;   // must be at least 32 bits
typedef unsigned IPos; // A Pos is an index in the character window. Pos is used only for parameter passing

#ifndef EOF
#define EOF (-1)
#endif


// Error return values.  The values 0..4 and 12..18 follow the conventions
// of PKZIP.   The values 4..10 are all assigned to "insufficient memory"
// by PKZIP, so the codes 5..10 are used here for other purposes.
#define ZE_MISS         -1      // used by procname(), zipbare()
#define ZE_OK           0       // success
#define ZE_EOF          2       // unexpected end of zip file
#define ZE_FORM         3       // zip file structure error
#define ZE_MEM          4       // out of memory
#define ZE_LOGIC        5       // internal logic error
#define ZE_BIG          6       // entry too large to split
#define ZE_NOTE         7       // invalid comment format
#define ZE_TEST         8       // zip test (-T) failed or out of memory
#define ZE_ABORT        9       // user interrupt or termination
#define ZE_TEMP         10      // error using a temp file
#define ZE_READ         11      // read or seek error
#define ZE_NONE         12      // nothing to do
#define ZE_NAME         13      // missing or empty zip file
#define ZE_WRITE        14      // error writing to a file
#define ZE_CREAT        15      // couldn't open to write
#define ZE_PARMS        16      // bad command line
#define ZE_OPEN         18      // could not open a specified file to read
#define ZE_MAXERR       18      // the highest error number


// internal file attribute
#define UNKNOWN (-1)
#define BINARY  0
#define ASCII   1

#define BEST -1                 // Use best method (deflation or store)
#define STORE 0                 // Store method
#define DEFLATE 8               // Deflation method

#define CRCVAL_INITIAL  0L

// MSDOS file or directory attributes
#define MSDOS_HIDDEN_ATTR 0x02
#define MSDOS_DIR_ATTR 0x10

// Lengths of headers after signatures in bytes
#define LOCHEAD 26
#define CENHEAD 42
#define ENDHEAD 18

// Definitions for extra field handling:
#define EB_HEADSIZE       4     /* length of a extra field block header */
#define EB_LEN            2     /* offset of data length field in header */
#define EB_UT_MINLEN      1     /* minimal UT field contains Flags byte */
#define EB_UT_FLAGS       0     /* byte offset of Flags field */
#define EB_UT_TIME1       1     /* byte offset of 1st time value */
#define EB_UT_FL_MTIME    (1 << 0)      /* mtime present */
#define EB_UT_FL_ATIME    (1 << 1)      /* atime present */
#define EB_UT_FL_CTIME    (1 << 2)      /* ctime present */
#define EB_UT_LEN(n)      (EB_UT_MINLEN + 4 * (n))
#define EB_L_UT_SIZE    (EB_HEADSIZE + EB_UT_LEN(3))
#define EB_C_UT_SIZE    (EB_HEADSIZE + EB_UT_LEN(1))


// Macros for writing machine integers to little-endian format
#define PUTSH(a,f) {char _putsh_c=(char)((a)&0xff); wfunc(param,&_putsh_c,1); _putsh_c=(char)((a)>>8); wfunc(param,&_putsh_c,1);}
#define PUTLG(a,f) {PUTSH((a) & 0xffff,(f)) PUTSH((a) >> 16,(f))}


// -- Structure of a ZIP file --
// Signatures for zip file information headers
#define LOCSIG     0x04034b50L
#define CENSIG     0x02014b50L
#define ENDSIG     0x06054b50L
#define EXTLOCSIG  0x08074b50L


#define MIN_MATCH  3
#define MAX_MATCH  258
// The minimum and maximum match lengths


#define WSIZE  (0x8000)
// Maximum window size = 32K. If you are really short of memory, compile
// with a smaller WSIZE but this reduces the compression ratio for files
// of size > WSIZE. WSIZE must be a power of two in the current implementation.
//

#define MIN_LOOKAHEAD (MAX_MATCH+MIN_MATCH+1)
// Minimum amount of lookahead, except at the end of the input file.
// See deflate.c for comments about the MIN_MATCH+1.
//

#define MAX_DIST  (WSIZE-MIN_LOOKAHEAD)
// In order to simplify the code, particularly on 16 bit machines, match
// distances are limited to MAX_DIST instead of WSIZE.
//


#define ZIP_HANDLE   1
#define ZIP_FILENAME 2
#define ZIP_MEMORY   3
#define ZIP_FOLDER   4



// ===========================================================================
// Constants
//

#define MAX_BITS 15
// All codes must not exceed MAX_BITS bits

#define MAX_BL_BITS 7
// Bit length codes must not exceed MAX_BL_BITS bits

#define LENGTH_CODES 29
// number of length codes, not counting the special END_BLOCK code

#define LITERALS  256
// number of literal bytes 0..255

#define END_BLOCK 256
// end of block literal code

#define L_CODES (LITERALS+1+LENGTH_CODES)
// number of Literal or Length codes, including the END_BLOCK code

#define D_CODES   30
// number of distance codes

#define BL_CODES  19
// number of codes used to transfer the bit lengths


#define STORED_BLOCK 0
#define STATIC_TREES 1
#define DYN_TREES    2
// The three kinds of block type

#define LIT_BUFSIZE  0x8000
#define DIST_BUFSIZE  LIT_BUFSIZE
// Sizes of match buffers for literals/lengths and distances.  There are
// 4 reasons for limiting LIT_BUFSIZE to 64K:
//   - frequencies can be kept in 16 bit counters
//   - if compression is not successful for the first block, all input data is
//     still in the window so we can still emit a stored block even when input
//     comes from standard input.  (This can also be done for all blocks if
//     LIT_BUFSIZE is not greater than 32K.)
//   - if compression is not successful for a file smaller than 64K, we can
//     even emit a stored file instead of a stored block (saving 5 bytes).
//   - creating new Huffman trees less frequently may not provide fast
//     adaptation to changes in the input data statistics. (Take for
//     example a binary file with poorly compressible code followed by
//     a highly compressible string table.) Smaller buffer sizes give
//     fast adaptation but have of course the overhead of transmitting trees
//     more frequently.
//   - I can't count above 4
// The current code is general and allows DIST_BUFSIZE < LIT_BUFSIZE (to save
// memory at the expense of compression). Some optimizations would be possible
// if we rely on DIST_BUFSIZE == LIT_BUFSIZE.
//

#define REP_3_6      16
// repeat previous bit length 3-6 times (2 bits of repeat count)

#define REPZ_3_10    17
// repeat a zero length 3-10 times  (3 bits of repeat count)

#define REPZ_11_138  18
// repeat a zero length 11-138 times  (7 bits of repeat count)

#define HEAP_SIZE (2*L_CODES+1)
// maximum heap size


// ===========================================================================
// Local data used by the "bit string" routines.
//

#define Buf_size (8 * 2*sizeof(char))
// Number of bits used within bi_buf. (bi_buf may be implemented on
// more than 16 bits on some systems.)

// Output a 16 bit value to the bit stream, lower (oldest) byte first
#define PUTSHORT(state,w) \
{ if (state.bs.out_offset >= state.bs.out_size-1) \
    state.flush_outbuf(state.param,state.bs.out_buf, &state.bs.out_offset); \
  state.bs.out_buf[state.bs.out_offset++] = (char) ((w) & 0xff); \
  state.bs.out_buf[state.bs.out_offset++] = (char) ((ush)(w) >> 8); \
}

#define PUTBYTE(state,b) \
{ if (state.bs.out_offset >= state.bs.out_size) \
    state.flush_outbuf(state.param,state.bs.out_buf, &state.bs.out_offset); \
  state.bs.out_buf[state.bs.out_offset++] = (char) (b); \
}

// DEFLATE.CPP HEADER

#define HASH_BITS  15
// For portability to 16 bit machines, do not use values above 15.

#define HASH_SIZE (unsigned)(1<<HASH_BITS)
#define HASH_MASK (HASH_SIZE-1)
#define WMASK     (WSIZE-1)
// HASH_SIZE and WSIZE must be powers of two

#define NIL 0
// Tail of hash chains

#define FAST 4
#define SLOW 2
// speed options for the general purpose bit flag

#define TOO_FAR 4096
// Matches of length 3 are discarded if their distance exceeds TOO_FAR



#define EQUAL 0
// result of memcmp for equal strings


// ===========================================================================
// Local data used by the "longest match" routines.

#define H_SHIFT  ((HASH_BITS+MIN_MATCH-1)/MIN_MATCH)
// Number of bits by which ins_h and del_h must be shifted at each
// input step. It must be such that after MIN_MATCH steps, the oldest
// byte no longer takes part in the hash key, that is:
//   H_SHIFT * MIN_MATCH >= HASH_BITS

#define max_insert_length  max_lazy_match
// Insert new strings in the hash table only if the match length
// is not greater than this length. This saves time but degrades compression.
// max_insert_length is used only for compression levels <= 3.



const int extra_lbits[LENGTH_CODES] // extra bits for each length code
   = {0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,0};

const int extra_dbits[D_CODES] // extra bits for each distance code
   = {0,0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13};

const int extra_blbits[BL_CODES]// extra bits for each bit length code
   = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,3,7};

const uch bl_order[BL_CODES] = {16,17,18,0,8,7,9,6,10,5,11,4,12,3,13,2,14,1,15};
// The lengths of the bit length codes are sent in order of decreasing
// probability, to avoid transmitting the lengths for unused bit length codes.


typedef struct config {
   ush good_length; // reduce lazy search above this match length
   ush max_lazy;    // do not perform lazy search above this match length
   ush nice_length; // quit search above this match length
   ush max_chain;
} config;

// Values for max_lazy_match, good_match, nice_match and max_chain_length,
// depending on the desired pack level (0..9). The values given below have
// been tuned to exclude worst case performance for pathological files.
// Better values may be found for specific files.
//

const config configuration_table[10] = {
//  good lazy nice chain
    {0,    0,  0,    0},  // 0 store only
    {4,    4,  8,    4},  // 1 maximum speed, no lazy matches
    {4,    5, 16,    8},  // 2
    {4,    6, 32,   32},  // 3
    {4,    4, 16,   16},  // 4 lazy matches */
    {8,   16, 32,   32},  // 5
    {8,   16, 128, 128},  // 6
    {8,   32, 128, 256},  // 7
    {32, 128, 258, 1024}, // 8
    {32, 258, 258, 4096}};// 9 maximum compression */

// Note: the deflate() code requires max_lazy >= MIN_MATCH and max_chain >= 4
// For deflate_fast() (levels <= 3) good is ignored and lazy has a different meaning.







// Data structure describing a single value and its code string.
typedef struct ct_data {
    union {
        ush  freq;       // frequency count
        ush  code;       // bit string
    } fc;
    union {
        ush  dad;        // father node in Huffman tree
        ush  len;        // length of bit string
    } dl;
} ct_data;

typedef struct tree_desc {
    ct_data *dyn_tree;      // the dynamic tree
    ct_data *static_tree;   // corresponding static tree or NULL
    const int *extra_bits;  // extra bits for each code or NULL
    int     extra_base;     // base index for extra_bits
    int     elems;          // max number of elements in the tree
    int     max_length;     // max bit length for the codes
    int     max_code;       // largest code with non zero frequency
} tree_desc;




class TTreeState
{ public:
  TTreeState();

  ct_data dyn_ltree[HEAP_SIZE];    // literal and length tree
  ct_data dyn_dtree[2*D_CODES+1];  // distance tree
  ct_data static_ltree[L_CODES+2]; // the static literal tree...
  // ... Since the bit lengths are imposed, there is no need for the L_CODES
  // extra codes used during heap construction. However the codes 286 and 287
  // are needed to build a canonical tree (see ct_init below).
  ct_data static_dtree[D_CODES]; // the static distance tree...
  // ... (Actually a trivial tree since all codes use 5 bits.)
  ct_data bl_tree[2*BL_CODES+1];  // Huffman tree for the bit lengths

  tree_desc l_desc;
  tree_desc d_desc;
  tree_desc bl_desc;

  ush bl_count[MAX_BITS+1];  // number of codes at each bit length for an optimal tree

  int heap[2*L_CODES+1]; // heap used to build the Huffman trees
  int heap_len;               // number of elements in the heap
  int heap_max;               // element of largest frequency
  // The sons of heap[n] are heap[2*n] and heap[2*n+1]. heap[0] is not used.
  // The same heap array is used to build all trees.

  uch depth[2*L_CODES+1];
  // Depth of each subtree used as tie breaker for trees of equal frequency

  uch length_code[MAX_MATCH-MIN_MATCH+1];
  // length code for each normalized match length (0 == MIN_MATCH)

  uch dist_code[512];
  // distance codes. The first 256 values correspond to the distances
  // 3 .. 258, the last 256 values correspond to the top 8 bits of
  // the 15 bit distances.

  int base_length[LENGTH_CODES];
  // First normalized length for each code (0 = MIN_MATCH)

  int base_dist[D_CODES];
  // First normalized distance for each code (0 = distance of 1)

  uch far l_buf[LIT_BUFSIZE];  // buffer for literals/lengths
  ush far d_buf[DIST_BUFSIZE]; // buffer for distances

  uch flag_buf[(LIT_BUFSIZE/8)];
  // flag_buf is a bit array distinguishing literals from lengths in
  // l_buf, and thus indicating the presence or absence of a distance.

  unsigned last_lit;    // running index in l_buf
  unsigned last_dist;   // running index in d_buf
  unsigned last_flags;  // running index in flag_buf
  uch flags;            // current flags not yet saved in flag_buf
  uch flag_bit;         // current bit used in flags
  // bits are filled in flags starting at bit 0 (least significant).
  // Note: these flags are overkill in the current code since we don't
  // take advantage of DIST_BUFSIZE == LIT_BUFSIZE.

  ulg opt_len;          // bit length of current block with optimal trees
  ulg static_len;       // bit length of current block with static trees

  ulg cmpr_bytelen;     // total byte length of compressed file
  ulg cmpr_len_bits;    // number of bits past 'cmpr_bytelen'

  ulg input_len;        // total byte length of input file
  // input_len is for debugging only since we can get it by other means.

  ush *file_type;       // pointer to UNKNOWN, BINARY or ASCII
//  int *file_method;     // pointer to DEFLATE or STORE
};

TTreeState::TTreeState()
{ tree_desc a = {dyn_ltree, static_ltree, extra_lbits, LITERALS+1, L_CODES, MAX_BITS, 0};  l_desc = a;
  tree_desc b = {dyn_dtree, static_dtree, extra_dbits, 0,          D_CODES, MAX_BITS, 0};  d_desc = b;
  tree_desc c = {bl_tree, NULL,       extra_blbits, 0,         BL_CODES, MAX_BL_BITS, 0};  bl_desc = c;
  last_lit=0;
  last_dist=0;
  last_flags=0;
}



class TBitState
{ public:

  int flush_flg;
  //
  unsigned bi_buf;
  // Output buffer. bits are inserted starting at the bottom (least significant
  // bits). The width of bi_buf must be at least 16 bits.
  int bi_valid;
  // Number of valid bits in bi_buf.  All bits above the last valid bit
  // are always zero.
  char *out_buf;
  // Current output buffer.
  unsigned out_offset;
  // Current offset in output buffer.
  // On 16 bit machines, the buffer is limited to 64K.
  unsigned out_size;
  // Size of current output buffer
  ulg bits_sent;   // bit length of the compressed data  only needed for debugging???
};







class TDeflateState
{ public:
  TDeflateState() {window_size=0;}

  uch    window[2L*WSIZE];
  // Sliding window. Input bytes are read into the second half of the window,
  // and move to the first half later to keep a dictionary of at least WSIZE
  // bytes. With this organization, matches are limited to a distance of
  // WSIZE-MAX_MATCH bytes, but this ensures that IO is always
  // performed with a length multiple of the block size. Also, it limits
  // the window size to 64K, which is quite useful on MSDOS.
  // To do: limit the window size to WSIZE+CBSZ if SMALL_MEM (the code would
  // be less efficient since the data would have to be copied WSIZE/CBSZ times)
  Pos    prev[WSIZE];
  // Link to older string with same hash index. To limit the size of this
  // array to 64K, this link is maintained only for the last 32K strings.
  // An index in this array is thus a window index modulo 32K.
  Pos    head[HASH_SIZE];
  // Heads of the hash chains or NIL. If your compiler thinks that
  // HASH_SIZE is a dynamic value, recompile with -DDYN_ALLOC.

  ulg window_size;
  // window size, 2*WSIZE except for MMAP or BIG_MEM, where it is the
  // input file length plus MIN_LOOKAHEAD.

  long block_start;
  // window position at the beginning of the current output block. Gets
  // negative when the window is moved backwards.

  int sliding;
  // Set to false when the input file is already in memory

  unsigned ins_h;  // hash index of string to be inserted

  unsigned int prev_length;
  // Length of the best match at previous step. Matches not greater than this
  // are discarded. This is used in the lazy match evaluation.

  unsigned strstart;         // start of string to insert
  unsigned match_start; // start of matching string
  int      eofile;           // flag set at end of input file
  unsigned lookahead;        // number of valid bytes ahead in window

  unsigned max_chain_length;
  // To speed up deflation, hash chains are never searched beyond this length.
  // A higher limit improves compression ratio but degrades the speed.

  unsigned int max_lazy_match;
  // Attempt to find a better match only when the current match is strictly
  // smaller than this value. This mechanism is used only for compression
  // levels >= 4.

  unsigned good_match;
  // Use a faster search when the previous match is longer than this

  int nice_match; // Stop searching when current match exceeds this
};

typedef __int64 lutime_t;       // define it ourselves since we don't include time.h

typedef struct iztimes {
  lutime_t atime,mtime,ctime;
} iztimes; // access, modify, create times

typedef struct zlist {
  ush vem, ver, flg, how;       // See central header in zipfile.c for what vem..off are
  ulg tim, crc, siz, len;
  extent nam, ext, cext, com;   // offset of ext must be >= LOCHEAD
  ush dsk, att, lflg;           // offset of lflg must be >= LOCHEAD
  ulg atx, off;
  char name[MAX_PATH];          // File name in zip file
  char *extra;                  // Extra field (set only if ext != 0)
  char *cextra;                 // Extra in central (set only if cext != 0)
  char *comment;                // Comment (set only if com != 0)
  char iname[MAX_PATH];         // Internal file name after cleanup
  char zname[MAX_PATH];         // External version of internal name
  int mark;                     // Marker for files to operate on
  int trash;                    // Marker for files to delete
  int dosflag;                  // Set to force MSDOS file attributes
  struct zlist far *nxt;        // Pointer to next header in list
} TZipFileInfo;


struct TState;
typedef unsigned (*READFUNC)(TState &state, char *buf,unsigned size);
typedef unsigned (*FLUSHFUNC)(void *param, const char *buf, unsigned *size);
typedef unsigned (*WRITEFUNC)(void *param, const char *buf, unsigned size);
struct TState
{ void *param;
  int level; bool seekable;
  READFUNC readfunc; FLUSHFUNC flush_outbuf;
  TTreeState ts; TBitState bs; TDeflateState ds;
  const char *err;
};









void Assert(TState &state,bool cond, const char *msg)
{ if (cond) return;
  state.err=msg;
}
void __cdecl Trace(const char *x, ...) {va_list paramList; va_start(paramList, x); paramList; va_end(paramList);}
void __cdecl Tracec(bool ,const char *x, ...) {va_list paramList; va_start(paramList, x); paramList; va_end(paramList);}



// ===========================================================================
// Local (static) routines in this file.
//

void init_block     (TState &);
void pqdownheap     (TState &,ct_data *tree, int k);
void gen_bitlen     (TState &,tree_desc *desc);
void gen_codes      (TState &state,ct_data *tree, int max_code);
void build_tree     (TState &,tree_desc *desc);
void scan_tree      (TState &,ct_data *tree, int max_code);
void send_tree      (TState &state,ct_data *tree, int max_code);
int  build_bl_tree  (TState &);
void send_all_trees (TState &state,int lcodes, int dcodes, int blcodes);
void compress_block (TState &state,ct_data *ltree, ct_data *dtree);
void set_file_type  (TState &);
void send_bits      (TState &state, int value, int length);
unsigned bi_reverse (unsigned code, int len);
void bi_windup      (TState &state);
void copy_block     (TState &state,char *buf, unsigned len, int header);


#define send_code(state, c, tree) send_bits(state, tree[c].fc.code, tree[c].dl.len)
// Send a code of the given tree. c and tree must not have side effects

// alternatively...
//#define send_code(state, c, tree)
//     { if (state.verbose>1) fprintf(stderr,"\ncd %3d ",(c));
//       send_bits(state, tree[c].fc.code, tree[c].dl.len); }

#define d_code(dist) ((dist) < 256 ? state.ts.dist_code[dist] : state.ts.dist_code[256+((dist)>>7)])
// Mapping from a distance to a distance code. dist is the distance - 1 and
// must not have side effects. dist_code[256] and dist_code[257] are never used.

#define Max(a,b) (a >= b ? a : b)
/* the arguments must not have side effects */

/* ===========================================================================
 * Allocate the match buffer, initialize the various tables and save the
 * location of the internal file attribute (ascii/binary) and method
 * (DEFLATE/STORE).
 */
void ct_init(TState &state, ush *attr)
{
    int n;        /* iterates over tree elements */
    int bits;     /* bit counter */
    int length;   /* length value */
    int code;     /* code value */
    int dist;     /* distance index */

    state.ts.file_type = attr;
    //state.ts.file_method = method;
    state.ts.cmpr_bytelen = state.ts.cmpr_len_bits = 0L;
    state.ts.input_len = 0L;

    if (state.ts.static_dtree[0].dl.len != 0) return; /* ct_init already called */

    /* Initialize the mapping length (0..255) -> length code (0..28) */
    length = 0;
    for (code = 0; code < LENGTH_CODES-1; code++) {
        state.ts.base_length[code] = length;
        for (n = 0; n < (1<<extra_lbits[code]); n++) {
            state.ts.length_code[length++] = (uch)code;
        }
    }
    Assert(state,length == 256, "ct_init: length != 256");
    /* Note that the length 255 (match length 258) can be represented
     * in two different ways: code 284 + 5 bits or code 285, so we
     * overwrite length_code[255] to use the best encoding:
     */
    state.ts.length_code[length-1] = (uch)code;

    /* Initialize the mapping dist (0..32K) -> dist code (0..29) */
    dist = 0;
    for (code = 0 ; code < 16; code++) {
        state.ts.base_dist[code] = dist;
        for (n = 0; n < (1<<extra_dbits[code]); n++) {
            state.ts.dist_code[dist++] = (uch)code;
        }
    }
    Assert(state,dist == 256, "ct_init: dist != 256");
    dist >>= 7; /* from now on, all distances are divided by 128 */
    for ( ; code < D_CODES; code++) {
        state.ts.base_dist[code] = dist << 7;
        for (n = 0; n < (1<<(extra_dbits[code]-7)); n++) {
            state.ts.dist_code[256 + dist++] = (uch)code;
        }
    }
    Assert(state,dist == 256, "ct_init: 256+dist != 512");

    /* Construct the codes of the static literal tree */
    for (bits = 0; bits <= MAX_BITS; bits++) state.ts.bl_count[bits] = 0;
    n = 0;
    while (n <= 143) state.ts.static_ltree[n++].dl.len = 8, state.ts.bl_count[8]++;
    while (n <= 255) state.ts.static_ltree[n++].dl.len = 9, state.ts.bl_count[9]++;
    while (n <= 279) state.ts.static_ltree[n++].dl.len = 7, state.ts.bl_count[7]++;
    while (n <= 287) state.ts.static_ltree[n++].dl.len = 8, state.ts.bl_count[8]++;
    /* fc.codes 286 and 287 do not exist, but we must include them in the
     * tree construction to get a canonical Huffman tree (longest code
     * all ones)
     */
    gen_codes(state,(ct_data *)state.ts.static_ltree, L_CODES+1);

    /* The static distance tree is trivial: */
    for (n = 0; n < D_CODES; n++) {
        state.ts.static_dtree[n].dl.len = 5;
        state.ts.static_dtree[n].fc.code = (ush)bi_reverse(n, 5);
    }

    /* Initialize the first block of the first file: */
    init_block(state);
}

/* ===========================================================================
 * Initialize a new block.
 */
void init_block(TState &state)
{
    int n; /* iterates over tree elements */

    /* Initialize the trees. */
    for (n = 0; n < L_CODES;  n++) state.ts.dyn_ltree[n].fc.freq = 0;
    for (n = 0; n < D_CODES;  n++) state.ts.dyn_dtree[n].fc.freq = 0;
    for (n = 0; n < BL_CODES; n++) state.ts.bl_tree[n].fc.freq = 0;

    state.ts.dyn_ltree[END_BLOCK].fc.freq = 1;
    state.ts.opt_len = state.ts.static_len = 0L;
    state.ts.last_lit = state.ts.last_dist = state.ts.last_flags = 0;
    state.ts.flags = 0; state.ts.flag_bit = 1;
}

#define SMALLEST 1
/* Index within the heap array of least frequent node in the Huffman tree */


/* ===========================================================================
 * Remove the smallest element from the heap and recreate the heap with
 * one less element. Updates heap and heap_len.
 */
#define pqremove(tree, top) \
{\
    top = state.ts.heap[SMALLEST]; \
    state.ts.heap[SMALLEST] = state.ts.heap[state.ts.heap_len--]; \
    pqdownheap(state,tree, SMALLEST); \
}

/* ===========================================================================
 * Compares to subtrees, using the tree depth as tie breaker when
 * the subtrees have equal frequency. This minimizes the worst case length.
 */
#define smaller(tree, n, m) \
   (tree[n].fc.freq < tree[m].fc.freq || \
   (tree[n].fc.freq == tree[m].fc.freq && state.ts.depth[n] <= state.ts.depth[m]))

/* ===========================================================================
 * Restore the heap property by moving down the tree starting at node k,
 * exchanging a node with the smallest of its two sons if necessary, stopping
 * when the heap property is re-established (each father smaller than its
 * two sons).
 */
void pqdownheap(TState &state,ct_data *tree, int k)
{
    int v = state.ts.heap[k];
    int j = k << 1;  /* left son of k */
    int htemp;       /* required because of bug in SASC compiler */

    while (j <= state.ts.heap_len) {
        /* Set j to the smallest of the two sons: */
        if (j < state.ts.heap_len && smaller(tree, state.ts.heap[j+1], state.ts.heap[j])) j++;

        /* Exit if v is smaller than both sons */
        htemp = state.ts.heap[j];
        if (smaller(tree, v, htemp)) break;

        /* Exchange v with the smallest son */
        state.ts.heap[k] = htemp;
        k = j;

        /* And continue down the tree, setting j to the left son of k */
        j <<= 1;
    }
    state.ts.heap[k] = v;
}

/* ===========================================================================
 * Compute the optimal bit lengths for a tree and update the total bit length
 * for the current block.
 * IN assertion: the fields freq and dad are set, heap[heap_max] and
 *    above are the tree nodes sorted by increasing frequency.
 * OUT assertions: the field len is set to the optimal bit length, the
 *     array bl_count contains the frequencies for each bit length.
 *     The length opt_len is updated; static_len is also updated if stree is
 *     not null.
 */
void gen_bitlen(TState &state,tree_desc *desc)
{
    ct_data *tree  = desc->dyn_tree;
    const int *extra     = desc->extra_bits;
    int base            = desc->extra_base;
    int max_code        = desc->max_code;
    int max_length      = desc->max_length;
    ct_data *stree = desc->static_tree;
    int h;              /* heap index */
    int n, m;           /* iterate over the tree elements */
    int bits;           /* bit length */
    int xbits;          /* extra bits */
    ush f;              /* frequency */
    int overflow = 0;   /* number of elements with bit length too large */

    for (bits = 0; bits <= MAX_BITS; bits++) state.ts.bl_count[bits] = 0;

    /* In a first pass, compute the optimal bit lengths (which may
     * overflow in the case of the bit length tree).
     */
    tree[state.ts.heap[state.ts.heap_max]].dl.len = 0; /* root of the heap */

    for (h = state.ts.heap_max+1; h < HEAP_SIZE; h++) {
        n = state.ts.heap[h];
        bits = tree[tree[n].dl.dad].dl.len + 1;
        if (bits > max_length) bits = max_length, overflow++;
        tree[n].dl.len = (ush)bits;
        /* We overwrite tree[n].dl.dad which is no longer needed */

        if (n > max_code) continue; /* not a leaf node */

        state.ts.bl_count[bits]++;
        xbits = 0;
        if (n >= base) xbits = extra[n-base];
        f = tree[n].fc.freq;
        state.ts.opt_len += (ulg)f * (bits + xbits);
        if (stree) state.ts.static_len += (ulg)f * (stree[n].dl.len + xbits);
    }
    if (overflow == 0) return;

    Trace("\nbit length overflow\n");
    /* This happens for example on obj2 and pic of the Calgary corpus */

    /* Find the first bit length which could increase: */
    do {
        bits = max_length-1;
        while (state.ts.bl_count[bits] == 0) bits--;
        state.ts.bl_count[bits]--;           /* move one leaf down the tree */
        state.ts.bl_count[bits+1] += (ush)2; /* move one overflow item as its brother */
        state.ts.bl_count[max_length]--;
        /* The brother of the overflow item also moves one step up,
         * but this does not affect bl_count[max_length]
         */
        overflow -= 2;
    } while (overflow > 0);

    /* Now recompute all bit lengths, scanning in increasing frequency.
     * h is still equal to HEAP_SIZE. (It is simpler to reconstruct all
     * lengths instead of fixing only the wrong ones. This idea is taken
     * from 'ar' written by Haruhiko Okumura.)
     */
    for (bits = max_length; bits != 0; bits--) {
        n = state.ts.bl_count[bits];
        while (n != 0) {
            m = state.ts.heap[--h];
            if (m > max_code) continue;
            if (tree[m].dl.len != (ush)bits) {
                Trace("code %d bits %d->%d\n", m, tree[m].dl.len, bits);
                state.ts.opt_len += ((long)bits-(long)tree[m].dl.len)*(long)tree[m].fc.freq;
                tree[m].dl.len = (ush)bits;
            }
            n--;
        }
    }
}

/* ===========================================================================
 * Generate the codes for a given tree and bit counts (which need not be
 * optimal).
 * IN assertion: the array bl_count contains the bit length statistics for
 * the given tree and the field len is set for all tree elements.
 * OUT assertion: the field code is set for all tree elements of non
 *     zero code length.
 */
void gen_codes (TState &state, ct_data *tree, int max_code)
{
    ush next_code[MAX_BITS+1]; /* next code value for each bit length */
    ush code = 0;              /* running code value */
    int bits;                  /* bit index */
    int n;                     /* code index */

    /* The distribution counts are first used to generate the code values
     * without bit reversal.
     */
    for (bits = 1; bits <= MAX_BITS; bits++) {
        next_code[bits] = code = (ush)((code + state.ts.bl_count[bits-1]) << 1);
    }
    /* Check that the bit counts in bl_count are consistent. The last code
     * must be all ones.
     */
    Assert(state,code + state.ts.bl_count[MAX_BITS]-1 == (1<< ((ush) MAX_BITS)) - 1,
            "inconsistent bit counts");
    Trace("\ngen_codes: max_code %d ", max_code);

    for (n = 0;  n <= max_code; n++) {
        int len = tree[n].dl.len;
        if (len == 0) continue;
        /* Now reverse the bits */
        tree[n].fc.code = (ush)bi_reverse(next_code[len]++, len);

        //Tracec(tree != state.ts.static_ltree, "\nn %3d %c l %2d c %4x (%x) ", n, (isgraph(n) ? n : ' '), len, tree[n].fc.code, next_code[len]-1);
    }
}

/* ===========================================================================
 * Construct one Huffman tree and assigns the code bit strings and lengths.
 * Update the total bit length for the current block.
 * IN assertion: the field freq is set for all tree elements.
 * OUT assertions: the fields len and code are set to the optimal bit length
 *     and corresponding code. The length opt_len is updated; static_len is
 *     also updated if stree is not null. The field max_code is set.
 */
void build_tree(TState &state,tree_desc *desc)
{
    ct_data *tree   = desc->dyn_tree;
    ct_data *stree  = desc->static_tree;
    int elems            = desc->elems;
    int n, m;          /* iterate over heap elements */
    int max_code = -1; /* largest code with non zero frequency */
    int node = elems;  /* next internal node of the tree */

    /* Construct the initial heap, with least frequent element in
     * heap[SMALLEST]. The sons of heap[n] are heap[2*n] and heap[2*n+1].
     * heap[0] is not used.
     */
    state.ts.heap_len = 0, state.ts.heap_max = HEAP_SIZE;

    for (n = 0; n < elems; n++) {
        if (tree[n].fc.freq != 0) {
            state.ts.heap[++state.ts.heap_len] = max_code = n;
            state.ts.depth[n] = 0;
        } else {
            tree[n].dl.len = 0;
        }
    }

    /* The pkzip format requires that at least one distance code exists,
     * and that at least one bit should be sent even if there is only one
     * possible code. So to avoid special checks later on we force at least
     * two codes of non zero frequency.
     */
    while (state.ts.heap_len < 2) {
        int newcp = state.ts.heap[++state.ts.heap_len] = (max_code < 2 ? ++max_code : 0);
        tree[newcp].fc.freq = 1;
        state.ts.depth[newcp] = 0;
        state.ts.opt_len--; if (stree) state.ts.static_len -= stree[newcp].dl.len;
        /* new is 0 or 1 so it does not have extra bits */
    }
    desc->max_code = max_code;

    /* The elements heap[heap_len/2+1 .. heap_len] are leaves of the tree,
     * establish sub-heaps of increasing lengths:
     */
    for (n = state.ts.heap_len/2; n >= 1; n--) pqdownheap(state,tree, n);

    /* Construct the Huffman tree by repeatedly combining the least two
     * frequent nodes.
     */
    do {
        pqremove(tree, n);   /* n = node of least frequency */
        m = state.ts.heap[SMALLEST];  /* m = node of next least frequency */

        state.ts.heap[--state.ts.heap_max] = n; /* keep the nodes sorted by frequency */
        state.ts.heap[--state.ts.heap_max] = m;

        /* Create a new node father of n and m */
        tree[node].fc.freq = (ush)(tree[n].fc.freq + tree[m].fc.freq);
        state.ts.depth[node] = (uch) (Max(state.ts.depth[n], state.ts.depth[m]) + 1);
        tree[n].dl.dad = tree[m].dl.dad = (ush)node;
        /* and insert the new node in the heap */
        state.ts.heap[SMALLEST] = node++;
        pqdownheap(state,tree, SMALLEST);

    } while (state.ts.heap_len >= 2);

    state.ts.heap[--state.ts.heap_max] = state.ts.heap[SMALLEST];

    /* At this point, the fields freq and dad are set. We can now
     * generate the bit lengths.
     */
    gen_bitlen(state,(tree_desc *)desc);

    /* The field len is now set, we can generate the bit codes */
    gen_codes (state,(ct_data *)tree, max_code);
}

/* ===========================================================================
 * Scan a literal or distance tree to determine the frequencies of the codes
 * in the bit length tree. Updates opt_len to take into account the repeat
 * counts. (The contribution of the bit length codes will be added later
 * during the construction of bl_tree.)
 */
void scan_tree (TState &state,ct_data *tree, int max_code)
{
    int n;                     /* iterates over all tree elements */
    int prevlen = -1;          /* last emitted length */
    int curlen;                /* length of current code */
    int nextlen = tree[0].dl.len; /* length of next code */
    int count = 0;             /* repeat count of the current code */
    int max_count = 7;         /* max repeat count */
    int min_count = 4;         /* min repeat count */

    if (nextlen == 0) max_count = 138, min_count = 3;
    tree[max_code+1].dl.len = (ush)-1; /* guard */

    for (n = 0; n <= max_code; n++) {
        curlen = nextlen; nextlen = tree[n+1].dl.len;
        if (++count < max_count && curlen == nextlen) {
            continue;
        } else if (count < min_count) {
            state.ts.bl_tree[curlen].fc.freq = (ush)(state.ts.bl_tree[curlen].fc.freq + count);
        } else if (curlen != 0) {
            if (curlen != prevlen) state.ts.bl_tree[curlen].fc.freq++;
            state.ts.bl_tree[REP_3_6].fc.freq++;
        } else if (count <= 10) {
            state.ts.bl_tree[REPZ_3_10].fc.freq++;
        } else {
            state.ts.bl_tree[REPZ_11_138].fc.freq++;
        }
        count = 0; prevlen = curlen;
        if (nextlen == 0) {
            max_count = 138, min_count = 3;
        } else if (curlen == nextlen) {
            max_count = 6, min_count = 3;
        } else {
            max_count = 7, min_count = 4;
        }
    }
}

/* ===========================================================================
 * Send a literal or distance tree in compressed form, using the codes in
 * bl_tree.
 */
void send_tree (TState &state, ct_data *tree, int max_code)
{
    int n;                     /* iterates over all tree elements */
    int prevlen = -1;          /* last emitted length */
    int curlen;                /* length of current code */
    int nextlen = tree[0].dl.len; /* length of next code */
    int count = 0;             /* repeat count of the current code */
    int max_count = 7;         /* max repeat count */
    int min_count = 4;         /* min repeat count */

    /* tree[max_code+1].dl.len = -1; */  /* guard already set */
    if (nextlen == 0) max_count = 138, min_count = 3;

    for (n = 0; n <= max_code; n++) {
        curlen = nextlen; nextlen = tree[n+1].dl.len;
        if (++count < max_count && curlen == nextlen) {
            continue;
        } else if (count < min_count) {
            do { send_code(state, curlen, state.ts.bl_tree); } while (--count != 0);

        } else if (curlen != 0) {
            if (curlen != prevlen) {
                send_code(state, curlen, state.ts.bl_tree); count--;
            }
            Assert(state,count >= 3 && count <= 6, " 3_6?");
            send_code(state,REP_3_6, state.ts.bl_tree); send_bits(state,count-3, 2);

        } else if (count <= 10) {
            send_code(state,REPZ_3_10, state.ts.bl_tree); send_bits(state,count-3, 3);

        } else {
            send_code(state,REPZ_11_138, state.ts.bl_tree); send_bits(state,count-11, 7);
        }
        count = 0; prevlen = curlen;
        if (nextlen == 0) {
            max_count = 138, min_count = 3;
        } else if (curlen == nextlen) {
            max_count = 6, min_count = 3;
        } else {
            max_count = 7, min_count = 4;
        }
    }
}

/* ===========================================================================
 * Construct the Huffman tree for the bit lengths and return the index in
 * bl_order of the last bit length code to send.
 */
int build_bl_tree(TState &state)
{
    int max_blindex;  /* index of last bit length code of non zero freq */

    /* Determine the bit length frequencies for literal and distance trees */
    scan_tree(state,(ct_data *)state.ts.dyn_ltree, state.ts.l_desc.max_code);
    scan_tree(state,(ct_data *)state.ts.dyn_dtree, state.ts.d_desc.max_code);

    /* Build the bit length tree: */
    build_tree(state,(tree_desc *)(&state.ts.bl_desc));
    /* opt_len now includes the length of the tree representations, except
     * the lengths of the bit lengths codes and the 5+5+4 bits for the counts.
     */

    /* Determine the number of bit length codes to send. The pkzip format
     * requires that at least 4 bit length codes be sent. (appnote.txt says
     * 3 but the actual value used is 4.)
     */
    for (max_blindex = BL_CODES-1; max_blindex >= 3; max_blindex--) {
        if (state.ts.bl_tree[bl_order[max_blindex]].dl.len != 0) break;
    }
    /* Update opt_len to include the bit length tree and counts */
    state.ts.opt_len += 3*(max_blindex+1) + 5+5+4;
    Trace("\ndyn trees: dyn %ld, stat %ld", state.ts.opt_len, state.ts.static_len);

    return max_blindex;
}

/* ===========================================================================
 * Send the header for a block using dynamic Huffman trees: the counts, the
 * lengths of the bit length codes, the literal tree and the distance tree.
 * IN assertion: lcodes >= 257, dcodes >= 1, blcodes >= 4.
 */
void send_all_trees(TState &state,int lcodes, int dcodes, int blcodes)
{
    int rank;                    /* index in bl_order */

    Assert(state,lcodes >= 257 && dcodes >= 1 && blcodes >= 4, "not enough codes");
    Assert(state,lcodes <= L_CODES && dcodes <= D_CODES && blcodes <= BL_CODES,
            "too many codes");
    Trace("\nbl counts: ");
    send_bits(state,lcodes-257, 5);
    /* not +255 as stated in appnote.txt 1.93a or -256 in 2.04c */
    send_bits(state,dcodes-1,   5);
    send_bits(state,blcodes-4,  4); /* not -3 as stated in appnote.txt */
    for (rank = 0; rank < blcodes; rank++) {
        Trace("\nbl code %2d ", bl_order[rank]);
        send_bits(state,state.ts.bl_tree[bl_order[rank]].dl.len, 3);
    }    
    Trace("\nbl tree: sent %ld", state.bs.bits_sent);

    send_tree(state,(ct_data *)state.ts.dyn_ltree, lcodes-1); /* send the literal tree */
    Trace("\nlit tree: sent %ld", state.bs.bits_sent);

    send_tree(state,(ct_data *)state.ts.dyn_dtree, dcodes-1); /* send the distance tree */
    Trace("\ndist tree: sent %ld", state.bs.bits_sent);
}

/* ===========================================================================
 * Determine the best encoding for the current block: dynamic trees, static
 * trees or store, and output the encoded block to the zip file. This function
 * returns the total compressed length (in bytes) for the file so far.
 */
ulg flush_block(TState &state,char *buf, ulg stored_len, int eof)
{
    ulg opt_lenb, static_lenb; /* opt_len and static_len in bytes */
    int max_blindex;  /* index of last bit length code of non zero freq */

    state.ts.flag_buf[state.ts.last_flags] = state.ts.flags; /* Save the flags for the last 8 items */

     /* Check if the file is ascii or binary */
    if (*state.ts.file_type == (ush)UNKNOWN) set_file_type(state);

    /* Construct the literal and distance trees */
    build_tree(state,(tree_desc *)(&state.ts.l_desc));
    Trace("\nlit data: dyn %ld, stat %ld", state.ts.opt_len, state.ts.static_len);

    build_tree(state,(tree_desc *)(&state.ts.d_desc));
    Trace("\ndist data: dyn %ld, stat %ld", state.ts.opt_len, state.ts.static_len);
    /* At this point, opt_len and static_len are the total bit lengths of
     * the compressed block data, excluding the tree representations.
     */

    /* Build the bit length tree for the above two trees, and get the index
     * in bl_order of the last bit length code to send.
     */
    max_blindex = build_bl_tree(state);

    /* Determine the best encoding. Compute first the block length in bytes */
    opt_lenb = (state.ts.opt_len+3+7)>>3;
    static_lenb = (state.ts.static_len+3+7)>>3;
    state.ts.input_len += stored_len; /* for debugging only */

    Trace("\nopt %lu(%lu) stat %lu(%lu) stored %lu lit %u dist %u ",
            opt_lenb, state.ts.opt_len, static_lenb, state.ts.static_len, stored_len,
            state.ts.last_lit, state.ts.last_dist);

    if (static_lenb <= opt_lenb) opt_lenb = static_lenb;

    // Originally, zip allowed the file to be transformed from a compressed
    // into a stored file in the case where compression failed, there
    // was only one block, and it was allowed to change. I've removed this
    // possibility since the code's cleaner if no changes are allowed.
    //if (stored_len <= opt_lenb && eof && state.ts.cmpr_bytelen == 0L
    //   && state.ts.cmpr_len_bits == 0L && state.seekable)
    //{   // && state.ts.file_method != NULL
    //    // Since LIT_BUFSIZE <= 2*WSIZE, the input data must be there:
    //    Assert(state,buf!=NULL,"block vanished");
    //    copy_block(state,buf, (unsigned)stored_len, 0); // without header
    //    state.ts.cmpr_bytelen = stored_len;
    //    Assert(state,false,"unimplemented *state.ts.file_method = STORE;");
    //    //*state.ts.file_method = STORE;
    //}
    //else
    if (stored_len+4 <= opt_lenb && buf != (char*)NULL) {
                       /* 4: two words for the lengths */
        /* The test buf != NULL is only necessary if LIT_BUFSIZE > WSIZE.
         * Otherwise we can't have processed more than WSIZE input bytes since
         * the last block flush, because compression would have been
         * successful. If LIT_BUFSIZE <= WSIZE, it is never too late to
         * transform a block into a stored block.
         */
        send_bits(state,(STORED_BLOCK<<1)+eof, 3);  /* send block type */
        state.ts.cmpr_bytelen += ((state.ts.cmpr_len_bits + 3 + 7) >> 3) + stored_len + 4;
        state.ts.cmpr_len_bits = 0L;

        copy_block(state,buf, (unsigned)stored_len, 1); /* with header */
    }
    else if (static_lenb == opt_lenb) {
        send_bits(state,(STATIC_TREES<<1)+eof, 3);
        compress_block(state,(ct_data *)state.ts.static_ltree, (ct_data *)state.ts.static_dtree);
        state.ts.cmpr_len_bits += 3 + state.ts.static_len;
        state.ts.cmpr_bytelen += state.ts.cmpr_len_bits >> 3;
        state.ts.cmpr_len_bits &= 7L;
    }
    else {
        send_bits(state,(DYN_TREES<<1)+eof, 3);
        send_all_trees(state,state.ts.l_desc.max_code+1, state.ts.d_desc.max_code+1, max_blindex+1);
        compress_block(state,(ct_data *)state.ts.dyn_ltree, (ct_data *)state.ts.dyn_dtree);
        state.ts.cmpr_len_bits += 3 + state.ts.opt_len;
        state.ts.cmpr_bytelen += state.ts.cmpr_len_bits >> 3;
        state.ts.cmpr_len_bits &= 7L;
    }
    Assert(state,((state.ts.cmpr_bytelen << 3) + state.ts.cmpr_len_bits) == state.bs.bits_sent, "bad compressed size");
    init_block(state);

    if (eof) {
        // Assert(state,input_len == isize, "bad input size");
        bi_windup(state);
        state.ts.cmpr_len_bits += 7;  /* align on byte boundary */
    }
    Trace("\n");

    return state.ts.cmpr_bytelen + (state.ts.cmpr_len_bits >> 3);
}

/* ===========================================================================
 * Save the match info and tally the frequency counts. Return true if
 * the current block must be flushed.
 */
int ct_tally (TState &state,int dist, int lc)
{
    state.ts.l_buf[state.ts.last_lit++] = (uch)lc;
    if (dist == 0) {
        /* lc is the unmatched char */
        state.ts.dyn_ltree[lc].fc.freq++;
    } else {
        /* Here, lc is the match length - MIN_MATCH */
        dist--;             /* dist = match distance - 1 */
        Assert(state,(ush)dist < (ush)MAX_DIST &&
               (ush)lc <= (ush)(MAX_MATCH-MIN_MATCH) &&
               (ush)d_code(dist) < (ush)D_CODES,  "ct_tally: bad match");

        state.ts.dyn_ltree[state.ts.length_code[lc]+LITERALS+1].fc.freq++;
        state.ts.dyn_dtree[d_code(dist)].fc.freq++;

        state.ts.d_buf[state.ts.last_dist++] = (ush)dist;
        state.ts.flags |= state.ts.flag_bit;
    }
    state.ts.flag_bit <<= 1;

    /* Output the flags if they fill a byte: */
    if ((state.ts.last_lit & 7) == 0) {
        state.ts.flag_buf[state.ts.last_flags++] = state.ts.flags;
        state.ts.flags = 0, state.ts.flag_bit = 1;
    }
    /* Try to guess if it is profitable to stop the current block here */
    if (state.level > 2 && (state.ts.last_lit & 0xfff) == 0) {
        /* Compute an upper bound for the compressed length */
        ulg out_length = (ulg)state.ts.last_lit*8L;
        ulg in_length = (ulg)state.ds.strstart-state.ds.block_start;
        int dcode;
        for (dcode = 0; dcode < D_CODES; dcode++) {
            out_length += (ulg)state.ts.dyn_dtree[dcode].fc.freq*(5L+extra_dbits[dcode]);
        }
        out_length >>= 3;
        Trace("\nlast_lit %u, last_dist %u, in %ld, out ~%ld(%ld%%) ",
               state.ts.last_lit, state.ts.last_dist, in_length, out_length,
               100L - out_length*100L/in_length);
        if (state.ts.last_dist < state.ts.last_lit/2 && out_length < in_length/2) return 1;
    }
    return (state.ts.last_lit == LIT_BUFSIZE-1 || state.ts.last_dist == DIST_BUFSIZE);
    /* We avoid equality with LIT_BUFSIZE because of wraparound at 64K
     * on 16 bit machines and because stored blocks are restricted to
     * 64K-1 bytes.
     */
}

/* ===========================================================================
 * Send the block data compressed using the given Huffman trees
 */
void compress_block(TState &state,ct_data *ltree, ct_data *dtree)
{
    unsigned dist;      /* distance of matched string */
    int lc;             /* match length or unmatched char (if dist == 0) */
    unsigned lx = 0;    /* running index in l_buf */
    unsigned dx = 0;    /* running index in d_buf */
    unsigned fx = 0;    /* running index in flag_buf */
    uch flag = 0;       /* current flags */
    unsigned code;      /* the code to send */
    int extra;          /* number of extra bits to send */

    if (state.ts.last_lit != 0) do {
        if ((lx & 7) == 0) flag = state.ts.flag_buf[fx++];
        lc = state.ts.l_buf[lx++];
        if ((flag & 1) == 0) {
            send_code(state,lc, ltree); /* send a literal byte */
        } else {
            /* Here, lc is the match length - MIN_MATCH */
            code = state.ts.length_code[lc];
            send_code(state,code+LITERALS+1, ltree); /* send the length code */
            extra = extra_lbits[code];
            if (extra != 0) {
                lc -= state.ts.base_length[code];
                send_bits(state,lc, extra);        /* send the extra length bits */
            }
            dist = state.ts.d_buf[dx++];
            /* Here, dist is the match distance - 1 */
            code = d_code(dist);
            Assert(state,code < D_CODES, "bad d_code");

            send_code(state,code, dtree);       /* send the distance code */
            extra = extra_dbits[code];
            if (extra != 0) {
                dist -= state.ts.base_dist[code];
                send_bits(state,dist, extra);   /* send the extra distance bits */
            }
        } /* literal or match pair ? */
        flag >>= 1;
    } while (lx < state.ts.last_lit);

    send_code(state,END_BLOCK, ltree);
}

/* ===========================================================================
 * Set the file type to ASCII or BINARY, using a crude approximation:
 * binary if more than 20% of the bytes are <= 6 or >= 128, ascii otherwise.
 * IN assertion: the fields freq of dyn_ltree are set and the total of all
 * frequencies does not exceed 64K (to fit in an int on 16 bit machines).
 */
void set_file_type(TState &state)
{
    int n = 0;
    unsigned ascii_freq = 0;
    unsigned bin_freq = 0;
    while (n < 7)        bin_freq += state.ts.dyn_ltree[n++].fc.freq;
    while (n < 128)    ascii_freq += state.ts.dyn_ltree[n++].fc.freq;
    while (n < LITERALS) bin_freq += state.ts.dyn_ltree[n++].fc.freq;
    *state.ts.file_type = (ush)(bin_freq > (ascii_freq >> 2) ? BINARY : ASCII);
}


/* ===========================================================================
 * Initialize the bit string routines.
 */
void bi_init (TState &state,char *tgt_buf, unsigned tgt_size, int flsh_allowed)
{
    state.bs.out_buf = tgt_buf;
    state.bs.out_size = tgt_size;
    state.bs.out_offset = 0;
    state.bs.flush_flg = flsh_allowed;

    state.bs.bi_buf = 0;
    state.bs.bi_valid = 0;
    state.bs.bits_sent = 0L;
}

/* ===========================================================================
 * Send a value on a given number of bits.
 * IN assertion: length <= 16 and value fits in length bits.
 */
void send_bits(TState &state,int value, int length)
{
    Assert(state,length > 0 && length <= 15, "invalid length");
    state.bs.bits_sent += (ulg)length;
    /* If not enough room in bi_buf, use (bi_valid) bits from bi_buf and
     * (Buf_size - bi_valid) bits from value to flush the filled bi_buf,
     * then fill in the rest of (value), leaving (length - (Buf_size-bi_valid))
     * unused bits in bi_buf.
     */
    state.bs.bi_buf |= (value << state.bs.bi_valid);
    state.bs.bi_valid += length;
    if (state.bs.bi_valid > (int)Buf_size) {
        PUTSHORT(state,state.bs.bi_buf);
        state.bs.bi_valid -= Buf_size;
        state.bs.bi_buf = (unsigned)value >> (length - state.bs.bi_valid);
    }
}

/* ===========================================================================
 * Reverse the first len bits of a code, using straightforward code (a faster
 * method would use a table)
 * IN assertion: 1 <= len <= 15
 */
unsigned bi_reverse(unsigned code, int len)
{
    register unsigned res = 0;
    do {
        res |= code & 1;
        code >>= 1, res <<= 1;
    } while (--len > 0);
    return res >> 1;
}

/* ===========================================================================
 * Write out any remaining bits in an incomplete byte.
 */
void bi_windup(TState &state)
{
    if (state.bs.bi_valid > 8) {
        PUTSHORT(state,state.bs.bi_buf);
    } else if (state.bs.bi_valid > 0) {
        PUTBYTE(state,state.bs.bi_buf);
    }
    if (state.bs.flush_flg) {
        state.flush_outbuf(state.param,state.bs.out_buf, &state.bs.out_offset);
    }
    state.bs.bi_buf = 0;
    state.bs.bi_valid = 0;
    state.bs.bits_sent = (state.bs.bits_sent+7) & ~7;
}

/* ===========================================================================
 * Copy a stored block to the zip file, storing first the length and its
 * one's complement if requested.
 */
void copy_block(TState &state, char *block, unsigned len, int header)
{
    bi_windup(state);              /* align on byte boundary */

    if (header) {
        PUTSHORT(state,(ush)len);
        PUTSHORT(state,(ush)~len);
        state.bs.bits_sent += 2*16;
    }
    if (state.bs.flush_flg) {
        state.flush_outbuf(state.param,state.bs.out_buf, &state.bs.out_offset);
        state.bs.out_offset = len;
        state.flush_outbuf(state.param,block, &state.bs.out_offset);
    } else if (state.bs.out_offset + len > state.bs.out_size) {
        Assert(state,false,"output buffer too small for in-memory compression");
    } else {
        memcpy(state.bs.out_buf + state.bs.out_offset, block, len);
        state.bs.out_offset += len;
    }
    state.bs.bits_sent += (ulg)len<<3;
}








/* ===========================================================================
 *  Prototypes for functions.
 */

void fill_window  (TState &state);
ulg deflate_fast  (TState &state);

int  longest_match (TState &state,IPos cur_match);


/* ===========================================================================
 * Update a hash value with the given input byte
 * IN  assertion: all calls to to UPDATE_HASH are made with consecutive
 *    input characters, so that a running hash key can be computed from the
 *    previous key instead of complete recalculation each time.
 */
#define UPDATE_HASH(h,c) (h = (((h)<<H_SHIFT) ^ (c)) & HASH_MASK)

/* ===========================================================================
 * Insert string s in the dictionary and set match_head to the previous head
 * of the hash chain (the most recent string with same hash key). Return
 * the previous length of the hash chain.
 * IN  assertion: all calls to to INSERT_STRING are made with consecutive
 *    input characters and the first MIN_MATCH bytes of s are valid
 *    (except for the last MIN_MATCH-1 bytes of the input file).
 */
#define INSERT_STRING(s, match_head) \
   (UPDATE_HASH(state.ds.ins_h, state.ds.window[(s) + (MIN_MATCH-1)]), \
    state.ds.prev[(s) & WMASK] = match_head = state.ds.head[state.ds.ins_h], \
    state.ds.head[state.ds.ins_h] = (s))

/* ===========================================================================
 * Initialize the "longest match" routines for a new file
 *
 * IN assertion: window_size is > 0 if the input file is already read or
 *    mmap'ed in the window[] array, 0 otherwise. In the first case,
 *    window_size is sufficient to contain the whole input file plus
 *    MIN_LOOKAHEAD bytes (to avoid referencing memory beyond the end
 *    of window[] when looking for matches towards the end).
 */
void lm_init (TState &state, int pack_level, ush *flags)
{
    register unsigned j;

    Assert(state,pack_level>=1 && pack_level<=8,"bad pack level");

    /* Do not slide the window if the whole input is already in memory
     * (window_size > 0)
     */
    state.ds.sliding = 0;
    if (state.ds.window_size == 0L) {
        state.ds.sliding = 1;
        state.ds.window_size = (ulg)2L*WSIZE;
    }

    /* Initialize the hash table (avoiding 64K overflow for 16 bit systems).
     * prev[] will be initialized on the fly.
     */
    state.ds.head[HASH_SIZE-1] = NIL;
    memset((char*)state.ds.head, NIL, (unsigned)(HASH_SIZE-1)*sizeof(*state.ds.head));

    /* Set the default configuration parameters:
     */
    state.ds.max_lazy_match   = configuration_table[pack_level].max_lazy;
    state.ds.good_match       = configuration_table[pack_level].good_length;
    state.ds.nice_match       = configuration_table[pack_level].nice_length;
    state.ds.max_chain_length = configuration_table[pack_level].max_chain;
    if (pack_level <= 2) {
       *flags |= FAST;
    } else if (pack_level >= 8) {
       *flags |= SLOW;
    }
    /* ??? reduce max_chain_length for binary files */

    state.ds.strstart = 0;
    state.ds.block_start = 0L;

    j = WSIZE;
    j <<= 1; // Can read 64K in one step
    state.ds.lookahead = state.readfunc(state, (char*)state.ds.window, j);

    if (state.ds.lookahead == 0 || state.ds.lookahead == (unsigned)EOF) {
       state.ds.eofile = 1, state.ds.lookahead = 0;
       return;
    }
    state.ds.eofile = 0;
    /* Make sure that we always have enough lookahead. This is important
     * if input comes from a device such as a tty.
     */
    if (state.ds.lookahead < MIN_LOOKAHEAD) fill_window(state);

    state.ds.ins_h = 0;
    for (j=0; j<MIN_MATCH-1; j++) UPDATE_HASH(state.ds.ins_h, state.ds.window[j]);
    /* If lookahead < MIN_MATCH, ins_h is garbage, but this is
     * not important since only literal bytes will be emitted.
     */
}


/* ===========================================================================
 * Set match_start to the longest match starting at the given string and
 * return its length. Matches shorter or equal to prev_length are discarded,
 * in which case the result is equal to prev_length and match_start is
 * garbage.
 * IN assertions: cur_match is the head of the hash chain for the current
 *   string (strstart) and its distance is <= MAX_DIST, and prev_length >= 1
 */
// For 80x86 and 680x0 and ARM, an optimized version is in match.asm or
// match.S. The code is functionally equivalent, so you can use the C version
// if desired. Which I do so desire!
int longest_match(TState &state,IPos cur_match)
{
    unsigned chain_length = state.ds.max_chain_length;   /* max hash chain length */
    register uch far *scan = state.ds.window + state.ds.strstart; /* current string */
    register uch far *match;                    /* matched string */
    register int len;                           /* length of current match */
    int best_len = state.ds.prev_length;                 /* best match length so far */
    IPos limit = state.ds.strstart > (IPos)MAX_DIST ? state.ds.strstart - (IPos)MAX_DIST : NIL;
    /* Stop when cur_match becomes <= limit. To simplify the code,
     * we prevent matches with the string of window index 0.
     */

  // The code is optimized for HASH_BITS >= 8 and MAX_MATCH-2 multiple of 16.
  // It is easy to get rid of this optimization if necessary.
    Assert(state,HASH_BITS>=8 && MAX_MATCH==258,"Code too clever");



    register uch far *strend = state.ds.window + state.ds.strstart + MAX_MATCH;
    register uch scan_end1  = scan[best_len-1];
    register uch scan_end   = scan[best_len];

    /* Do not waste too much time if we already have a good match: */
    if (state.ds.prev_length >= state.ds.good_match) {
        chain_length >>= 2;
    }

    Assert(state,state.ds.strstart <= state.ds.window_size-MIN_LOOKAHEAD, "insufficient lookahead");

    do {
        Assert(state,cur_match < state.ds.strstart, "no future");
        match = state.ds.window + cur_match;

        /* Skip to next match if the match length cannot increase
         * or if the match length is less than 2:
         */
        if (match[best_len]   != scan_end  ||
            match[best_len-1] != scan_end1 ||
            *match            != *scan     ||
            *++match          != scan[1])      continue;

        /* The check at best_len-1 can be removed because it will be made
         * again later. (This heuristic is not always a win.)
         * It is not necessary to compare scan[2] and match[2] since they
         * are always equal when the other bytes match, given that
         * the hash keys are equal and that HASH_BITS >= 8.
         */
        scan += 2, match++;

        /* We check for insufficient lookahead only every 8th comparison;
         * the 256th check will be made at strstart+258.
         */
        do {
        } while (*++scan == *++match && *++scan == *++match &&
                 *++scan == *++match && *++scan == *++match &&
                 *++scan == *++match && *++scan == *++match &&
                 *++scan == *++match && *++scan == *++match &&
                 scan < strend);

        Assert(state,scan <= state.ds.window+(unsigned)(state.ds.window_size-1), "wild scan");
                          
        len = MAX_MATCH - (int)(strend - scan);
        scan = strend - MAX_MATCH;


        if (len > best_len) {
            state.ds.match_start = cur_match;
            best_len = len;
            if (len >= state.ds.nice_match) break;
            scan_end1  = scan[best_len-1];
            scan_end   = scan[best_len];
        }
    } while ((cur_match = state.ds.prev[cur_match & WMASK]) > limit
             && --chain_length != 0);

    return best_len;
}



#define check_match(state,start, match, length)
// or alternatively...
//void check_match(TState &state,IPos start, IPos match, int length)
//{ // check that the match is indeed a match
//    if (memcmp((char*)state.ds.window + match,
//                (char*)state.ds.window + start, length) != EQUAL) {
//        fprintf(stderr,
//            " start %d, match %d, length %d\n",
//            start, match, length);
//        error("invalid match");
//    }
//    if (state.verbose > 1) {
//        fprintf(stderr,"\\[%d,%d]", start-match, length);
//        do { fprintf(stdout,"%c",state.ds.window[start++]); } while (--length != 0);
//    }
//}

/* ===========================================================================
 * Fill the window when the lookahead becomes insufficient.
 * Updates strstart and lookahead, and sets eofile if end of input file.
 *
 * IN assertion: lookahead < MIN_LOOKAHEAD && strstart + lookahead > 0
 * OUT assertions: strstart <= window_size-MIN_LOOKAHEAD
 *    At least one byte has been read, or eofile is set; file reads are
 *    performed for at least two bytes (required for the translate_eol option).
 */
void fill_window(TState &state)
{
    register unsigned n, m;
    unsigned more;    /* Amount of free space at the end of the window. */

    do {
        more = (unsigned)(state.ds.window_size - (ulg)state.ds.lookahead - (ulg)state.ds.strstart);

        /* If the window is almost full and there is insufficient lookahead,
         * move the upper half to the lower one to make room in the upper half.
         */
        if (more == (unsigned)EOF) {
            /* Very unlikely, but possible on 16 bit machine if strstart == 0
             * and lookahead == 1 (input done one byte at time)
             */
            more--;

        /* For MMAP or BIG_MEM, the whole input file is already in memory so
         * we must not perform sliding. We must however call (*read_buf)() in
         * order to compute the crc, update lookahead and possibly set eofile.
         */
        } else if (state.ds.strstart >= WSIZE+MAX_DIST && state.ds.sliding) {

            /* By the IN assertion, the window is not empty so we can't confuse
             * more == 0 with more == 64K on a 16 bit machine.
             */
            memcpy((char*)state.ds.window, (char*)state.ds.window+WSIZE, (unsigned)WSIZE);
            state.ds.match_start -= WSIZE;
            state.ds.strstart    -= WSIZE; /* we now have strstart >= MAX_DIST: */

            state.ds.block_start -= (long) WSIZE;

            for (n = 0; n < HASH_SIZE; n++) {
                m = state.ds.head[n];
                state.ds.head[n] = (Pos)(m >= WSIZE ? m-WSIZE : NIL);
            }
            for (n = 0; n < WSIZE; n++) {
                m = state.ds.prev[n];
                state.ds.prev[n] = (Pos)(m >= WSIZE ? m-WSIZE : NIL);
                /* If n is not on any hash chain, prev[n] is garbage but
                 * its value will never be used.
                 */
            }
            more += WSIZE;
        }
        if (state.ds.eofile) return;

        /* If there was no sliding:
         *    strstart <= WSIZE+MAX_DIST-1 && lookahead <= MIN_LOOKAHEAD - 1 &&
         *    more == window_size - lookahead - strstart
         * => more >= window_size - (MIN_LOOKAHEAD-1 + WSIZE + MAX_DIST-1)
         * => more >= window_size - 2*WSIZE + 2
         * In the MMAP or BIG_MEM case (not yet supported in gzip),
         *   window_size == input_size + MIN_LOOKAHEAD  &&
         *   strstart + lookahead <= input_size => more >= MIN_LOOKAHEAD.
         * Otherwise, window_size == 2*WSIZE so more >= 2.
         * If there was sliding, more >= WSIZE. So in all cases, more >= 2.
         */
        Assert(state,more >= 2, "more < 2");

        n = state.readfunc(state, (char*)state.ds.window+state.ds.strstart+state.ds.lookahead, more);

        if (n == 0 || n == (unsigned)EOF) {
            state.ds.eofile = 1;
        } else {
            state.ds.lookahead += n;
        }
    } while (state.ds.lookahead < MIN_LOOKAHEAD && !state.ds.eofile);
}

/* ===========================================================================
 * Flush the current block, with given end-of-file flag.
 * IN assertion: strstart is set to the end of the current match.
 */
#define FLUSH_BLOCK(state,eof) \
   flush_block(state,state.ds.block_start >= 0L ? (char*)&state.ds.window[(unsigned)state.ds.block_start] : \
                (char*)NULL, (long)state.ds.strstart - state.ds.block_start, (eof))

/* ===========================================================================
 * Processes a new input file and return its compressed length. This
 * function does not perform lazy evaluation of matches and inserts
 * new strings in the dictionary only for unmatched strings or for short
 * matches. It is used only for the fast compression options.
 */
ulg deflate_fast(TState &state)
{
    IPos hash_head = NIL;       /* head of the hash chain */
    int flush;                  /* set if current block must be flushed */
    unsigned match_length = 0;  /* length of best match */

    state.ds.prev_length = MIN_MATCH-1;
    while (state.ds.lookahead != 0) {
        /* Insert the string window[strstart .. strstart+2] in the
         * dictionary, and set hash_head to the head of the hash chain:
         */
        if (state.ds.lookahead >= MIN_MATCH)
        INSERT_STRING(state.ds.strstart, hash_head);

        /* Find the longest match, discarding those <= prev_length.
         * At this point we have always match_length < MIN_MATCH
         */
        if (hash_head != NIL && state.ds.strstart - hash_head <= MAX_DIST) {
            /* To simplify the code, we prevent matches with the string
             * of window index 0 (in particular we have to avoid a match
             * of the string with itself at the start of the input file).
             */
            /* Do not look for matches beyond the end of the input.
             * This is necessary to make deflate deterministic.
             */
            if ((unsigned)state.ds.nice_match > state.ds.lookahead) state.ds.nice_match = (int)state.ds.lookahead;
            match_length = longest_match (state,hash_head);
            /* longest_match() sets match_start */
            if (match_length > state.ds.lookahead) match_length = state.ds.lookahead;
        }
        if (match_length >= MIN_MATCH) {
            check_match(state,state.ds.strstart, state.ds.match_start, match_length);

            flush = ct_tally(state,state.ds.strstart-state.ds.match_start, match_length - MIN_MATCH);

            state.ds.lookahead -= match_length;

            /* Insert new strings in the hash table only if the match length
             * is not too large. This saves time but degrades compression.
             */
            if (match_length <= state.ds.max_insert_length
                && state.ds.lookahead >= MIN_MATCH) {
                match_length--; /* string at strstart already in hash table */
                do {
                    state.ds.strstart++;
                    INSERT_STRING(state.ds.strstart, hash_head);
                    /* strstart never exceeds WSIZE-MAX_MATCH, so there are
                     * always MIN_MATCH bytes ahead.
                     */
                } while (--match_length != 0);
                state.ds.strstart++;
            } else {
                state.ds.strstart += match_length;
                match_length = 0;
                state.ds.ins_h = state.ds.window[state.ds.strstart];
                UPDATE_HASH(state.ds.ins_h, state.ds.window[state.ds.strstart+1]);
                Assert(state,MIN_MATCH==3,"Call UPDATE_HASH() MIN_MATCH-3 more times");
            }
        } else {
            /* No match, output a literal byte */
            flush = ct_tally (state,0, state.ds.window[state.ds.strstart]);
            state.ds.lookahead--;
            state.ds.strstart++;
        }
        if (flush) FLUSH_BLOCK(state,0), state.ds.block_start = state.ds.strstart;

        /* Make sure that we always have enough lookahead, except
         * at the end of the input file. We need MAX_MATCH bytes
         * for the next match, plus MIN_MATCH bytes to insert the
         * string following the next match.
         */
        if (state.ds.lookahead < MIN_LOOKAHEAD) fill_window(state);
    }
    return FLUSH_BLOCK(state,1); /* eof */
}

/* ===========================================================================
 * Same as above, but achieves better compression. We use a lazy
 * evaluation for matches: a match is finally adopted only if there is
 * no better match at the next window position.
 */
ulg deflate(TState &state)
{
    IPos hash_head = NIL;       /* head of hash chain */
    IPos prev_match;            /* previous match */
    int flush;                  /* set if current block must be flushed */
    int match_available = 0;    /* set if previous match exists */
    register unsigned match_length = MIN_MATCH-1; /* length of best match */

    if (state.level <= 3) return deflate_fast(state); /* optimized for speed */

    /* Process the input block. */
    while (state.ds.lookahead != 0) {
        /* Insert the string window[strstart .. strstart+2] in the
         * dictionary, and set hash_head to the head of the hash chain:
         */
        if (state.ds.lookahead >= MIN_MATCH)
        INSERT_STRING(state.ds.strstart, hash_head);

        /* Find the longest match, discarding those <= prev_length.
         */
        state.ds.prev_length = match_length, prev_match = state.ds.match_start;
        match_length = MIN_MATCH-1;

        if (hash_head != NIL && state.ds.prev_length < state.ds.max_lazy_match &&
            state.ds.strstart - hash_head <= MAX_DIST) {
            /* To simplify the code, we prevent matches with the string
             * of window index 0 (in particular we have to avoid a match
             * of the string with itself at the start of the input file).
             */
            /* Do not look for matches beyond the end of the input.
             * This is necessary to make deflate deterministic.
             */
            if ((unsigned)state.ds.nice_match > state.ds.lookahead) state.ds.nice_match = (int)state.ds.lookahead;
            match_length = longest_match (state,hash_head);
            /* longest_match() sets match_start */
            if (match_length > state.ds.lookahead) match_length = state.ds.lookahead;

            /* Ignore a length 3 match if it is too distant: */
            if (match_length == MIN_MATCH && state.ds.strstart-state.ds.match_start > TOO_FAR){
                /* If prev_match is also MIN_MATCH, match_start is garbage
                 * but we will ignore the current match anyway.
                 */
                match_length = MIN_MATCH-1;
            }
        }
        /* If there was a match at the previous step and the current
         * match is not better, output the previous match:
         */
        if (state.ds.prev_length >= MIN_MATCH && match_length <= state.ds.prev_length) {
            unsigned max_insert = state.ds.strstart + state.ds.lookahead - MIN_MATCH;
            check_match(state,state.ds.strstart-1, prev_match, state.ds.prev_length);
            flush = ct_tally(state,state.ds.strstart-1-prev_match, state.ds.prev_length - MIN_MATCH);

            /* Insert in hash table all strings up to the end of the match.
             * strstart-1 and strstart are already inserted.
             */
            state.ds.lookahead -= state.ds.prev_length-1;
            state.ds.prev_length -= 2;
            do {
                if (++state.ds.strstart <= max_insert) {
                    INSERT_STRING(state.ds.strstart, hash_head);
                    /* strstart never exceeds WSIZE-MAX_MATCH, so there are
                     * always MIN_MATCH bytes ahead.
                     */
                }
            } while (--state.ds.prev_length != 0);
            state.ds.strstart++;
            match_available = 0;
            match_length = MIN_MATCH-1;

            if (flush) FLUSH_BLOCK(state,0), state.ds.block_start = state.ds.strstart;

        } else if (match_available) {
            /* If there was no match at the previous position, output a
             * single literal. If there was a match but the current match
             * is longer, truncate the previous match to a single literal.
             */
            if (ct_tally (state,0, state.ds.window[state.ds.strstart-1])) {
                FLUSH_BLOCK(state,0), state.ds.block_start = state.ds.strstart;
            }
            state.ds.strstart++;
            state.ds.lookahead--;
        } else {
            /* There is no previous match to compare with, wait for
             * the next step to decide.
             */
            match_available = 1;
            state.ds.strstart++;
            state.ds.lookahead--;
        }
//        Assert(state,strstart <= isize && lookahead <= isize, "a bit too far");

        /* Make sure that we always have enough lookahead, except
         * at the end of the input file. We need MAX_MATCH bytes
         * for the next match, plus MIN_MATCH bytes to insert the
         * string following the next match.
         */
        if (state.ds.lookahead < MIN_LOOKAHEAD) fill_window(state);
    }
    if (match_available) ct_tally (state,0, state.ds.window[state.ds.strstart-1]);

    return FLUSH_BLOCK(state,1); /* eof */
}












int putlocal(struct zlist far *z, WRITEFUNC wfunc,void *param)
{ // Write a local header described by *z to file *f.  Return a ZE_ error code.
  PUTLG(LOCSIG, f);
  PUTSH(z->ver, f);
  PUTSH(z->lflg, f);
  PUTSH(z->how, f);
  PUTLG(z->tim, f);
  PUTLG(z->crc, f);
  PUTLG(z->siz, f);
  PUTLG(z->len, f);
  PUTSH(z->nam, f);
  PUTSH(z->ext, f);
  size_t res = (size_t)wfunc(param, z->iname, (unsigned int)z->nam);
  if (res!=z->nam) return ZE_TEMP;
  if (z->ext)
  { res = (size_t)wfunc(param, z->extra, (unsigned int)z->ext);
    if (res!=z->ext) return ZE_TEMP;
  }
  return ZE_OK;
}

int putextended(struct zlist far *z, WRITEFUNC wfunc, void *param)
{ // Write an extended local header described by *z to file *f. Returns a ZE_ code
  PUTLG(EXTLOCSIG, f);
  PUTLG(z->crc, f);
  PUTLG(z->siz, f);
  PUTLG(z->len, f);
  return ZE_OK;
}

int putcentral(struct zlist far *z, WRITEFUNC wfunc, void *param)
{ // Write a central header entry of *z to file *f. Returns a ZE_ code.
  PUTLG(CENSIG, f);
  PUTSH(z->vem, f);
  PUTSH(z->ver, f);
  PUTSH(z->flg, f);
  PUTSH(z->how, f);
  PUTLG(z->tim, f);
  PUTLG(z->crc, f);
  PUTLG(z->siz, f);
  PUTLG(z->len, f);
  PUTSH(z->nam, f);
  PUTSH(z->cext, f);
  PUTSH(z->com, f);
  PUTSH(z->dsk, f);
  PUTSH(z->att, f);
  PUTLG(z->atx, f);
  PUTLG(z->off, f);
  if ((size_t)wfunc(param, z->iname, (unsigned int)z->nam) != z->nam ||
      (z->cext && (size_t)wfunc(param, z->cextra, (unsigned int)z->cext) != z->cext) ||
      (z->com && (size_t)wfunc(param, z->comment, (unsigned int)z->com) != z->com))
    return ZE_TEMP;
  return ZE_OK;
}


int putend(int n, ulg s, ulg c, extent m, char *z, WRITEFUNC wfunc, void *param)
{ // write the end of the central-directory-data to file *f.
  PUTLG(ENDSIG, f);
  PUTSH(0, f);
  PUTSH(0, f);
  PUTSH(n, f);
  PUTSH(n, f);
  PUTLG(s, f);
  PUTLG(c, f);
  PUTSH(m, f);
  // Write the comment, if any
  if (m && wfunc(param, z, (unsigned int)m) != m) return ZE_TEMP;
  return ZE_OK;
}






const ulg crc_table[256] = {
  0x00000000L, 0x77073096L, 0xee0e612cL, 0x990951baL, 0x076dc419L,
  0x706af48fL, 0xe963a535L, 0x9e6495a3L, 0x0edb8832L, 0x79dcb8a4L,
  0xe0d5e91eL, 0x97d2d988L, 0x09b64c2bL, 0x7eb17cbdL, 0xe7b82d07L,
  0x90bf1d91L, 0x1db71064L, 0x6ab020f2L, 0xf3b97148L, 0x84be41deL,
  0x1adad47dL, 0x6ddde4ebL, 0xf4d4b551L, 0x83d385c7L, 0x136c9856L,
  0x646ba8c0L, 0xfd62f97aL, 0x8a65c9ecL, 0x14015c4fL, 0x63066cd9L,
  0xfa0f3d63L, 0x8d080df5L, 0x3b6e20c8L, 0x4c69105eL, 0xd56041e4L,
  0xa2677172L, 0x3c03e4d1L, 0x4b04d447L, 0xd20d85fdL, 0xa50ab56bL,
  0x35b5a8faL, 0x42b2986cL, 0xdbbbc9d6L, 0xacbcf940L, 0x32d86ce3L,
  0x45df5c75L, 0xdcd60dcfL, 0xabd13d59L, 0x26d930acL, 0x51de003aL,
  0xc8d75180L, 0xbfd06116L, 0x21b4f4b5L, 0x56b3c423L, 0xcfba9599L,
  0xb8bda50fL, 0x2802b89eL, 0x5f058808L, 0xc60cd9b2L, 0xb10be924L,
  0x2f6f7c87L, 0x58684c11L, 0xc1611dabL, 0xb6662d3dL, 0x76dc4190L,
  0x01db7106L, 0x98d220bcL, 0xefd5102aL, 0x71b18589L, 0x06b6b51fL,
  0x9fbfe4a5L, 0xe8b8d433L, 0x7807c9a2L, 0x0f00f934L, 0x9609a88eL,
  0xe10e9818L, 0x7f6a0dbbL, 0x086d3d2dL, 0x91646c97L, 0xe6635c01L,
  0x6b6b51f4L, 0x1c6c6162L, 0x856530d8L, 0xf262004eL, 0x6c0695edL,
  0x1b01a57bL, 0x8208f4c1L, 0xf50fc457L, 0x65b0d9c6L, 0x12b7e950L,
  0x8bbeb8eaL, 0xfcb9887cL, 0x62dd1ddfL, 0x15da2d49L, 0x8cd37cf3L,
  0xfbd44c65L, 0x4db26158L, 0x3ab551ceL, 0xa3bc0074L, 0xd4bb30e2L,
  0x4adfa541L, 0x3dd895d7L, 0xa4d1c46dL, 0xd3d6f4fbL, 0x4369e96aL,
  0x346ed9fcL, 0xad678846L, 0xda60b8d0L, 0x44042d73L, 0x33031de5L,
  0xaa0a4c5fL, 0xdd0d7cc9L, 0x5005713cL, 0x270241aaL, 0xbe0b1010L,
  0xc90c2086L, 0x5768b525L, 0x206f85b3L, 0xb966d409L, 0xce61e49fL,
  0x5edef90eL, 0x29d9c998L, 0xb0d09822L, 0xc7d7a8b4L, 0x59b33d17L,
  0x2eb40d81L, 0xb7bd5c3bL, 0xc0ba6cadL, 0xedb88320L, 0x9abfb3b6L,
  0x03b6e20cL, 0x74b1d29aL, 0xead54739L, 0x9dd277afL, 0x04db2615L,
  0x73dc1683L, 0xe3630b12L, 0x94643b84L, 0x0d6d6a3eL, 0x7a6a5aa8L,
  0xe40ecf0bL, 0x9309ff9dL, 0x0a00ae27L, 0x7d079eb1L, 0xf00f9344L,
  0x8708a3d2L, 0x1e01f268L, 0x6906c2feL, 0xf762575dL, 0x806567cbL,
  0x196c3671L, 0x6e6b06e7L, 0xfed41b76L, 0x89d32be0L, 0x10da7a5aL,
  0x67dd4accL, 0xf9b9df6fL, 0x8ebeeff9L, 0x17b7be43L, 0x60b08ed5L,
  0xd6d6a3e8L, 0xa1d1937eL, 0x38d8c2c4L, 0x4fdff252L, 0xd1bb67f1L,
  0xa6bc5767L, 0x3fb506ddL, 0x48b2364bL, 0xd80d2bdaL, 0xaf0a1b4cL,
  0x36034af6L, 0x41047a60L, 0xdf60efc3L, 0xa867df55L, 0x316e8eefL,
  0x4669be79L, 0xcb61b38cL, 0xbc66831aL, 0x256fd2a0L, 0x5268e236L,
  0xcc0c7795L, 0xbb0b4703L, 0x220216b9L, 0x5505262fL, 0xc5ba3bbeL,
  0xb2bd0b28L, 0x2bb45a92L, 0x5cb36a04L, 0xc2d7ffa7L, 0xb5d0cf31L,
  0x2cd99e8bL, 0x5bdeae1dL, 0x9b64c2b0L, 0xec63f226L, 0x756aa39cL,
  0x026d930aL, 0x9c0906a9L, 0xeb0e363fL, 0x72076785L, 0x05005713L,
  0x95bf4a82L, 0xe2b87a14L, 0x7bb12baeL, 0x0cb61b38L, 0x92d28e9bL,
  0xe5d5be0dL, 0x7cdcefb7L, 0x0bdbdf21L, 0x86d3d2d4L, 0xf1d4e242L,
  0x68ddb3f8L, 0x1fda836eL, 0x81be16cdL, 0xf6b9265bL, 0x6fb077e1L,
  0x18b74777L, 0x88085ae6L, 0xff0f6a70L, 0x66063bcaL, 0x11010b5cL,
  0x8f659effL, 0xf862ae69L, 0x616bffd3L, 0x166ccf45L, 0xa00ae278L,
  0xd70dd2eeL, 0x4e048354L, 0x3903b3c2L, 0xa7672661L, 0xd06016f7L,
  0x4969474dL, 0x3e6e77dbL, 0xaed16a4aL, 0xd9d65adcL, 0x40df0b66L,
  0x37d83bf0L, 0xa9bcae53L, 0xdebb9ec5L, 0x47b2cf7fL, 0x30b5ffe9L,
  0xbdbdf21cL, 0xcabac28aL, 0x53b39330L, 0x24b4a3a6L, 0xbad03605L,
  0xcdd70693L, 0x54de5729L, 0x23d967bfL, 0xb3667a2eL, 0xc4614ab8L,
  0x5d681b02L, 0x2a6f2b94L, 0xb40bbe37L, 0xc30c8ea1L, 0x5a05df1bL,
  0x2d02ef8dL
};

#define CRC32(c, b) (crc_table[((int)(c) ^ (b)) & 0xff] ^ ((c) >> 8))
#define DO1(buf)  crc = CRC32(crc, *buf++)
#define DO2(buf)  DO1(buf); DO1(buf)
#define DO4(buf)  DO2(buf); DO2(buf)
#define DO8(buf)  DO4(buf); DO4(buf)

ulg crc32(ulg crc, const uch *buf, extent len)
{ if (buf==NULL) return 0L;
  crc = crc ^ 0xffffffffL;
  while (len >= 8) {DO8(buf); len -= 8;}
  if (len) do {DO1(buf);} while (--len);
  return crc ^ 0xffffffffL;  // (instead of ~c for 64-bit machines)
}


void update_keys(unsigned long *keys, char c)
{ keys[0] = CRC32(keys[0],c);
  keys[1] += keys[0] & 0xFF;
  keys[1] = keys[1]*134775813L +1;
  keys[2] = CRC32(keys[2], keys[1] >> 24);
}
char decrypt_byte(unsigned long *keys)
{ unsigned temp = ((unsigned)keys[2] & 0xffff) | 2;
  return (char)(((temp * (temp ^ 1)) >> 8) & 0xff);
}
char zencode(unsigned long *keys, char c)
{ int t=decrypt_byte(keys);
  update_keys(keys,c);
  return (char)(t^c);
}







bool HasZipSuffix(const TCHAR *fn)
{ const TCHAR *ext = fn+_tcslen(fn);
  while (ext>fn && *ext!='.') ext--;
  if (ext==fn && *ext!='.') return false;
  if (_tcsicmp(ext,_T(".Z"))==0) return true;
  if (_tcsicmp(ext,_T(".zip"))==0) return true;
  if (_tcsicmp(ext,_T(".zoo"))==0) return true;
  if (_tcsicmp(ext,_T(".arc"))==0) return true;
  if (_tcsicmp(ext,_T(".lzh"))==0) return true;
  if (_tcsicmp(ext,_T(".arj"))==0) return true;
  if (_tcsicmp(ext,_T(".gz"))==0) return true;
  if (_tcsicmp(ext,_T(".tgz"))==0) return true;
  return false;
}


lutime_t filetime2timet(const FILETIME ft)
{ __int64 i = *(__int64*)&ft;
  return (lutime_t)((i-116444736000000000)/10000000);
}

void filetime2dosdatetime(const FILETIME ft, WORD *dosdate,WORD *dostime)
{ // date: bits 0-4 are day of month 1-31. Bits 5-8 are month 1..12. Bits 9-15 are year-1980
  // time: bits 0-4 are seconds/2, bits 5-10 are minute 0..59. Bits 11-15 are hour 0..23
  SYSTEMTIME st; FileTimeToSystemTime(&ft,&st); 
  *dosdate = (WORD)(((st.wYear-1980)&0x7f) << 9);
  *dosdate |= (WORD)((st.wMonth&0xf) << 5);
  *dosdate |= (WORD)((st.wDay&0x1f));
  *dostime = (WORD)((st.wHour&0x1f) << 11);
  *dostime |= (WORD)((st.wMinute&0x3f) << 5);
  *dostime |= (WORD)((st.wSecond*2)&0x1f);
}


ZRESULT GetFileInfo(HANDLE hf, ulg *attr, long *size, iztimes *times, ulg *timestamp)
{ // The handle must be a handle to a file
  // The date and time is returned in a long with the date most significant to allow
  // unsigned integer comparison of absolute times. The attributes have two
  // high bytes unix attr, and two low bytes a mapping of that to DOS attr.
  //struct stat s; int res=stat(fn,&s); if (res!=0) return false;
  // translate windows file attributes into zip ones.
  BY_HANDLE_FILE_INFORMATION bhi; BOOL res=GetFileInformationByHandle(hf,&bhi);
  if (!res) return ZR_NOFILE;
  DWORD fa=bhi.dwFileAttributes; ulg a=0;
  // Zip uses the lower word for its interpretation of windows stuff
  if (fa&FILE_ATTRIBUTE_READONLY) a|=0x01;
  if (fa&FILE_ATTRIBUTE_HIDDEN)   a|=0x02;
  if (fa&FILE_ATTRIBUTE_SYSTEM)   a|=0x04;
  if (fa&FILE_ATTRIBUTE_DIRECTORY)a|=0x10;
  if (fa&FILE_ATTRIBUTE_ARCHIVE)  a|=0x20;
  // It uses the upper word for standard unix attr, which we manually construct
  if (fa&FILE_ATTRIBUTE_DIRECTORY)a|=0x40000000;  // directory
  else a|=0x80000000;  // normal file
  a|=0x01000000;      // readable
  if (fa&FILE_ATTRIBUTE_READONLY) {} else a|=0x00800000; // writeable
  // now just a small heuristic to check if it's an executable:
  DWORD red, hsize=GetFileSize(hf,NULL); if (hsize>40)
  { SetFilePointer(hf,0,NULL,FILE_BEGIN); unsigned short magic; ReadFile(hf,&magic,sizeof(magic),&red,NULL);
    SetFilePointer(hf,36,NULL,FILE_BEGIN); unsigned long hpos;  ReadFile(hf,&hpos,sizeof(hpos),&red,NULL);
    if (magic==0x54AD && hsize>hpos+4+20+28)
    { SetFilePointer(hf,hpos,NULL,FILE_BEGIN); unsigned long signature; ReadFile(hf,&signature,sizeof(signature),&red,NULL);
      if (signature==IMAGE_DOS_SIGNATURE || signature==IMAGE_OS2_SIGNATURE
         || signature==IMAGE_OS2_SIGNATURE_LE || signature==IMAGE_NT_SIGNATURE)
      { a |= 0x00400000; // executable
      }
    }
  }
  //
  if (attr!=NULL) *attr = a;
  if (size!=NULL) *size = hsize;
  if (times!=NULL)
  { // lutime_t is 32bit number of seconds elapsed since 0:0:0GMT, Jan1, 1970.
    // but FILETIME is 64bit number of 100-nanosecs since Jan1, 1601
    times->atime = filetime2timet(bhi.ftLastAccessTime);
    times->mtime = filetime2timet(bhi.ftLastWriteTime);
    times->ctime = filetime2timet(bhi.ftCreationTime);
  }
  if (timestamp!=NULL)
  { WORD dosdate,dostime;
    filetime2dosdatetime(bhi.ftLastWriteTime,&dosdate,&dostime);
    *timestamp = (WORD)dostime | (((DWORD)dosdate)<<16);
  }
  return ZR_OK;
}








class TZip
{ public:
  TZip(const char *pwd) : hfout(0),mustclosehfout(false),hmapout(0),zfis(0),obuf(0),hfin(0),writ(0),oerr(false),hasputcen(false),ooffset(0),encwriting(false),encbuf(0),password(0), state(0) {if (pwd!=0 && *pwd!=0) {password=new char[strlen(pwd)+1]; strcpy(password,pwd);}}
  ~TZip() {if (state!=0) delete state; state=0; if (encbuf!=0) delete[] encbuf; encbuf=0; if (password!=0) delete[] password; password=0;}

  // These variables say about the file we're writing into
  // We can write to pipe, file-by-handle, file-by-name, memory-to-memmapfile
  char *password;           // keep a copy of the password
  HANDLE hfout;             // if valid, we'll write here (for files or pipes)
  bool mustclosehfout;      // if true, we are responsible for closing hfout
  HANDLE hmapout;           // otherwise, we'll write here (for memmap)
  unsigned ooffset;         // for hfout, this is where the pointer was initially
  ZRESULT oerr;             // did a write operation give rise to an error?
  unsigned writ;            // how far have we written. This is maintained by Add, not write(), to avoid confusion over seeks
  bool ocanseek;            // can we seek?
  char *obuf;               // this is where we've locked mmap to view.
  unsigned int opos;        // current pos in the mmap
  unsigned int mapsize;     // the size of the map we created
  bool hasputcen;           // have we yet placed the central directory?
  bool encwriting;          // if true, then we'll encrypt stuff using 'keys' before we write it to disk
  unsigned long keys[3];    // keys are initialised inside Add()
  char *encbuf;             // if encrypting, then this is a temporary workspace for encrypting the data
  unsigned int encbufsize;  // (to be used and resized inside write(), and deleted in the destructor)
  //
  TZipFileInfo *zfis;       // each file gets added onto this list, for writing the table at the end
  TState *state;            // we use just one state object per zip, because it's big (500k)

  ZRESULT Create(void *z,unsigned int len,DWORD flags);
  static unsigned sflush(void *param,const char *buf, unsigned *size);
  static unsigned swrite(void *param,const char *buf, unsigned size);
  unsigned int write(const char *buf,unsigned int size);
  bool oseek(unsigned int pos);
  ZRESULT GetMemory(void **pbuf, unsigned long *plen);
  ZRESULT Close();

  // some variables to do with the file currently being read:
  // I haven't done it object-orientedly here, just put them all
  // together, since OO didn't seem to make the design any clearer.
  ulg attr; iztimes times; ulg timestamp;  // all open_* methods set these
  bool iseekable; long isize,ired;         // size is not set until close() on pips
  ulg crc;                                 // crc is not set until close(). iwrit is cumulative
  HANDLE hfin; bool selfclosehf;           // for input files and pipes
  const char *bufin; unsigned int lenin,posin; // for memory
  // and a variable for what we've done with the input: (i.e. compressed it!)
  ulg csize;                               // compressed size, set by the compression routines
  // and this is used by some of the compression routines
  char buf[16384];


  ZRESULT open_file(const TCHAR *fn);
  ZRESULT open_handle(HANDLE hf,unsigned int len);
  ZRESULT open_mem(void *src,unsigned int len);
  ZRESULT open_dir();
  static unsigned sread(TState &s,char *buf,unsigned size);
  unsigned read(char *buf, unsigned size);
  ZRESULT iclose();

  ZRESULT ideflate(TZipFileInfo *zfi);
  ZRESULT istore();

  ZRESULT Add(const TCHAR *odstzn, void *src,unsigned int len, DWORD flags);
  ZRESULT AddCentral();

};



ZRESULT TZip::Create(void *z,unsigned int len,DWORD flags)
{ if (hfout!=0 || hmapout!=0 || obuf!=0 || writ!=0 || oerr!=ZR_OK || hasputcen) return ZR_NOTINITED;
  //
  if (flags==ZIP_HANDLE)
  { HANDLE hf = (HANDLE)z;
    hfout=hf; mustclosehfout=false;
#ifdef DuplicateHandle
    BOOL res = DuplicateHandle(GetCurrentProcess(),hf,GetCurrentProcess(),&hfout,0,FALSE,DUPLICATE_SAME_ACCESS);
    if (res) mustclosehandle=true;
#endif
    // now we have hfout. Either we duplicated the handle and we close it ourselves
    // (while the caller closes h themselves), or we couldn't duplicate it.
    DWORD res = SetFilePointer(hfout,0,0,FILE_CURRENT);
    ocanseek = (res!=0xFFFFFFFF);
    if (ocanseek) ooffset=res; else ooffset=0;
    return ZR_OK;
  }
  else if (flags==ZIP_FILENAME)
  { const TCHAR *fn = (const TCHAR*)z;
    hfout = CreateFile(fn,GENERIC_WRITE,0,NULL,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,NULL);
    if (hfout==INVALID_HANDLE_VALUE) {hfout=0; return ZR_NOFILE;}
    ocanseek=true;
    ooffset=0;
    mustclosehfout=true;
    return ZR_OK;
  }
  else if (flags==ZIP_MEMORY)
  { unsigned int size = len;
    if (size==0) return ZR_MEMSIZE;
    if (z!=0) obuf=(char*)z;
    else
    { hmapout = CreateFileMapping(INVALID_HANDLE_VALUE,NULL,PAGE_READWRITE,0,size,NULL);
      if (hmapout==NULL) return ZR_NOALLOC;
      obuf = (char*)MapViewOfFile(hmapout,FILE_MAP_ALL_ACCESS,0,0,size);
      if (obuf==0) {CloseHandle(hmapout); hmapout=0; return ZR_NOALLOC;}
    }
    ocanseek=true;
    opos=0; mapsize=size;
    return ZR_OK;
  }
  else return ZR_ARGS;
}

unsigned TZip::sflush(void *param,const char *buf, unsigned *size)
{ // static
  if (*size==0) return 0;
  TZip *zip = (TZip*)param;
  unsigned int writ = zip->write(buf,*size);
  if (writ!=0) *size=0;
  return writ;
}
unsigned TZip::swrite(void *param,const char *buf, unsigned size)
{ // static
  if (size==0) return 0;
  TZip *zip=(TZip*)param; return zip->write(buf,size);
}
unsigned int TZip::write(const char *buf,unsigned int size)
{ const char *srcbuf=buf;
  if (encwriting)
  { if (encbuf!=0 && encbufsize<size) {delete[] encbuf; encbuf=0;}
    if (encbuf==0) {encbuf=new char[size*2]; encbufsize=size;}
    memcpy(encbuf,buf,size);
    for (unsigned int i=0; i<size; i++) encbuf[i]=zencode(keys,encbuf[i]);
    srcbuf=encbuf;
  }
  if (obuf!=0)
  { if (opos+size>=mapsize) {oerr=ZR_MEMSIZE; return 0;}
    memcpy(obuf+opos, srcbuf, size);
    opos+=size;
    return size;
  }
  else if (hfout!=0)
  { DWORD writ; WriteFile(hfout,srcbuf,size,&writ,NULL);
    return writ;
  }
  oerr=ZR_NOTINITED; return 0;
}

bool TZip::oseek(unsigned int pos)
{ if (!ocanseek) {oerr=ZR_SEEK; return false;}
  if (obuf!=0)
  { if (pos>=mapsize) {oerr=ZR_MEMSIZE; return false;}
    opos=pos;
    return true;
  }
  else if (hfout!=0)
  { SetFilePointer(hfout,pos+ooffset,NULL,FILE_BEGIN);
    return true;
  }
  oerr=ZR_NOTINITED; return 0;
}

ZRESULT TZip::GetMemory(void **pbuf, unsigned long *plen)
{ // When the user calls GetMemory, they're presumably at the end
  // of all their adding. In any case, we have to add the central
  // directory now, otherwise the memory we tell them won't be complete.
  if (!hasputcen) AddCentral(); hasputcen=true;
  if (pbuf!=NULL) *pbuf=(void*)obuf;
  if (plen!=NULL) *plen=writ;
  if (obuf==NULL) return ZR_NOTMMAP;
  return ZR_OK;
}

ZRESULT TZip::Close()
{ // if the directory hadn't already been added through a call to GetMemory,
  // then we do it now
  ZRESULT res=ZR_OK; if (!hasputcen) res=AddCentral(); hasputcen=true;
  if (obuf!=0 && hmapout!=0) UnmapViewOfFile(obuf); obuf=0;
  if (hmapout!=0) CloseHandle(hmapout); hmapout=0;
  if (hfout!=0 && mustclosehfout) CloseHandle(hfout); hfout=0; mustclosehfout=false;
  return res;
}




ZRESULT TZip::open_file(const TCHAR *fn)
{ hfin=0; bufin=0; selfclosehf=false; crc=CRCVAL_INITIAL; isize=0; csize=0; ired=0;
  if (fn==0) return ZR_ARGS;
  HANDLE hf = CreateFile(fn,GENERIC_READ,FILE_SHARE_READ,NULL,OPEN_EXISTING,0,NULL);
  if (hf==INVALID_HANDLE_VALUE) return ZR_NOFILE;
  ZRESULT res = open_handle(hf,0);
  if (res!=ZR_OK) {CloseHandle(hf); return res;}
  selfclosehf=true;
  return ZR_OK;
}
ZRESULT TZip::open_handle(HANDLE hf,unsigned int len)
{ hfin=0; bufin=0; selfclosehf=false; crc=CRCVAL_INITIAL; isize=0; csize=0; ired=0;
  if (hf==0 || hf==INVALID_HANDLE_VALUE) return ZR_ARGS;
  DWORD res = SetFilePointer(hfout,0,0,FILE_CURRENT);
  if (res!=0xFFFFFFFF)
  { ZRESULT res = GetFileInfo(hf,&attr,&isize,&times,&timestamp);
    if (res!=ZR_OK) return res;
    SetFilePointer(hf,0,NULL,FILE_BEGIN); // because GetFileInfo will have screwed it up
    iseekable=true; hfin=hf;
    return ZR_OK;
  }
  else
  { attr= 0x80000000;      // just a normal file
    isize = -1;            // can't know size until at the end
    if (len!=0) isize=len; // unless we were told explicitly!
    iseekable=false;
    SYSTEMTIME st; GetLocalTime(&st);
    FILETIME ft;   SystemTimeToFileTime(&st,&ft);
    WORD dosdate,dostime; filetime2dosdatetime(ft,&dosdate,&dostime);
    times.atime = filetime2timet(ft);
    times.mtime = times.atime;
    times.ctime = times.atime;
    timestamp = (WORD)dostime | (((DWORD)dosdate)<<16);
    hfin=hf;
    return ZR_OK;
  }
}
ZRESULT TZip::open_mem(void *src,unsigned int len)
{ hfin=0; bufin=(const char*)src; selfclosehf=false; crc=CRCVAL_INITIAL; ired=0; csize=0; ired=0;
  lenin=len; posin=0;
  if (src==0 || len==0) return ZR_ARGS;
  attr= 0x80000000; // just a normal file
  isize = len;
  iseekable=true;
  SYSTEMTIME st; GetLocalTime(&st);
  FILETIME ft;   SystemTimeToFileTime(&st,&ft);
  WORD dosdate,dostime; filetime2dosdatetime(ft,&dosdate,&dostime);
  times.atime = filetime2timet(ft);
  times.mtime = times.atime;
  times.ctime = times.atime;
  timestamp = (WORD)dostime | (((DWORD)dosdate)<<16);
  return ZR_OK;
}
ZRESULT TZip::open_dir()
{ hfin=0; bufin=0; selfclosehf=false; crc=CRCVAL_INITIAL; isize=0; csize=0; ired=0;
  attr= 0x41C00010; // a readable writable directory, and again directory
  isize = 0;
  iseekable=false;
  SYSTEMTIME st; GetLocalTime(&st);
  FILETIME ft;   SystemTimeToFileTime(&st,&ft);
  WORD dosdate,dostime; filetime2dosdatetime(ft,&dosdate,&dostime);
  times.atime = filetime2timet(ft);
  times.mtime = times.atime;
  times.ctime = times.atime;
  timestamp = (WORD)dostime | (((DWORD)dosdate)<<16);
  return ZR_OK;
}

unsigned TZip::sread(TState &s,char *buf,unsigned size)
{ // static
  TZip *zip = (TZip*)s.param;
  return zip->read(buf,size);
}

unsigned TZip::read(char *buf, unsigned size)
{ if (bufin!=0)
  { if (posin>=lenin) return 0; // end of input
    ulg red = lenin-posin;
    if (red>size) red=size;
    memcpy(buf, bufin+posin, red);
    posin += red;
    ired += red;
    crc = crc32(crc, (uch*)buf, red);
    return red;
  }
  else if (hfin!=0)
  { DWORD red;
    BOOL ok = ReadFile(hfin,buf,size,&red,NULL);
    if (!ok) return 0;
    ired += red;
    crc = crc32(crc, (uch*)buf, red);
    return red;
  }
  else {oerr=ZR_NOTINITED; return 0;}
}

ZRESULT TZip::iclose()
{ if (selfclosehf && hfin!=0) CloseHandle(hfin); hfin=0;
  bool mismatch = (isize!=-1 && isize!=ired);
  isize=ired; // and crc has been being updated anyway
  if (mismatch) return ZR_MISSIZE;
  else return ZR_OK;
}



ZRESULT TZip::ideflate(TZipFileInfo *zfi)
{ if (state==0) state=new TState();
  // It's a very big object! 500k! We allocate it on the heap, because PocketPC's
  // stack breaks if we try to put it all on the stack. It will be deleted lazily
  state->err=0;
  state->readfunc=sread; state->flush_outbuf=sflush;
  state->param=this; state->level=8; state->seekable=iseekable; state->err=NULL;
  // the following line will make ct_init realise it has to perform the init
  state->ts.static_dtree[0].dl.len = 0;
  // Thanks to Alvin77 for this crucial fix:
  state->ds.window_size=0;
  //  I think that covers everything that needs to be initted.
  //
  bi_init(*state,buf, sizeof(buf), TRUE); // it used to be just 1024-size, not 16384 as here
  ct_init(*state,&zfi->att);
  lm_init(*state,state->level, &zfi->flg);
  ulg sz = deflate(*state);
  csize=sz;
  ZRESULT r=ZR_OK; if (state->err!=NULL) r=ZR_FLATE;
  return r;
}

ZRESULT TZip::istore()
{ ulg size=0;
  for (;;)
  { unsigned int cin=read(buf,16384); if (cin<=0 || cin==(unsigned int)EOF) break;
    unsigned int cout = write(buf,cin); if (cout!=cin) return ZR_MISSIZE;
    size += cin;
  }
  csize=size;
  return ZR_OK;
}





bool has_seeded=false;
ZRESULT TZip::Add(const TCHAR *odstzn, void *src,unsigned int len, DWORD flags)
{ if (oerr) return ZR_FAILED;
  if (hasputcen) return ZR_ENDED;

  // if we use password encryption, then every isize and csize is 12 bytes bigger
  int passex=0; if (password!=0 && flags!=ZIP_FOLDER) passex=12;

  // zip has its own notion of what its names should look like: i.e. dir/file.stuff
  TCHAR dstzn[MAX_PATH]; _tcscpy(dstzn,odstzn);
  if (*dstzn==0) return ZR_ARGS;
  TCHAR *d=dstzn; while (*d!=0) {if (*d=='\\') *d='/'; d++;}
  bool isdir = (flags==ZIP_FOLDER);
  bool needs_trailing_slash = (isdir && dstzn[_tcslen(dstzn)-1]!='/');
  int method=DEFLATE; if (isdir || HasZipSuffix(dstzn)) method=STORE;

  // now open whatever was our input source:
  ZRESULT openres;
  if (flags==ZIP_FILENAME) openres=open_file((const TCHAR*)src);
  else if (flags==ZIP_HANDLE) openres=open_handle((HANDLE)src,len);
  else if (flags==ZIP_MEMORY) openres=open_mem(src,len);
  else if (flags==ZIP_FOLDER) openres=open_dir();
  else return ZR_ARGS;
  if (openres!=ZR_OK) return openres;

  // A zip "entry" consists of a local header (which includes the file name),
  // then the compressed data, and possibly an extended local header.

  // Initialize the local header
  TZipFileInfo zfi; zfi.nxt=NULL;
  strcpy(zfi.name,"");
#ifdef UNICODE
  WideCharToMultiByte(CP_UTF8,0,dstzn,-1,zfi.iname,MAX_PATH,0,0);
#else
  strcpy(zfi.iname,dstzn);
#endif
  zfi.nam=strlen(zfi.iname);
  if (needs_trailing_slash) {strcat(zfi.iname,"/"); zfi.nam++;}
  strcpy(zfi.zname,"");
  zfi.extra=NULL; zfi.ext=0;   // extra header to go after this compressed data, and its length
  zfi.cextra=NULL; zfi.cext=0; // extra header to go in the central end-of-zip directory, and its length
  zfi.comment=NULL; zfi.com=0; // comment, and its length
  zfi.mark = 1;
  zfi.dosflag = 0;
  zfi.att = (ush)BINARY;
  zfi.vem = (ush)0xB17; // 0xB00 is win32 os-code. 0x17 is 23 in decimal: zip 2.3
  zfi.ver = (ush)20;    // Needs PKUNZIP 2.0 to unzip it
  zfi.tim = timestamp;
  // Even though we write the header now, it will have to be rewritten, since we don't know compressed size or crc.
  zfi.crc = 0;            // to be updated later
  zfi.flg = 8;            // 8 means 'there is an extra header'. Assume for the moment that we need it.
  if (password!=0 && !isdir) zfi.flg=9;  // and 1 means 'password-encrypted'
  zfi.lflg = zfi.flg;     // to be updated later
  zfi.how = (ush)method;  // to be updated later
  zfi.siz = (ulg)(method==STORE && isize>=0 ? isize+passex : 0); // to be updated later
  zfi.len = (ulg)(isize);  // to be updated later
  zfi.dsk = 0;
  zfi.atx = attr;
  zfi.off = writ+ooffset;         // offset within file of the start of this local record
  // stuff the 'times' structure into zfi.extra

  // nb. apparently there's a problem with PocketPC CE(zip)->CE(unzip) fails. And removing the following block fixes it up.
  char xloc[EB_L_UT_SIZE]; zfi.extra=xloc;  zfi.ext=EB_L_UT_SIZE;
  char xcen[EB_C_UT_SIZE]; zfi.cextra=xcen; zfi.cext=EB_C_UT_SIZE;
  xloc[0]  = 'U';
  xloc[1]  = 'T';
  xloc[2]  = EB_UT_LEN(3);       // length of data part of e.f.
  xloc[3]  = 0;
  xloc[4]  = EB_UT_FL_MTIME | EB_UT_FL_ATIME | EB_UT_FL_CTIME;
  xloc[5]  = (char)(times.mtime);
  xloc[6]  = (char)(times.mtime >> 8);
  xloc[7]  = (char)(times.mtime >> 16);
  xloc[8]  = (char)(times.mtime >> 24);
  xloc[9]  = (char)(times.atime);
  xloc[10] = (char)(times.atime >> 8);
  xloc[11] = (char)(times.atime >> 16);
  xloc[12] = (char)(times.atime >> 24);
  xloc[13] = (char)(times.ctime);
  xloc[14] = (char)(times.ctime >> 8);
  xloc[15] = (char)(times.ctime >> 16);
  xloc[16] = (char)(times.ctime >> 24);
  memcpy(zfi.cextra,zfi.extra,EB_C_UT_SIZE);
  zfi.cextra[EB_LEN] = EB_UT_LEN(1);


  // (1) Start by writing the local header:
  int r = putlocal(&zfi,swrite,this);
  if (r!=ZE_OK) {iclose(); return ZR_WRITE;}
  writ += 4 + LOCHEAD + (unsigned int)zfi.nam + (unsigned int)zfi.ext;
  if (oerr!=ZR_OK) {iclose(); return oerr;}

  // (1.5) if necessary, write the encryption header
  keys[0]=305419896L;
  keys[1]=591751049L;
  keys[2]=878082192L;
  for (const char *cp=password; cp!=0 && *cp!=0; cp++) update_keys(keys,*cp);
  // generate some random bytes
  if (!has_seeded) srand(GetTickCount()^(unsigned long)GetDesktopWindow());
  char encbuf[12]; for (int i=0; i<12; i++) encbuf[i]=(char)((rand()>>7)&0xff);
  encbuf[11] = (char)((zfi.tim>>8)&0xff);
  for (int ei=0; ei<12; ei++) encbuf[ei]=zencode(keys,encbuf[ei]);
  if (password!=0 && !isdir) {swrite(this,encbuf,12); writ+=12;}

  //(2) Write deflated/stored file to zip file
  ZRESULT writeres=ZR_OK;
  encwriting = (password!=0 && !isdir);  // an object member variable to say whether we write to disk encrypted
  if (!isdir && method==DEFLATE) writeres=ideflate(&zfi);
  else if (!isdir && method==STORE) writeres=istore();
  else if (isdir) csize=0;
  encwriting = false;
  iclose();
  writ += csize;
  if (oerr!=ZR_OK) return oerr;
  if (writeres!=ZR_OK) return ZR_WRITE;

  // (3) Either rewrite the local header with correct information...
  bool first_header_has_size_right = (zfi.siz==csize+passex);
  zfi.crc = crc;
  zfi.siz = csize+passex;
  zfi.len = isize;
  if (ocanseek && (password==0 || isdir))
  { zfi.how = (ush)method;
    if ((zfi.flg & 1) == 0) zfi.flg &= ~8; // clear the extended local header flag
    zfi.lflg = zfi.flg;
    // rewrite the local header:
    if (!oseek(zfi.off-ooffset)) return ZR_SEEK;
    if ((r = putlocal(&zfi, swrite,this)) != ZE_OK) return ZR_WRITE;
    if (!oseek(writ)) return ZR_SEEK;
  }
  else
  { // (4) ... or put an updated header at the end
    if (zfi.how != (ush) method) return ZR_NOCHANGE;
    if (method==STORE && !first_header_has_size_right) return ZR_NOCHANGE;
    if ((r = putextended(&zfi, swrite,this)) != ZE_OK) return ZR_WRITE;
    writ += 16L;
    zfi.flg = zfi.lflg; // if flg modified by inflate, for the central index
  }
  if (oerr!=ZR_OK) return oerr;

  // Keep a copy of the zipfileinfo, for our end-of-zip directory
  char *cextra = new char[zfi.cext]; memcpy(cextra,zfi.cextra,zfi.cext); zfi.cextra=cextra;
  TZipFileInfo *pzfi = new TZipFileInfo; memcpy(pzfi,&zfi,sizeof(zfi));
  if (zfis==NULL) zfis=pzfi;
  else {TZipFileInfo *z=zfis; while (z->nxt!=NULL) z=z->nxt; z->nxt=pzfi;}
  return ZR_OK;
}

ZRESULT TZip::AddCentral()
{ // write central directory
  int numentries = 0;
  ulg pos_at_start_of_central = writ;
  //ulg tot_unc_size=0, tot_compressed_size=0;
  bool okay=true;
  for (TZipFileInfo *zfi=zfis; zfi!=NULL; )
  { if (okay)
    { int res = putcentral(zfi, swrite,this);
      if (res!=ZE_OK) okay=false;
    }
    writ += 4 + CENHEAD + (unsigned int)zfi->nam + (unsigned int)zfi->cext + (unsigned int)zfi->com;
    //tot_unc_size += zfi->len;
    //tot_compressed_size += zfi->siz;
    numentries++;
    //
    TZipFileInfo *zfinext = zfi->nxt;
    if (zfi->cextra!=0) delete[] zfi->cextra;
    delete zfi;
    zfi = zfinext;
  }
  ulg center_size = writ - pos_at_start_of_central;
  if (okay)
  { int res = putend(numentries, center_size, pos_at_start_of_central+ooffset, 0, NULL, swrite,this);
    if (res!=ZE_OK) okay=false;
    writ += 4 + ENDHEAD + 0;
  }
  if (!okay) return ZR_WRITE;
  return ZR_OK;
}





ZRESULT lasterrorZ=ZR_OK;

unsigned int FormatZipMessageZ(ZRESULT code, char *buf,unsigned int len)
{ if (code==ZR_RECENT) code=lasterrorZ;
  const char *msg="unknown zip result code";
  switch (code)
  { case ZR_OK: msg="Success"; break;
    case ZR_NODUPH: msg="Culdn't duplicate handle"; break;
    case ZR_NOFILE: msg="Couldn't create/open file"; break;
    case ZR_NOALLOC: msg="Failed to allocate memory"; break;
    case ZR_WRITE: msg="Error writing to file"; break;
    case ZR_NOTFOUND: msg="File not found in the zipfile"; break;
    case ZR_MORE: msg="Still more data to unzip"; break;
    case ZR_CORRUPT: msg="Zipfile is corrupt or not a zipfile"; break;
    case ZR_READ: msg="Error reading file"; break;
    case ZR_ARGS: msg="Caller: faulty arguments"; break;
    case ZR_PARTIALUNZ: msg="Caller: the file had already been partially unzipped"; break;
    case ZR_NOTMMAP: msg="Caller: can only get memory of a memory zipfile"; break;
    case ZR_MEMSIZE: msg="Caller: not enough space allocated for memory zipfile"; break;
    case ZR_FAILED: msg="Caller: there was a previous error"; break;
    case ZR_ENDED: msg="Caller: additions to the zip have already been ended"; break;
    case ZR_ZMODE: msg="Caller: mixing creation and opening of zip"; break;
    case ZR_NOTINITED: msg="Zip-bug: internal initialisation not completed"; break;
    case ZR_SEEK: msg="Zip-bug: trying to seek the unseekable"; break;
    case ZR_MISSIZE: msg="Zip-bug: the anticipated size turned out wrong"; break;
    case ZR_NOCHANGE: msg="Zip-bug: tried to change mind, but not allowed"; break;
    case ZR_FLATE: msg="Zip-bug: an internal error during flation"; break;
  }
  unsigned int mlen=(unsigned int)strlen(msg);
  if (buf==0 || len==0) return mlen;
  unsigned int n=mlen; if (n+1>len) n=len-1;
  strncpy(buf,msg,n); buf[n]=0;
  return mlen;
}



typedef struct
{ DWORD flag;
  TZip *zip;
} TZipHandleData;


HZIP CreateZipInternal(void *z,unsigned int len,DWORD flags, const char *password)
{ TZip *zip = new TZip(password);
  lasterrorZ = zip->Create(z,len,flags);
  if (lasterrorZ!=ZR_OK) {delete zip; return 0;}
  TZipHandleData *han = new TZipHandleData;
  han->flag=2; han->zip=zip; return (HZIP)han;
}
HZIP CreateZipHandle(HANDLE h, const char *password) {return CreateZipInternal(h,0,ZIP_HANDLE,password);}
HZIP CreateZip(const TCHAR *fn, const char *password) {return CreateZipInternal((void*)fn,0,ZIP_FILENAME,password);}
HZIP CreateZip(void *z,unsigned int len, const char *password) {return CreateZipInternal(z,len,ZIP_MEMORY,password);}


ZRESULT ZipAddInternal(HZIP hz,const TCHAR *dstzn, void *src,unsigned int len, DWORD flags)
{ if (hz==0) {lasterrorZ=ZR_ARGS;return ZR_ARGS;}
  TZipHandleData *han = (TZipHandleData*)hz;
  if (han->flag!=2) {lasterrorZ=ZR_ZMODE;return ZR_ZMODE;}
  TZip *zip = han->zip;
  lasterrorZ = zip->Add(dstzn,src,len,flags);
  return lasterrorZ;
}
ZRESULT ZipAdd(HZIP hz,const TCHAR *dstzn, const TCHAR *fn) {return ZipAddInternal(hz,dstzn,(void*)fn,0,ZIP_FILENAME);}
ZRESULT ZipAdd(HZIP hz,const TCHAR *dstzn, void *src,unsigned int len) {return ZipAddInternal(hz,dstzn,src,len,ZIP_MEMORY);}
ZRESULT ZipAddHandle(HZIP hz,const TCHAR *dstzn, HANDLE h) {return ZipAddInternal(hz,dstzn,h,0,ZIP_HANDLE);}
ZRESULT ZipAddHandle(HZIP hz,const TCHAR *dstzn, HANDLE h, unsigned int len) {return ZipAddInternal(hz,dstzn,h,len,ZIP_HANDLE);}
ZRESULT ZipAddFolder(HZIP hz,const TCHAR *dstzn) {return ZipAddInternal(hz,dstzn,0,0,ZIP_FOLDER);}



ZRESULT ZipGetMemory(HZIP hz, void **buf, unsigned long *len)
{ if (hz==0) {if (buf!=0) *buf=0; if (len!=0) *len=0; lasterrorZ=ZR_ARGS;return ZR_ARGS;}
  TZipHandleData *han = (TZipHandleData*)hz;
  if (han->flag!=2) {lasterrorZ=ZR_ZMODE;return ZR_ZMODE;}
  TZip *zip = han->zip;
  lasterrorZ = zip->GetMemory(buf,len);
  return lasterrorZ;
}

ZRESULT CloseZipZ(HZIP hz)
{ if (hz==0) {lasterrorZ=ZR_ARGS;return ZR_ARGS;}
  TZipHandleData *han = (TZipHandleData*)hz;
  if (han->flag!=2) {lasterrorZ=ZR_ZMODE;return ZR_ZMODE;}
  TZip *zip = han->zip;
  lasterrorZ = zip->Close();
  delete zip;
  delete han;
  return lasterrorZ;
}

bool IsZipHandleZ(HZIP hz)
{ if (hz==0) return false;
  TZipHandleData *han = (TZipHandleData*)hz;
  return (han->flag==2);
}

