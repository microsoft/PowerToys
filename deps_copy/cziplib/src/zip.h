/*
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 */

#pragma once
#ifndef ZIP_H
#define ZIP_H

#include <stdint.h>
#include <string.h>
#include <sys/types.h>

#ifndef ZIP_SHARED
#define ZIP_EXPORT
#else
#ifdef _WIN32
#ifdef ZIP_BUILD_SHARED
#define ZIP_EXPORT __declspec(dllexport)
#else
#define ZIP_EXPORT __declspec(dllimport)
#endif
#else
#define ZIP_EXPORT __attribute__((visibility("default")))
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

#if !defined(_POSIX_C_SOURCE) && defined(_MSC_VER)
// 64-bit Windows is the only mainstream platform
// where sizeof(long) != sizeof(void*)
#ifdef _WIN64
typedef long long ssize_t; /* byte count or error */
#else
typedef long ssize_t; /* byte count or error */
#endif
#endif

#ifndef MAX_PATH
#define MAX_PATH 1024 /* # chars in a path name including NULL */
#endif

/**
 * @mainpage
 *
 * Documenation for @ref zip.
 */

/**
 * @addtogroup zip
 * @{
 */

/**
 * Default zip compression level.
 */
#define ZIP_DEFAULT_COMPRESSION_LEVEL 6

/**
 * Error codes
 */
#define ZIP_ENOINIT -1      // not initialized
#define ZIP_EINVENTNAME -2  // invalid entry name
#define ZIP_ENOENT -3       // entry not found
#define ZIP_EINVMODE -4     // invalid zip mode
#define ZIP_EINVLVL -5      // invalid compression level
#define ZIP_ENOSUP64 -6     // no zip 64 support
#define ZIP_EMEMSET -7      // memset error
#define ZIP_EWRTENT -8      // cannot write data to entry
#define ZIP_ETDEFLINIT -9   // cannot initialize tdefl compressor
#define ZIP_EINVIDX -10     // invalid index
#define ZIP_ENOHDR -11      // header not found
#define ZIP_ETDEFLBUF -12   // cannot flush tdefl buffer
#define ZIP_ECRTHDR -13     // cannot create entry header
#define ZIP_EWRTHDR -14     // cannot write entry header
#define ZIP_EWRTDIR -15     // cannot write to central dir
#define ZIP_EOPNFILE -16    // cannot open file
#define ZIP_EINVENTTYPE -17 // invalid entry type
#define ZIP_EMEMNOALLOC -18 // extracting data using no memory allocation
#define ZIP_ENOFILE -19     // file not found
#define ZIP_ENOPERM -20     // no permission
#define ZIP_EOOMEM -21      // out of memory
#define ZIP_EINVZIPNAME -22 // invalid zip archive name
#define ZIP_EMKDIR -23      // make dir error
#define ZIP_ESYMLINK -24    // symlink error
#define ZIP_ECLSZIP -25     // close archive error
#define ZIP_ECAPSIZE -26    // capacity size too small
#define ZIP_EFSEEK -27      // fseek error
#define ZIP_EFREAD -28      // fread error
#define ZIP_EFWRITE -29     // fwrite error

/**
 * Looks up the error message string coresponding to an error number.
 * @param errnum error number
 * @return error message string coresponding to errnum or NULL if error is not
 * found.
 */
extern ZIP_EXPORT const char *zip_strerror(int errnum);

/**
 * @struct zip_t
 *
 * This data structure is used throughout the library to represent zip archive -
 * forward declaration.
 */
struct zip_t;

/**
 * Opens zip archive with compression level using the given mode.
 *
 * @param zipname zip archive file name.
 * @param level compression level (0-9 are the standard zlib-style levels).
 * @param mode file access mode.
 *        - 'r': opens a file for reading/extracting (the file must exists).
 *        - 'w': creates an empty file for writing.
 *        - 'a': appends to an existing archive.
 *
 * @return the zip archive handler or NULL on error
 */
extern ZIP_EXPORT struct zip_t *zip_open(const char *zipname, int level,
                                         char mode);

/**
 * Closes the zip archive, releases resources - always finalize.
 *
 * @param zip zip archive handler.
 */
extern ZIP_EXPORT void zip_close(struct zip_t *zip);

/**
 * Determines if the archive has a zip64 end of central directory headers.
 *
 * @param zip zip archive handler.
 *
 * @return the return code - 1 (true), 0 (false), negative number (< 0) on
 *         error.
 */
extern ZIP_EXPORT int zip_is64(struct zip_t *zip);

/**
 * Opens an entry by name in the zip archive.
 *
 * For zip archive opened in 'w' or 'a' mode the function will append
 * a new entry. In readonly mode the function tries to locate the entry
 * in global dictionary.
 *
 * @param zip zip archive handler.
 * @param entryname an entry name in local dictionary.
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int zip_entry_open(struct zip_t *zip, const char *entryname);

/**
 * Opens an entry by name in the zip archive.
 *
 * For zip archive opened in 'w' or 'a' mode the function will append
 * a new entry. In readonly mode the function tries to locate the entry
 * in global dictionary (case sensitive).
 *
 * @param zip zip archive handler.
 * @param entryname an entry name in local dictionary (case sensitive).
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int zip_entry_opencasesensitive(struct zip_t *zip,
                                                  const char *entryname);

/**
 * Opens a new entry by index in the zip archive.
 *
 * This function is only valid if zip archive was opened in 'r' (readonly) mode.
 *
 * @param zip zip archive handler.
 * @param index index in local dictionary.
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int zip_entry_openbyindex(struct zip_t *zip, size_t index);

/**
 * Closes a zip entry, flushes buffer and releases resources.
 *
 * @param zip zip archive handler.
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int zip_entry_close(struct zip_t *zip);

/**
 * Returns a local name of the current zip entry.
 *
 * The main difference between user's entry name and local entry name
 * is optional relative path.
 * Following .ZIP File Format Specification - the path stored MUST not contain
 * a drive or device letter, or a leading slash.
 * All slashes MUST be forward slashes '/' as opposed to backwards slashes '\'
 * for compatibility with Amiga and UNIX file systems etc.
 *
 * @param zip: zip archive handler.
 *
 * @return the pointer to the current zip entry name, or NULL on error.
 */
extern ZIP_EXPORT const char *zip_entry_name(struct zip_t *zip);

/**
 * Returns an index of the current zip entry.
 *
 * @param zip zip archive handler.
 *
 * @return the index on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT ssize_t zip_entry_index(struct zip_t *zip);

/**
 * Determines if the current zip entry is a directory entry.
 *
 * @param zip zip archive handler.
 *
 * @return the return code - 1 (true), 0 (false), negative number (< 0) on
 *         error.
 */
extern ZIP_EXPORT int zip_entry_isdir(struct zip_t *zip);

/**
 * Returns the uncompressed size of the current zip entry.
 * Alias for zip_entry_uncomp_size (for backward compatibility).
 *
 * @param zip zip archive handler.
 *
 * @return the uncompressed size in bytes.
 */
extern ZIP_EXPORT unsigned long long zip_entry_size(struct zip_t *zip);

/**
 * Returns the uncompressed size of the current zip entry.
 *
 * @param zip zip archive handler.
 *
 * @return the uncompressed size in bytes.
 */
extern ZIP_EXPORT unsigned long long zip_entry_uncomp_size(struct zip_t *zip);

/**
 * Returns the compressed size of the current zip entry.
 *
 * @param zip zip archive handler.
 *
 * @return the compressed size in bytes.
 */
extern ZIP_EXPORT unsigned long long zip_entry_comp_size(struct zip_t *zip);

/**
 * Returns CRC-32 checksum of the current zip entry.
 *
 * @param zip zip archive handler.
 *
 * @return the CRC-32 checksum.
 */
extern ZIP_EXPORT unsigned int zip_entry_crc32(struct zip_t *zip);

/**
 * Compresses an input buffer for the current zip entry.
 *
 * @param zip zip archive handler.
 * @param buf input buffer.
 * @param bufsize input buffer size (in bytes).
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int zip_entry_write(struct zip_t *zip, const void *buf,
                                      size_t bufsize);

/**
 * Compresses a file for the current zip entry.
 *
 * @param zip zip archive handler.
 * @param filename input file.
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int zip_entry_fwrite(struct zip_t *zip, const char *filename);

/**
 * Extracts the current zip entry into output buffer.
 *
 * The function allocates sufficient memory for a output buffer.
 *
 * @param zip zip archive handler.
 * @param buf output buffer.
 * @param bufsize output buffer size (in bytes).
 *
 * @note remember to release memory allocated for a output buffer.
 *       for large entries, please take a look at zip_entry_extract function.
 *
 * @return the return code - the number of bytes actually read on success.
 *         Otherwise a negative number (< 0) on error.
 */
extern ZIP_EXPORT ssize_t zip_entry_read(struct zip_t *zip, void **buf,
                                         size_t *bufsize);

/**
 * Extracts the current zip entry into a memory buffer using no memory
 * allocation.
 *
 * @param zip zip archive handler.
 * @param buf preallocated output buffer.
 * @param bufsize output buffer size (in bytes).
 *
 * @note ensure supplied output buffer is large enough.
 *       zip_entry_size function (returns uncompressed size for the current
 *       entry) can be handy to estimate how big buffer is needed.
 *       For large entries, please take a look at zip_entry_extract function.
 *
 * @return the return code - the number of bytes actually read on success.
 *         Otherwise a negative number (< 0) on error (e.g. bufsize is not large
 * enough).
 */
extern ZIP_EXPORT ssize_t zip_entry_noallocread(struct zip_t *zip, void *buf,
                                                size_t bufsize);

/**
 * Extracts the current zip entry into output file.
 *
 * @param zip zip archive handler.
 * @param filename output file.
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int zip_entry_fread(struct zip_t *zip, const char *filename);

/**
 * Extracts the current zip entry using a callback function (on_extract).
 *
 * @param zip zip archive handler.
 * @param on_extract callback function.
 * @param arg opaque pointer (optional argument, which you can pass to the
 *        on_extract callback)
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int
zip_entry_extract(struct zip_t *zip,
                  size_t (*on_extract)(void *arg, uint64_t offset,
                                       const void *data, size_t size),
                  void *arg);

/**
 * Returns the number of all entries (files and directories) in the zip archive.
 *
 * @param zip zip archive handler.
 *
 * @return the return code - the number of entries on success, negative number
 *         (< 0) on error.
 */
extern ZIP_EXPORT ssize_t zip_entries_total(struct zip_t *zip);

/**
 * Deletes zip archive entries.
 *
 * @param zip zip archive handler.
 * @param entries array of zip archive entries to be deleted.
 * @param len the number of entries to be deleted.
 * @return the number of deleted entries, or negative number (< 0) on error.
 */
extern ZIP_EXPORT ssize_t zip_entries_delete(struct zip_t *zip,
                                             char *const entries[], size_t len);

/**
 * Extracts a zip archive stream into directory.
 *
 * If on_extract is not NULL, the callback will be called after
 * successfully extracted each zip entry.
 * Returning a negative value from the callback will cause abort and return an
 * error. The last argument (void *arg) is optional, which you can use to pass
 * data to the on_extract callback.
 *
 * @param stream zip archive stream.
 * @param size stream size.
 * @param dir output directory.
 * @param on_extract on extract callback.
 * @param arg opaque pointer.
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int
zip_stream_extract(const char *stream, size_t size, const char *dir,
                   int (*on_extract)(const char *filename, void *arg),
                   void *arg);

/**
 * Opens zip archive stream into memory.
 *
 * @param stream zip archive stream.
 * @param size stream size.
 *
 * @return the zip archive handler or NULL on error
 */
extern ZIP_EXPORT struct zip_t *zip_stream_open(const char *stream, size_t size,
                                                int level, char mode);

/**
 * Copy zip archive stream output buffer.
 *
 * @param zip zip archive handler.
 * @param buf output buffer. User should free buf.
 * @param bufsize output buffer size (in bytes).
 *
 * @return copy size
 */
extern ZIP_EXPORT ssize_t zip_stream_copy(struct zip_t *zip, void **buf,
                                          size_t *bufsize);

/**
 * Close zip archive releases resources.
 *
 * @param zip zip archive handler.
 *
 * @return
 */
extern ZIP_EXPORT void zip_stream_close(struct zip_t *zip);

/**
 * Creates a new archive and puts files into a single zip archive.
 *
 * @param zipname zip archive file.
 * @param filenames input files.
 * @param len: number of input files.
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int zip_create(const char *zipname, const char *filenames[],
                                 size_t len);

/**
 * Extracts a zip archive file into directory.
 *
 * If on_extract_entry is not NULL, the callback will be called after
 * successfully extracted each zip entry.
 * Returning a negative value from the callback will cause abort and return an
 * error. The last argument (void *arg) is optional, which you can use to pass
 * data to the on_extract_entry callback.
 *
 * @param zipname zip archive file.
 * @param dir output directory.
 * @param on_extract_entry on extract callback.
 * @param arg opaque pointer.
 *
 * @return the return code - 0 on success, negative number (< 0) on error.
 */
extern ZIP_EXPORT int zip_extract(const char *zipname, const char *dir,
                                  int (*on_extract_entry)(const char *filename,
                                                          void *arg),
                                  void *arg);
/** @} */
#ifdef __cplusplus
}
#endif

#endif
