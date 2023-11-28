/*
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 */
#define __STDC_WANT_LIB_EXT1__ 1

#include <errno.h>
#include <sys/stat.h>
#include <time.h>

#if defined(_WIN32) || defined(__WIN32__) || defined(_MSC_VER) ||              \
    defined(__MINGW32__)
/* Win32, DOS, MSVC, MSVS */
#include <direct.h>

#define STRCLONE(STR) ((STR) ? _strdup(STR) : NULL)
#define HAS_DEVICE(P)                                                          \
  ((((P)[0] >= 'A' && (P)[0] <= 'Z') || ((P)[0] >= 'a' && (P)[0] <= 'z')) &&   \
   (P)[1] == ':')
#define FILESYSTEM_PREFIX_LEN(P) (HAS_DEVICE(P) ? 2 : 0)

#else

#include <unistd.h> // needed for symlink()
#define STRCLONE(STR) ((STR) ? strdup(STR) : NULL)

#endif

#ifdef __MINGW32__
#include <sys/types.h>
#include <unistd.h>
#endif

#include "miniz.h"
#include "zip.h"

#ifdef _MSC_VER
#include <io.h>

#define ftruncate(fd, sz) (-(_chsize_s((fd), (sz)) != 0))
#define fileno _fileno
#endif

#if defined(__TINYC__) && (defined(_WIN32) || defined(_WIN64))
#include <io.h>

#define ftruncate(fd, sz) (-(_chsize_s((fd), (sz)) != 0))
#define fileno _fileno
#endif

#ifndef HAS_DEVICE
#define HAS_DEVICE(P) 0
#endif

#ifndef FILESYSTEM_PREFIX_LEN
#define FILESYSTEM_PREFIX_LEN(P) 0
#endif

#ifndef ISSLASH
#define ISSLASH(C) ((C) == '/' || (C) == '\\')
#endif

#define CLEANUP(ptr)                                                           \
  do {                                                                         \
    if (ptr) {                                                                 \
      free((void *)ptr);                                                       \
      ptr = NULL;                                                              \
    }                                                                          \
  } while (0)

#define UNX_IFDIR 0040000  /* Unix directory */
#define UNX_IFREG 0100000  /* Unix regular file */
#define UNX_IFSOCK 0140000 /* Unix socket (BSD, not SysV or Amiga) */
#define UNX_IFLNK 0120000  /* Unix symbolic link (not SysV, Amiga) */
#define UNX_IFBLK 0060000  /* Unix block special       (not Amiga) */
#define UNX_IFCHR 0020000  /* Unix character special   (not Amiga) */
#define UNX_IFIFO 0010000  /* Unix fifo    (BCC, not MSC or Amiga) */

struct zip_entry_t {
  ssize_t index;
  char *name;
  mz_uint64 uncomp_size;
  mz_uint64 comp_size;
  mz_uint32 uncomp_crc32;
  mz_uint64 offset;
  mz_uint8 header[MZ_ZIP_LOCAL_DIR_HEADER_SIZE];
  mz_uint64 header_offset;
  mz_uint16 method;
  mz_zip_writer_add_state state;
  tdefl_compressor comp;
  mz_uint32 external_attr;
  time_t m_time;
};

struct zip_t {
  mz_zip_archive archive;
  mz_uint level;
  struct zip_entry_t entry;
};

enum zip_modify_t {
  MZ_KEEP = 0,
  MZ_DELETE = 1,
  MZ_MOVE = 2,
};

struct zip_entry_mark_t {
  ssize_t file_index;
  enum zip_modify_t type;
  mz_uint64 m_local_header_ofs;
  size_t lf_length;
};

static const char *const zip_errlist[30] = {
    NULL,
    "not initialized\0",
    "invalid entry name\0",
    "entry not found\0",
    "invalid zip mode\0",
    "invalid compression level\0",
    "no zip 64 support\0",
    "memset error\0",
    "cannot write data to entry\0",
    "cannot initialize tdefl compressor\0",
    "invalid index\0",
    "header not found\0",
    "cannot flush tdefl buffer\0",
    "cannot write entry header\0",
    "cannot create entry header\0",
    "cannot write to central dir\0",
    "cannot open file\0",
    "invalid entry type\0",
    "extracting data using no memory allocation\0",
    "file not found\0",
    "no permission\0",
    "out of memory\0",
    "invalid zip archive name\0",
    "make dir error\0",
    "symlink error\0",
    "close archive error\0",
    "capacity size too small\0",
    "fseek error\0",
    "fread error\0",
    "fwrite error\0",
};

const char *zip_strerror(int errnum) {
  errnum = -errnum;
  if (errnum <= 0 || errnum >= 30) {
    return NULL;
  }

  return zip_errlist[errnum];
}

static const char *zip_basename(const char *name) {
  char const *p;
  char const *base = name += FILESYSTEM_PREFIX_LEN(name);
  int all_slashes = 1;

  for (p = name; *p; p++) {
    if (ISSLASH(*p))
      base = p + 1;
    else
      all_slashes = 0;
  }

  /* If NAME is all slashes, arrange to return `/'. */
  if (*base == '\0' && ISSLASH(*name) && all_slashes)
    --base;

  return base;
}

static int zip_mkpath(char *path) {
  char *p;
  char npath[MAX_PATH + 1];
  int len = 0;
  int has_device = HAS_DEVICE(path);

  memset(npath, 0, MAX_PATH + 1);
  if (has_device) {
    // only on windows
    npath[0] = path[0];
    npath[1] = path[1];
    len = 2;
  }
  for (p = path + len; *p && len < MAX_PATH; p++) {
    if (ISSLASH(*p) && ((!has_device && len > 0) || (has_device && len > 2))) {
#if defined(_WIN32) || defined(__WIN32__) || defined(_MSC_VER) ||              \
    defined(__MINGW32__)
#else
      if ('\\' == *p) {
        *p = '/';
      }
#endif

      if (MZ_MKDIR(npath) == -1) {
        if (errno != EEXIST) {
          return ZIP_EMKDIR;
        }
      }
    }
    npath[len++] = *p;
  }

  return 0;
}

static char *zip_strrpl(const char *str, size_t n, char oldchar, char newchar) {
  char c;
  size_t i;
  char *rpl = (char *)calloc((1 + n), sizeof(char));
  char *begin = rpl;
  if (!rpl) {
    return NULL;
  }

  for (i = 0; (i < n) && (c = *str++); ++i) {
    if (c == oldchar) {
      c = newchar;
    }
    *rpl++ = c;
  }

  return begin;
}

static char *zip_name_normalize(char *name, char *const nname, size_t len) {
  size_t offn = 0;
  size_t offnn = 0, ncpy = 0;

  if (name == NULL || nname == NULL || len <= 0) {
    return NULL;
  }
  // skip trailing '/'
  while (ISSLASH(*name))
    name++;

  for (; offn < len; offn++) {
    if (ISSLASH(name[offn])) {
      if (ncpy > 0 && strcmp(&nname[offnn], ".\0") &&
          strcmp(&nname[offnn], "..\0")) {
        offnn += ncpy;
        nname[offnn++] = name[offn]; // append '/'
      }
      ncpy = 0;
    } else {
      nname[offnn + ncpy] = name[offn];
      ncpy++;
    }
  }

  // at the end, extra check what we've already copied
  if (ncpy == 0 || !strcmp(&nname[offnn], ".\0") ||
      !strcmp(&nname[offnn], "..\0")) {
    nname[offnn] = 0;
  }
  return nname;
}

static mz_bool zip_name_match(const char *name1, const char *name2) {
  char *nname2 = NULL;

#ifdef ZIP_RAW_ENTRYNAME
  nname2 = STRCLONE(name2);
#else
  nname2 = zip_strrpl(name2, strlen(name2), '\\', '/');
#endif

  if (!nname2) {
    return MZ_FALSE;
  }

  mz_bool res = (strcmp(name1, nname2) == 0) ? MZ_TRUE : MZ_FALSE;
  CLEANUP(nname2);
  return res;
}

static int zip_archive_truncate(mz_zip_archive *pzip) {
  mz_zip_internal_state *pState = pzip->m_pState;
  mz_uint64 file_size = pzip->m_archive_size;
  if ((pzip->m_pWrite == mz_zip_heap_write_func) && (pState->m_pMem)) {
    return 0;
  }
  if (pzip->m_zip_mode == MZ_ZIP_MODE_WRITING_HAS_BEEN_FINALIZED) {
    if (pState->m_pFile) {
      int fd = fileno(pState->m_pFile);
      return ftruncate(fd, file_size);
    }
  }
  return 0;
}

static int zip_archive_extract(mz_zip_archive *zip_archive, const char *dir,
                               int (*on_extract)(const char *filename,
                                                 void *arg),
                               void *arg) {
  int err = 0;
  mz_uint i, n;
  char path[MAX_PATH + 1];
  char symlink_to[MAX_PATH + 1];
  mz_zip_archive_file_stat info;
  size_t dirlen = 0, filename_size = MZ_ZIP_MAX_ARCHIVE_FILENAME_SIZE;
  mz_uint32 xattr = 0;

  memset(path, 0, sizeof(path));
  memset(symlink_to, 0, sizeof(symlink_to));

  dirlen = strlen(dir);
  if (dirlen + 1 > MAX_PATH) {
    return ZIP_EINVENTNAME;
  }

  memset((void *)&info, 0, sizeof(mz_zip_archive_file_stat));

#if defined(_MSC_VER)
  strcpy_s(path, MAX_PATH, dir);
#else
  strcpy(path, dir);
#endif

  if (!ISSLASH(path[dirlen - 1])) {
#if defined(_WIN32) || defined(__WIN32__)
    path[dirlen] = '\\';
#else
    path[dirlen] = '/';
#endif
    ++dirlen;
  }

  if (filename_size > MAX_PATH - dirlen) {
    filename_size = MAX_PATH - dirlen;
  }
  // Get and print information about each file in the archive.
  n = mz_zip_reader_get_num_files(zip_archive);
  for (i = 0; i < n; ++i) {
    if (!mz_zip_reader_file_stat(zip_archive, i, &info)) {
      // Cannot get information about zip archive;
      err = ZIP_ENOENT;
      goto out;
    }

    if (!zip_name_normalize(info.m_filename, info.m_filename,
                            strlen(info.m_filename))) {
      // Cannot normalize file name;
      err = ZIP_EINVENTNAME;
      goto out;
    }

#if defined(_MSC_VER)
    strncpy_s(&path[dirlen], filename_size, info.m_filename,
              filename_size);
#else
    strncpy(&path[dirlen], info.m_filename, filename_size);
#endif
    err = zip_mkpath(path);
    if (err < 0) {
      // Cannot make a path
      goto out;
    }

    if ((((info.m_version_made_by >> 8) == 3) ||
         ((info.m_version_made_by >> 8) ==
          19)) // if zip is produced on Unix or macOS (3 and 19 from
               // section 4.4.2.2 of zip standard)
        && info.m_external_attr &
               (0x20 << 24)) { // and has sym link attribute (0x80 is file, 0x40
                               // is directory)
#if defined(_WIN32) || defined(__WIN32__) || defined(_MSC_VER) ||              \
    defined(__MINGW32__)
#else
      if (info.m_uncomp_size > MAX_PATH ||
          !mz_zip_reader_extract_to_mem_no_alloc(zip_archive, i, symlink_to,
                                                 MAX_PATH, 0, NULL, 0)) {
        err = ZIP_EMEMNOALLOC;
        goto out;
      }
      symlink_to[info.m_uncomp_size] = '\0';
      if (symlink(symlink_to, path) != 0) {
        err = ZIP_ESYMLINK;
        goto out;
      }
#endif
    } else {
      if (!mz_zip_reader_is_file_a_directory(zip_archive, i)) {
        if (!mz_zip_reader_extract_to_file(zip_archive, i, path, 0)) {
          // Cannot extract zip archive to file
          err = ZIP_ENOFILE;
          goto out;
        }
      }

#if defined(_MSC_VER) || defined(PS4)
      (void)xattr; // unused
#else
      xattr = (info.m_external_attr >> 16) & 0xFFFF;
      if (xattr > 0 && xattr <= MZ_UINT16_MAX) {
        if (CHMOD(path, (mode_t)xattr) < 0) {
          err = ZIP_ENOPERM;
          goto out;
        }
      }
#endif
    }

    if (on_extract) {
      if (on_extract(path, arg) < 0) {
        goto out;
      }
    }
  }

out:
  // Close the archive, freeing any resources it was using
  if (!mz_zip_reader_end(zip_archive)) {
    // Cannot end zip reader
    err = ZIP_ECLSZIP;
  }
  return err;
}

static inline void zip_archive_finalize(mz_zip_archive *pzip) {
  mz_zip_writer_finalize_archive(pzip);
  zip_archive_truncate(pzip);
}

static ssize_t zip_entry_mark(struct zip_t *zip,
                              struct zip_entry_mark_t *entry_mark,
                              const ssize_t n, char *const entries[],
                              const size_t len) {
  ssize_t i = 0;
  ssize_t err = 0;
  if (!zip || !entry_mark || !entries) {
    return ZIP_ENOINIT;
  }

  mz_zip_archive_file_stat file_stat;
  mz_uint64 d_pos = ~0UL;
  for (i = 0; i < n; ++i) {
    if ((err = zip_entry_openbyindex(zip, i))) {
      return (ssize_t)err;
    }

    mz_bool name_matches = MZ_FALSE;
    {
      size_t j;
      for (j = 0; j < len; ++j) {
        if (zip_name_match(zip->entry.name, entries[j])) {
          name_matches = MZ_TRUE;
          break;
        }
      }
    }
    if (name_matches) {
      entry_mark[i].type = MZ_DELETE;
    } else {
      entry_mark[i].type = MZ_KEEP;
    }

    if (!mz_zip_reader_file_stat(&zip->archive, i, &file_stat)) {
      return ZIP_ENOENT;
    }

    zip_entry_close(zip);

    entry_mark[i].m_local_header_ofs = file_stat.m_local_header_ofs;
    entry_mark[i].file_index = (ssize_t)-1;
    entry_mark[i].lf_length = 0;
    if ((entry_mark[i].type) == MZ_DELETE &&
        (d_pos > entry_mark[i].m_local_header_ofs)) {
      d_pos = entry_mark[i].m_local_header_ofs;
    }
  }

  for (i = 0; i < n; ++i) {
    if ((entry_mark[i].m_local_header_ofs > d_pos) &&
        (entry_mark[i].type != MZ_DELETE)) {
      entry_mark[i].type = MZ_MOVE;
    }
  }
  return err;
}

static ssize_t zip_index_next(mz_uint64 *local_header_ofs_array,
                              ssize_t cur_index) {
  ssize_t new_index = 0, i;
  for (i = cur_index - 1; i >= 0; --i) {
    if (local_header_ofs_array[cur_index] > local_header_ofs_array[i]) {
      new_index = i + 1;
      return new_index;
    }
  }
  return new_index;
}

static ssize_t zip_sort(mz_uint64 *local_header_ofs_array, ssize_t cur_index) {
  ssize_t nxt_index = zip_index_next(local_header_ofs_array, cur_index);

  if (nxt_index != cur_index) {
    mz_uint64 temp = local_header_ofs_array[cur_index];
    ssize_t i;
    for (i = cur_index; i > nxt_index; i--) {
      local_header_ofs_array[i] = local_header_ofs_array[i - 1];
    }
    local_header_ofs_array[nxt_index] = temp;
  }
  return nxt_index;
}

static int zip_index_update(struct zip_entry_mark_t *entry_mark,
                            ssize_t last_index, ssize_t nxt_index) {
  ssize_t j;
  for (j = 0; j < last_index; j++) {
    if (entry_mark[j].file_index >= nxt_index) {
      entry_mark[j].file_index += 1;
    }
  }
  entry_mark[nxt_index].file_index = last_index;
  return 0;
}

static int zip_entry_finalize(struct zip_t *zip,
                              struct zip_entry_mark_t *entry_mark,
                              const ssize_t n) {

  ssize_t i = 0;
  mz_uint64 *local_header_ofs_array = (mz_uint64 *)calloc(n, sizeof(mz_uint64));
  if (!local_header_ofs_array) {
    return ZIP_EOOMEM;
  }

  for (i = 0; i < n; ++i) {
    local_header_ofs_array[i] = entry_mark[i].m_local_header_ofs;
    ssize_t index = zip_sort(local_header_ofs_array, i);

    if (index != i) {
      zip_index_update(entry_mark, i, index);
    }
    entry_mark[i].file_index = index;
  }

  size_t *length = (size_t *)calloc(n, sizeof(size_t));
  if (!length) {
    CLEANUP(local_header_ofs_array);
    return ZIP_EOOMEM;
  }
  for (i = 0; i < n - 1; i++) {
    length[i] =
        (size_t)(local_header_ofs_array[i + 1] - local_header_ofs_array[i]);
  }
  length[n - 1] =
      (size_t)(zip->archive.m_archive_size - local_header_ofs_array[n - 1]);

  for (i = 0; i < n; i++) {
    entry_mark[i].lf_length = length[entry_mark[i].file_index];
  }

  CLEANUP(length);
  CLEANUP(local_header_ofs_array);
  return 0;
}

static ssize_t zip_entry_set(struct zip_t *zip,
                             struct zip_entry_mark_t *entry_mark, ssize_t n,
                             char *const entries[], const size_t len) {
  ssize_t err = 0;

  if ((err = zip_entry_mark(zip, entry_mark, n, entries, len)) < 0) {
    return err;
  }
  if ((err = zip_entry_finalize(zip, entry_mark, n)) < 0) {
    return err;
  }
  return 0;
}

static ssize_t zip_file_move(MZ_FILE *m_pFile, const mz_uint64 to,
                             const mz_uint64 from, const size_t length,
                             mz_uint8 *move_buf, const size_t capacity_size) {
  if (length > capacity_size) {
    return ZIP_ECAPSIZE;
  }
  if (MZ_FSEEK64(m_pFile, from, SEEK_SET)) {
    return ZIP_EFSEEK;
  }
  if (fread(move_buf, 1, length, m_pFile) != length) {
    return ZIP_EFREAD;
  }
  if (MZ_FSEEK64(m_pFile, to, SEEK_SET)) {
    return ZIP_EFSEEK;
  }
  if (fwrite(move_buf, 1, length, m_pFile) != length) {
    return ZIP_EFWRITE;
  }
  return (ssize_t)length;
}

static ssize_t zip_files_move(MZ_FILE *m_pFile, mz_uint64 writen_num,
                              mz_uint64 read_num, size_t length) {
  ssize_t n = 0;
  const size_t page_size = 1 << 12; // 4K
  mz_uint8 *move_buf = (mz_uint8 *)calloc(1, page_size);
  if (!move_buf) {
    return ZIP_EOOMEM;
  }

  ssize_t moved_length = 0;
  ssize_t move_count = 0;
  while ((mz_int64)length > 0) {
    move_count = (length >= page_size) ? page_size : length;
    n = zip_file_move(m_pFile, writen_num, read_num, move_count, move_buf,
                      page_size);
    if (n < 0) {
      moved_length = n;
      goto cleanup;
    }

    if (n != move_count) {
      goto cleanup;
    }

    writen_num += move_count;
    read_num += move_count;
    length -= move_count;
    moved_length += move_count;
  }

cleanup:
  CLEANUP(move_buf);
  return moved_length;
}

static int zip_central_dir_move(mz_zip_internal_state *pState, int begin,
                                int end, int entry_num) {
  if (begin == entry_num) {
    return 0;
  }

  size_t l_size = 0;
  size_t r_size = 0;
  mz_uint32 d_size = 0;
  mz_uint8 *next = NULL;
  mz_uint8 *deleted = &MZ_ZIP_ARRAY_ELEMENT(
      &pState->m_central_dir, mz_uint8,
      MZ_ZIP_ARRAY_ELEMENT(&pState->m_central_dir_offsets, mz_uint32, begin));
  l_size = (size_t)(deleted - (mz_uint8 *)(pState->m_central_dir.m_p));
  if (end == entry_num) {
    r_size = 0;
  } else {
    next = &MZ_ZIP_ARRAY_ELEMENT(
        &pState->m_central_dir, mz_uint8,
        MZ_ZIP_ARRAY_ELEMENT(&pState->m_central_dir_offsets, mz_uint32, end));
    r_size = pState->m_central_dir.m_size -
             (mz_uint32)(next - (mz_uint8 *)(pState->m_central_dir.m_p));
    d_size = (mz_uint32)(next - deleted);
  }

  if (next && l_size == 0) {
    memmove(pState->m_central_dir.m_p, next, r_size);
    pState->m_central_dir.m_p = MZ_REALLOC(pState->m_central_dir.m_p, r_size);
    {
      int i;
      for (i = end; i < entry_num; i++) {
        MZ_ZIP_ARRAY_ELEMENT(&pState->m_central_dir_offsets, mz_uint32, i) -=
            d_size;
      }
    }
  }

  if (next && l_size * r_size != 0) {
    memmove(deleted, next, r_size);
    {
      int i;
      for (i = end; i < entry_num; i++) {
        MZ_ZIP_ARRAY_ELEMENT(&pState->m_central_dir_offsets, mz_uint32, i) -=
            d_size;
      }
    }
  }

  pState->m_central_dir.m_size = l_size + r_size;
  return 0;
}

static int zip_central_dir_delete(mz_zip_internal_state *pState,
                                  int *deleted_entry_index_array,
                                  int entry_num) {
  int i = 0;
  int begin = 0;
  int end = 0;
  int d_num = 0;
  while (i < entry_num) {
    while ((i < entry_num) && (!deleted_entry_index_array[i])) {
      i++;
    }
    begin = i;

    while ((i < entry_num) && (deleted_entry_index_array[i])) {
      i++;
    }
    end = i;
    zip_central_dir_move(pState, begin, end, entry_num);
  }

  i = 0;
  while (i < entry_num) {
    while ((i < entry_num) && (!deleted_entry_index_array[i])) {
      i++;
    }
    begin = i;
    if (begin == entry_num) {
      break;
    }
    while ((i < entry_num) && (deleted_entry_index_array[i])) {
      i++;
    }
    end = i;
    int k = 0, j;
    for (j = end; j < entry_num; j++) {
      MZ_ZIP_ARRAY_ELEMENT(&pState->m_central_dir_offsets, mz_uint32,
                           begin + k) =
          (mz_uint32)MZ_ZIP_ARRAY_ELEMENT(&pState->m_central_dir_offsets,
                                          mz_uint32, j);
      k++;
    }
    d_num += end - begin;
  }

  pState->m_central_dir_offsets.m_size =
      sizeof(mz_uint32) * (entry_num - d_num);
  return 0;
}

static ssize_t zip_entries_delete_mark(struct zip_t *zip,
                                       struct zip_entry_mark_t *entry_mark,
                                       int entry_num) {
  mz_uint64 writen_num = 0;
  mz_uint64 read_num = 0;
  size_t deleted_length = 0;
  size_t move_length = 0;
  int i = 0;
  size_t deleted_entry_num = 0;
  ssize_t n = 0;

  mz_bool *deleted_entry_flag_array =
      (mz_bool *)calloc(entry_num, sizeof(mz_bool));
  if (deleted_entry_flag_array == NULL) {
    return ZIP_EOOMEM;
  }

  mz_zip_internal_state *pState = zip->archive.m_pState;
  zip->archive.m_zip_mode = MZ_ZIP_MODE_WRITING;

  if ((!pState->m_pFile) || MZ_FSEEK64(pState->m_pFile, 0, SEEK_SET)) {
    CLEANUP(deleted_entry_flag_array);
    return ZIP_ENOENT;
  }

  while (i < entry_num) {
    while ((i < entry_num) && (entry_mark[i].type == MZ_KEEP)) {
      writen_num += entry_mark[i].lf_length;
      read_num = writen_num;
      i++;
    }

    while ((i < entry_num) && (entry_mark[i].type == MZ_DELETE)) {
      deleted_entry_flag_array[i] = MZ_TRUE;
      read_num += entry_mark[i].lf_length;
      deleted_length += entry_mark[i].lf_length;
      i++;
      deleted_entry_num++;
    }

    while ((i < entry_num) && (entry_mark[i].type == MZ_MOVE)) {
      move_length += entry_mark[i].lf_length;
      mz_uint8 *p = &MZ_ZIP_ARRAY_ELEMENT(
          &pState->m_central_dir, mz_uint8,
          MZ_ZIP_ARRAY_ELEMENT(&pState->m_central_dir_offsets, mz_uint32, i));
      if (!p) {
        CLEANUP(deleted_entry_flag_array);
        return ZIP_ENOENT;
      }
      mz_uint32 offset = MZ_READ_LE32(p + MZ_ZIP_CDH_LOCAL_HEADER_OFS);
      offset -= (mz_uint32)deleted_length;
      MZ_WRITE_LE32(p + MZ_ZIP_CDH_LOCAL_HEADER_OFS, offset);
      i++;
    }

    n = zip_files_move(pState->m_pFile, writen_num, read_num, move_length);
    if (n != (ssize_t)move_length) {
      CLEANUP(deleted_entry_flag_array);
      return n;
    }
    writen_num += move_length;
    read_num += move_length;
  }

  zip->archive.m_archive_size -= (mz_uint64)deleted_length;
  zip->archive.m_total_files =
      (mz_uint32)entry_num - (mz_uint32)deleted_entry_num;

  zip_central_dir_delete(pState, deleted_entry_flag_array, entry_num);
  CLEANUP(deleted_entry_flag_array);

  return (ssize_t)deleted_entry_num;
}

struct zip_t *zip_open(const char *zipname, int level, char mode) {
  struct zip_t *zip = NULL;

  if (!zipname || strlen(zipname) < 1) {
    // zip_t archive name is empty or NULL
    goto cleanup;
  }

  if (level < 0)
    level = MZ_DEFAULT_LEVEL;
  if ((level & 0xF) > MZ_UBER_COMPRESSION) {
    // Wrong compression level
    goto cleanup;
  }

  zip = (struct zip_t *)calloc((size_t)1, sizeof(struct zip_t));
  if (!zip)
    goto cleanup;

  zip->level = (mz_uint)level;
  switch (mode) {
  case 'w':
    // Create a new archive.
    if (!mz_zip_writer_init_file_v2(&(zip->archive), zipname, 0,
                                    MZ_ZIP_FLAG_WRITE_ZIP64)) {
      // Cannot initialize zip_archive writer
      goto cleanup;
    }
    break;

  case 'r':
    if (!mz_zip_reader_init_file_v2(
            &(zip->archive), zipname,
            zip->level | MZ_ZIP_FLAG_DO_NOT_SORT_CENTRAL_DIRECTORY, 0, 0)) {
      // An archive file does not exist or cannot initialize
      // zip_archive reader
      goto cleanup;
    }
    break;

  case 'a':
  case 'd':
    if (!mz_zip_reader_init_file_v2_rpb(
            &(zip->archive), zipname,
            zip->level | MZ_ZIP_FLAG_DO_NOT_SORT_CENTRAL_DIRECTORY, 0, 0)) {
      // An archive file does not exist or cannot initialize
      // zip_archive reader
      goto cleanup;
    }
    if ((mode == 'a' || mode == 'd')) {
      if (!mz_zip_writer_init_from_reader_v2_noreopen(&(zip->archive), zipname,
                                                      0)) {
        mz_zip_reader_end(&(zip->archive));
        goto cleanup;
      }
    }
    break;

  default:
    goto cleanup;
  }

  return zip;

cleanup:
  CLEANUP(zip);
  return NULL;
}

void zip_close(struct zip_t *zip) {
  if (zip) {
    // Always finalize, even if adding failed for some reason, so we have a
    // valid central directory.
    mz_zip_writer_finalize_archive(&(zip->archive));
    zip_archive_truncate(&(zip->archive));
    mz_zip_writer_end(&(zip->archive));
    mz_zip_reader_end(&(zip->archive));

    CLEANUP(zip);
  }
}

int zip_is64(struct zip_t *zip) {
  if (!zip || !zip->archive.m_pState) {
    // zip_t handler or zip state is not initialized
    return ZIP_ENOINIT;
  }

  return (int)zip->archive.m_pState->m_zip64;
}

static int _zip_entry_open(struct zip_t *zip, const char *entryname,
                           int case_sensitive) {
  size_t entrylen = 0;
  mz_zip_archive *pzip = NULL;
  mz_uint num_alignment_padding_bytes, level;
  mz_zip_archive_file_stat stats;
  int err = 0;
  mz_uint16 dos_time, dos_date;
  mz_uint32 extra_size = 0;
  mz_uint8 extra_data[MZ_ZIP64_MAX_CENTRAL_EXTRA_FIELD_SIZE];
  mz_uint64 local_dir_header_ofs = 0;

  if (!zip) {
    return ZIP_ENOINIT;
  }

  local_dir_header_ofs = zip->archive.m_archive_size;

  if (!entryname) {
    return ZIP_EINVENTNAME;
  }

  entrylen = strlen(entryname);
  if (entrylen == 0) {
    return ZIP_EINVENTNAME;
  }

  /*
    .ZIP File Format Specification Version: 6.3.3

    4.4.17.1 The name of the file, with optional relative path.
    The path stored MUST not contain a drive or
    device letter, or a leading slash.  All slashes
    MUST be forward slashes '/' as opposed to
    backwards slashes '\' for compatibility with Amiga
    and UNIX file systems etc.  If input came from standard
    input, there is no file name field.
  */
  if (zip->entry.name) {
    CLEANUP(zip->entry.name);
  }
#ifdef ZIP_RAW_ENTRYNAME
  zip->entry.name = STRCLONE(entryname);
#else
  zip->entry.name = zip_strrpl(entryname, entrylen, '\\', '/');
#endif

  if (!zip->entry.name) {
    // Cannot parse zip entry name
    return ZIP_EINVENTNAME;
  }

  pzip = &(zip->archive);
  if (pzip->m_zip_mode == MZ_ZIP_MODE_READING) {
    zip->entry.index = (ssize_t)mz_zip_reader_locate_file(
        pzip, zip->entry.name, NULL,
        case_sensitive ? MZ_ZIP_FLAG_CASE_SENSITIVE : 0);
    if (zip->entry.index < (ssize_t)0) {
      err = ZIP_ENOENT;
      goto cleanup;
    }

    if (!mz_zip_reader_file_stat(pzip, (mz_uint)zip->entry.index, &stats)) {
      err = ZIP_ENOENT;
      goto cleanup;
    }

    zip->entry.comp_size = stats.m_comp_size;
    zip->entry.uncomp_size = stats.m_uncomp_size;
    zip->entry.uncomp_crc32 = stats.m_crc32;
    zip->entry.offset = stats.m_central_dir_ofs;
    zip->entry.header_offset = stats.m_local_header_ofs;
    zip->entry.method = stats.m_method;
    zip->entry.external_attr = stats.m_external_attr;
#ifndef MINIZ_NO_TIME
    zip->entry.m_time = stats.m_time;
#endif

    return 0;
  }

  level = zip->level & 0xF;

  zip->entry.index = (ssize_t)zip->archive.m_total_files;
  zip->entry.comp_size = 0;
  zip->entry.uncomp_size = 0;
  zip->entry.uncomp_crc32 = MZ_CRC32_INIT;
  zip->entry.offset = zip->archive.m_archive_size;
  zip->entry.header_offset = zip->archive.m_archive_size;
  memset(zip->entry.header, 0, MZ_ZIP_LOCAL_DIR_HEADER_SIZE * sizeof(mz_uint8));
  zip->entry.method = level ? MZ_DEFLATED : 0;

  // UNIX or APPLE
#if MZ_PLATFORM == 3 || MZ_PLATFORM == 19
  // regular file with rw-r--r-- permissions
  zip->entry.external_attr = (mz_uint32)(0100644) << 16;
#else
  zip->entry.external_attr = 0;
#endif

  num_alignment_padding_bytes =
      mz_zip_writer_compute_padding_needed_for_file_alignment(pzip);

  if (!pzip->m_pState || (pzip->m_zip_mode != MZ_ZIP_MODE_WRITING)) {
    // Invalid zip mode
    err = ZIP_EINVMODE;
    goto cleanup;
  }
  if (zip->level & MZ_ZIP_FLAG_COMPRESSED_DATA) {
    // Invalid zip compression level
    err = ZIP_EINVLVL;
    goto cleanup;
  }

  if (!mz_zip_writer_write_zeros(pzip, zip->entry.offset,
                                 num_alignment_padding_bytes)) {
    // Cannot memset zip entry header
    err = ZIP_EMEMSET;
    goto cleanup;
  }
  local_dir_header_ofs += num_alignment_padding_bytes;

  zip->entry.m_time = time(NULL);
#ifndef MINIZ_NO_TIME
  mz_zip_time_t_to_dos_time(zip->entry.m_time, &dos_time, &dos_date);
#endif

  // ZIP64 header with NULL sizes (sizes will be in the data descriptor, just
  // after file data)
  extra_size = mz_zip_writer_create_zip64_extra_data(
      extra_data, NULL, NULL,
      (local_dir_header_ofs >= MZ_UINT32_MAX) ? &local_dir_header_ofs : NULL);

  if (!mz_zip_writer_create_local_dir_header(
          pzip, zip->entry.header, entrylen, (mz_uint16)extra_size, 0, 0, 0,
          zip->entry.method,
          MZ_ZIP_GENERAL_PURPOSE_BIT_FLAG_UTF8 |
              MZ_ZIP_LDH_BIT_FLAG_HAS_LOCATOR,
          dos_time, dos_date)) {
    // Cannot create zip entry header
    err = ZIP_EMEMSET;
    goto cleanup;
  }

  zip->entry.header_offset = zip->entry.offset + num_alignment_padding_bytes;

  if (pzip->m_pWrite(pzip->m_pIO_opaque, zip->entry.header_offset,
                     zip->entry.header,
                     sizeof(zip->entry.header)) != sizeof(zip->entry.header)) {
    // Cannot write zip entry header
    err = ZIP_EMEMSET;
    goto cleanup;
  }

  if (pzip->m_file_offset_alignment) {
    MZ_ASSERT(
        (zip->entry.header_offset & (pzip->m_file_offset_alignment - 1)) == 0);
  }
  zip->entry.offset += num_alignment_padding_bytes + sizeof(zip->entry.header);

  if (pzip->m_pWrite(pzip->m_pIO_opaque, zip->entry.offset, zip->entry.name,
                     entrylen) != entrylen) {
    // Cannot write data to zip entry
    err = ZIP_EWRTENT;
    goto cleanup;
  }

  zip->entry.offset += entrylen;

  if (pzip->m_pWrite(pzip->m_pIO_opaque, zip->entry.offset, extra_data,
                     extra_size) != extra_size) {
    // Cannot write ZIP64 data to zip entry
    err = ZIP_EWRTENT;
    goto cleanup;
  }
  zip->entry.offset += extra_size;

  if (level) {
    zip->entry.state.m_pZip = pzip;
    zip->entry.state.m_cur_archive_file_ofs = zip->entry.offset;
    zip->entry.state.m_comp_size = 0;

    if (tdefl_init(&(zip->entry.comp), mz_zip_writer_add_put_buf_callback,
                   &(zip->entry.state),
                   (int)tdefl_create_comp_flags_from_zip_params(
                       (int)level, -15, MZ_DEFAULT_STRATEGY)) !=
        TDEFL_STATUS_OKAY) {
      // Cannot initialize the zip compressor
      err = ZIP_ETDEFLINIT;
      goto cleanup;
    }
  }

  return 0;

cleanup:
  CLEANUP(zip->entry.name);
  return err;
}

int zip_entry_open(struct zip_t *zip, const char *entryname) {
  return _zip_entry_open(zip, entryname, 0);
}

int zip_entry_opencasesensitive(struct zip_t *zip, const char *entryname) {
  return _zip_entry_open(zip, entryname, 1);
}

int zip_entry_openbyindex(struct zip_t *zip, size_t index) {
  mz_zip_archive *pZip = NULL;
  mz_zip_archive_file_stat stats;
  mz_uint namelen;
  const mz_uint8 *pHeader;
  const char *pFilename;

  if (!zip) {
    // zip_t handler is not initialized
    return ZIP_ENOINIT;
  }

  pZip = &(zip->archive);
  if (pZip->m_zip_mode != MZ_ZIP_MODE_READING) {
    // open by index requires readonly mode
    return ZIP_EINVMODE;
  }

  if (index >= (size_t)pZip->m_total_files) {
    // index out of range
    return ZIP_EINVIDX;
  }

  if (!(pHeader = &MZ_ZIP_ARRAY_ELEMENT(
            &pZip->m_pState->m_central_dir, mz_uint8,
            MZ_ZIP_ARRAY_ELEMENT(&pZip->m_pState->m_central_dir_offsets,
                                 mz_uint32, index)))) {
    // cannot find header in central directory
    return ZIP_ENOHDR;
  }

  namelen = MZ_READ_LE16(pHeader + MZ_ZIP_CDH_FILENAME_LEN_OFS);
  pFilename = (const char *)pHeader + MZ_ZIP_CENTRAL_DIR_HEADER_SIZE;

  /*
    .ZIP File Format Specification Version: 6.3.3

    4.4.17.1 The name of the file, with optional relative path.
    The path stored MUST not contain a drive or
    device letter, or a leading slash.  All slashes
    MUST be forward slashes '/' as opposed to
    backwards slashes '\' for compatibility with Amiga
    and UNIX file systems etc.  If input came from standard
    input, there is no file name field.
  */
  if (zip->entry.name) {
    CLEANUP(zip->entry.name);
  }
#ifdef ZIP_RAW_ENTRYNAME
  zip->entry.name = STRCLONE(pFilename);
#else
  zip->entry.name = zip_strrpl(pFilename, namelen, '\\', '/');
#endif

  if (!zip->entry.name) {
    // local entry name is NULL
    return ZIP_EINVENTNAME;
  }

  if (!mz_zip_reader_file_stat(pZip, (mz_uint)index, &stats)) {
    return ZIP_ENOENT;
  }

  zip->entry.index = (ssize_t)index;
  zip->entry.comp_size = stats.m_comp_size;
  zip->entry.uncomp_size = stats.m_uncomp_size;
  zip->entry.uncomp_crc32 = stats.m_crc32;
  zip->entry.offset = stats.m_central_dir_ofs;
  zip->entry.header_offset = stats.m_local_header_ofs;
  zip->entry.method = stats.m_method;
  zip->entry.external_attr = stats.m_external_attr;
#ifndef MINIZ_NO_TIME
  zip->entry.m_time = stats.m_time;
#endif

  return 0;
}

int zip_entry_close(struct zip_t *zip) {
  mz_zip_archive *pzip = NULL;
  mz_uint level;
  tdefl_status done;
  mz_uint16 entrylen;
  mz_uint16 dos_time = 0, dos_date = 0;
  int err = 0;
  mz_uint8 *pExtra_data = NULL;
  mz_uint32 extra_size = 0;
  mz_uint8 extra_data[MZ_ZIP64_MAX_CENTRAL_EXTRA_FIELD_SIZE];
  mz_uint8 local_dir_footer[MZ_ZIP_DATA_DESCRIPTER_SIZE64];
  mz_uint32 local_dir_footer_size = MZ_ZIP_DATA_DESCRIPTER_SIZE64;

  if (!zip) {
    // zip_t handler is not initialized
    err = ZIP_ENOINIT;
    goto cleanup;
  }

  pzip = &(zip->archive);
  if (pzip->m_zip_mode == MZ_ZIP_MODE_READING) {
    goto cleanup;
  }

  level = zip->level & 0xF;
  if (level) {
    done = tdefl_compress_buffer(&(zip->entry.comp), "", 0, TDEFL_FINISH);
    if (done != TDEFL_STATUS_DONE && done != TDEFL_STATUS_OKAY) {
      // Cannot flush compressed buffer
      err = ZIP_ETDEFLBUF;
      goto cleanup;
    }
    zip->entry.comp_size = zip->entry.state.m_comp_size;
    zip->entry.offset = zip->entry.state.m_cur_archive_file_ofs;
    zip->entry.method = MZ_DEFLATED;
  }

  entrylen = (mz_uint16)strlen(zip->entry.name);
#ifndef MINIZ_NO_TIME
  mz_zip_time_t_to_dos_time(zip->entry.m_time, &dos_time, &dos_date);
#endif

  MZ_WRITE_LE32(local_dir_footer + 0, MZ_ZIP_DATA_DESCRIPTOR_ID);
  MZ_WRITE_LE32(local_dir_footer + 4, zip->entry.uncomp_crc32);
  MZ_WRITE_LE64(local_dir_footer + 8, zip->entry.comp_size);
  MZ_WRITE_LE64(local_dir_footer + 16, zip->entry.uncomp_size);

  if (pzip->m_pWrite(pzip->m_pIO_opaque, zip->entry.offset, local_dir_footer,
                     local_dir_footer_size) != local_dir_footer_size) {
    // Cannot write zip entry header
    err = ZIP_EWRTHDR;
    goto cleanup;
  }
  zip->entry.offset += local_dir_footer_size;

  pExtra_data = extra_data;
  extra_size = mz_zip_writer_create_zip64_extra_data(
      extra_data,
      (zip->entry.uncomp_size >= MZ_UINT32_MAX) ? &zip->entry.uncomp_size
                                                : NULL,
      (zip->entry.comp_size >= MZ_UINT32_MAX) ? &zip->entry.comp_size : NULL,
      (zip->entry.header_offset >= MZ_UINT32_MAX) ? &zip->entry.header_offset
                                                  : NULL);

  if ((entrylen) && (zip->entry.name[entrylen - 1] == '/') &&
      !zip->entry.uncomp_size) {
    /* Set DOS Subdirectory attribute bit. */
    zip->entry.external_attr |= MZ_ZIP_DOS_DIR_ATTRIBUTE_BITFLAG;
  }

  if (!mz_zip_writer_add_to_central_dir(
          pzip, zip->entry.name, entrylen, pExtra_data, (mz_uint16)extra_size,
          "", 0, zip->entry.uncomp_size, zip->entry.comp_size,
          zip->entry.uncomp_crc32, zip->entry.method,
          MZ_ZIP_GENERAL_PURPOSE_BIT_FLAG_UTF8 |
              MZ_ZIP_LDH_BIT_FLAG_HAS_LOCATOR,
          dos_time, dos_date, zip->entry.header_offset,
          zip->entry.external_attr, NULL, 0)) {
    // Cannot write to zip central dir
    err = ZIP_EWRTDIR;
    goto cleanup;
  }

  pzip->m_total_files++;
  pzip->m_archive_size = zip->entry.offset;

cleanup:
  if (zip) {
    zip->entry.m_time = 0;
    CLEANUP(zip->entry.name);
  }
  return err;
}

const char *zip_entry_name(struct zip_t *zip) {
  if (!zip) {
    // zip_t handler is not initialized
    return NULL;
  }

  return zip->entry.name;
}

ssize_t zip_entry_index(struct zip_t *zip) {
  if (!zip) {
    // zip_t handler is not initialized
    return (ssize_t)ZIP_ENOINIT;
  }

  return zip->entry.index;
}

int zip_entry_isdir(struct zip_t *zip) {
  if (!zip) {
    // zip_t handler is not initialized
    return ZIP_ENOINIT;
  }

  if (zip->entry.index < (ssize_t)0) {
    // zip entry is not opened
    return ZIP_EINVIDX;
  }

  return (int)mz_zip_reader_is_file_a_directory(&zip->archive,
                                                (mz_uint)zip->entry.index);
}

unsigned long long zip_entry_size(struct zip_t *zip) {
  return zip_entry_uncomp_size(zip);
}

unsigned long long zip_entry_uncomp_size(struct zip_t *zip) {
  return zip ? zip->entry.uncomp_size : 0;
}

unsigned long long zip_entry_comp_size(struct zip_t *zip) {
  return zip ? zip->entry.comp_size : 0;
}

unsigned int zip_entry_crc32(struct zip_t *zip) {
  return zip ? zip->entry.uncomp_crc32 : 0;
}

int zip_entry_write(struct zip_t *zip, const void *buf, size_t bufsize) {
  mz_uint level;
  mz_zip_archive *pzip = NULL;
  tdefl_status status;

  if (!zip) {
    // zip_t handler is not initialized
    return ZIP_ENOINIT;
  }

  pzip = &(zip->archive);
  if (buf && bufsize > 0) {
    zip->entry.uncomp_size += bufsize;
    zip->entry.uncomp_crc32 = (mz_uint32)mz_crc32(
        zip->entry.uncomp_crc32, (const mz_uint8 *)buf, bufsize);

    level = zip->level & 0xF;
    if (!level) {
      if ((pzip->m_pWrite(pzip->m_pIO_opaque, zip->entry.offset, buf,
                          bufsize) != bufsize)) {
        // Cannot write buffer
        return ZIP_EWRTENT;
      }
      zip->entry.offset += bufsize;
      zip->entry.comp_size += bufsize;
    } else {
      status = tdefl_compress_buffer(&(zip->entry.comp), buf, bufsize,
                                     TDEFL_NO_FLUSH);
      if (status != TDEFL_STATUS_DONE && status != TDEFL_STATUS_OKAY) {
        // Cannot compress buffer
        return ZIP_ETDEFLBUF;
      }
    }
  }

  return 0;
}

int zip_entry_fwrite(struct zip_t *zip, const char *filename) {
  int err = 0;
  size_t n = 0;
  MZ_FILE *stream = NULL;
  mz_uint8 buf[MZ_ZIP_MAX_IO_BUF_SIZE];
  struct MZ_FILE_STAT_STRUCT file_stat;
  mz_uint16 modes;

  if (!zip) {
    // zip_t handler is not initialized
    return ZIP_ENOINIT;
  }

  memset(buf, 0, MZ_ZIP_MAX_IO_BUF_SIZE);
  memset((void *)&file_stat, 0, sizeof(struct MZ_FILE_STAT_STRUCT));
  if (MZ_FILE_STAT(filename, &file_stat) != 0) {
    // problem getting information - check errno
    return ZIP_ENOENT;
  }

#if defined(_WIN32) || defined(__WIN32__) || defined(DJGPP)
  (void)modes; // unused
#else
  /* Initialize with permission bits--which are not implementation-optional */
  modes = file_stat.st_mode &
          (S_IRWXU | S_IRWXG | S_IRWXO | S_ISUID | S_ISGID | S_ISVTX);
  if (S_ISDIR(file_stat.st_mode))
    modes |= UNX_IFDIR;
  if (S_ISREG(file_stat.st_mode))
    modes |= UNX_IFREG;
  if (S_ISLNK(file_stat.st_mode))
    modes |= UNX_IFLNK;
  if (S_ISBLK(file_stat.st_mode))
    modes |= UNX_IFBLK;
  if (S_ISCHR(file_stat.st_mode))
    modes |= UNX_IFCHR;
  if (S_ISFIFO(file_stat.st_mode))
    modes |= UNX_IFIFO;
  if (S_ISSOCK(file_stat.st_mode))
    modes |= UNX_IFSOCK;
  zip->entry.external_attr = (modes << 16) | !(file_stat.st_mode & S_IWUSR);
  if ((file_stat.st_mode & S_IFMT) == S_IFDIR) {
    zip->entry.external_attr |= MZ_ZIP_DOS_DIR_ATTRIBUTE_BITFLAG;
  }
#endif

  zip->entry.m_time = file_stat.st_mtime;

  if (!(stream = MZ_FOPEN(filename, "rb"))) {
    // Cannot open filename
    return ZIP_EOPNFILE;
  }

  while ((n = fread(buf, sizeof(mz_uint8), MZ_ZIP_MAX_IO_BUF_SIZE, stream)) >
         0) {
    if (zip_entry_write(zip, buf, n) < 0) {
      err = ZIP_EWRTENT;
      break;
    }
  }
  fclose(stream);

  return err;
}

ssize_t zip_entry_read(struct zip_t *zip, void **buf, size_t *bufsize) {
  mz_zip_archive *pzip = NULL;
  mz_uint idx;
  size_t size = 0;

  if (!zip) {
    // zip_t handler is not initialized
    return (ssize_t)ZIP_ENOINIT;
  }

  pzip = &(zip->archive);
  if (pzip->m_zip_mode != MZ_ZIP_MODE_READING ||
      zip->entry.index < (ssize_t)0) {
    // the entry is not found or we do not have read access
    return (ssize_t)ZIP_ENOENT;
  }

  idx = (mz_uint)zip->entry.index;
  if (mz_zip_reader_is_file_a_directory(pzip, idx)) {
    // the entry is a directory
    return (ssize_t)ZIP_EINVENTTYPE;
  }

  *buf = mz_zip_reader_extract_to_heap(pzip, idx, &size, 0);
  if (*buf && bufsize) {
    *bufsize = size;
  }
  return (ssize_t)size;
}

ssize_t zip_entry_noallocread(struct zip_t *zip, void *buf, size_t bufsize) {
  mz_zip_archive *pzip = NULL;

  if (!zip) {
    // zip_t handler is not initialized
    return (ssize_t)ZIP_ENOINIT;
  }

  pzip = &(zip->archive);
  if (pzip->m_zip_mode != MZ_ZIP_MODE_READING ||
      zip->entry.index < (ssize_t)0) {
    // the entry is not found or we do not have read access
    return (ssize_t)ZIP_ENOENT;
  }

  if (!mz_zip_reader_extract_to_mem_no_alloc(pzip, (mz_uint)zip->entry.index,
                                             buf, bufsize, 0, NULL, 0)) {
    return (ssize_t)ZIP_EMEMNOALLOC;
  }

  return (ssize_t)zip->entry.uncomp_size;
}

int zip_entry_fread(struct zip_t *zip, const char *filename) {
  mz_zip_archive *pzip = NULL;
  mz_uint idx;
  mz_uint32 xattr = 0;
  mz_zip_archive_file_stat info;

  if (!zip) {
    // zip_t handler is not initialized
    return ZIP_ENOINIT;
  }

  memset((void *)&info, 0, sizeof(mz_zip_archive_file_stat));
  pzip = &(zip->archive);
  if (pzip->m_zip_mode != MZ_ZIP_MODE_READING ||
      zip->entry.index < (ssize_t)0) {
    // the entry is not found or we do not have read access
    return ZIP_ENOENT;
  }

  idx = (mz_uint)zip->entry.index;
  if (mz_zip_reader_is_file_a_directory(pzip, idx)) {
    // the entry is a directory
    return ZIP_EINVENTTYPE;
  }

  if (!mz_zip_reader_extract_to_file(pzip, idx, filename, 0)) {
    return ZIP_ENOFILE;
  }

#if defined(_MSC_VER) || defined(PS4)
  (void)xattr; // unused
#else
  if (!mz_zip_reader_file_stat(pzip, idx, &info)) {
    // Cannot get information about zip archive;
    return ZIP_ENOFILE;
  }

  xattr = (info.m_external_attr >> 16) & 0xFFFF;
  if (xattr > 0 && xattr <= MZ_UINT16_MAX) {
    if (CHMOD(filename, (mode_t)xattr) < 0) {
      return ZIP_ENOPERM;
    }
  }
#endif

  return 0;
}

int zip_entry_extract(struct zip_t *zip,
                      size_t (*on_extract)(void *arg, uint64_t offset,
                                           const void *buf, size_t bufsize),
                      void *arg) {
  mz_zip_archive *pzip = NULL;
  mz_uint idx;

  if (!zip) {
    // zip_t handler is not initialized
    return ZIP_ENOINIT;
  }

  pzip = &(zip->archive);
  if (pzip->m_zip_mode != MZ_ZIP_MODE_READING ||
      zip->entry.index < (ssize_t)0) {
    // the entry is not found or we do not have read access
    return ZIP_ENOENT;
  }

  idx = (mz_uint)zip->entry.index;
  return (mz_zip_reader_extract_to_callback(pzip, idx, on_extract, arg, 0))
             ? 0
             : ZIP_EINVIDX;
}

ssize_t zip_entries_total(struct zip_t *zip) {
  if (!zip) {
    // zip_t handler is not initialized
    return ZIP_ENOINIT;
  }

  return (ssize_t)zip->archive.m_total_files;
}

ssize_t zip_entries_delete(struct zip_t *zip, char *const entries[],
                           size_t len) {
  ssize_t n = 0;
  ssize_t err = 0;
  struct zip_entry_mark_t *entry_mark = NULL;

  if (zip == NULL || (entries == NULL && len != 0)) {
    return ZIP_ENOINIT;
  }

  if (entries == NULL && len == 0) {
    return 0;
  }

  n = zip_entries_total(zip);

  entry_mark = (struct zip_entry_mark_t *)calloc(
      (size_t)n, sizeof(struct zip_entry_mark_t));
  if (!entry_mark) {
    return ZIP_EOOMEM;
  }

  zip->archive.m_zip_mode = MZ_ZIP_MODE_READING;

  err = zip_entry_set(zip, entry_mark, n, entries, len);
  if (err < 0) {
    CLEANUP(entry_mark);
    return err;
  }

  err = zip_entries_delete_mark(zip, entry_mark, (int)n);
  CLEANUP(entry_mark);
  return err;
}

int zip_stream_extract(const char *stream, size_t size, const char *dir,
                       int (*on_extract)(const char *filename, void *arg),
                       void *arg) {
  mz_zip_archive zip_archive;
  if (!stream || !dir) {
    // Cannot parse zip archive stream
    return ZIP_ENOINIT;
  }
  if (!memset(&zip_archive, 0, sizeof(mz_zip_archive))) {
    // Cannot memset zip archive
    return ZIP_EMEMSET;
  }
  if (!mz_zip_reader_init_mem(&zip_archive, stream, size, 0)) {
    // Cannot initialize zip_archive reader
    return ZIP_ENOINIT;
  }

  return zip_archive_extract(&zip_archive, dir, on_extract, arg);
}

struct zip_t *zip_stream_open(const char *stream, size_t size, int level,
                              char mode) {
  struct zip_t *zip = (struct zip_t *)calloc((size_t)1, sizeof(struct zip_t));
  if (!zip) {
    return NULL;
  }

  if (level < 0) {
    level = MZ_DEFAULT_LEVEL;
  }
  if ((level & 0xF) > MZ_UBER_COMPRESSION) {
    // Wrong compression level
    goto cleanup;
  }
  zip->level = (mz_uint)level;

  if ((stream != NULL) && (size > 0) && (mode == 'r')) {
    if (!mz_zip_reader_init_mem(&(zip->archive), stream, size, 0)) {
      goto cleanup;
    }
  } else if ((stream == NULL) && (size == 0) && (mode == 'w')) {
    // Create a new archive.
    if (!mz_zip_writer_init_heap(&(zip->archive), 0, 1024)) {
      // Cannot initialize zip_archive writer
      goto cleanup;
    }
  } else {
    goto cleanup;
  }
  return zip;

cleanup:
  CLEANUP(zip);
  return NULL;
}

ssize_t zip_stream_copy(struct zip_t *zip, void **buf, size_t *bufsize) {
  size_t n;

  if (!zip) {
    return (ssize_t)ZIP_ENOINIT;
  }
  zip_archive_finalize(&(zip->archive));

  n = (size_t)zip->archive.m_archive_size;
  if (bufsize != NULL) {
    *bufsize = n;
  }

  *buf = calloc(sizeof(unsigned char), n);
  memcpy(*buf, zip->archive.m_pState->m_pMem, n);

  return (ssize_t)n;
}

void zip_stream_close(struct zip_t *zip) {
  if (zip) {
    mz_zip_writer_end(&(zip->archive));
    mz_zip_reader_end(&(zip->archive));
    CLEANUP(zip);
  }
}

int zip_create(const char *zipname, const char *filenames[], size_t len) {
  int err = 0;
  size_t i;
  mz_zip_archive zip_archive;
  struct MZ_FILE_STAT_STRUCT file_stat;
  mz_uint32 ext_attributes = 0;
  mz_uint16 modes;

  if (!zipname || strlen(zipname) < 1) {
    // zip_t archive name is empty or NULL
    return ZIP_EINVZIPNAME;
  }

  // Create a new archive.
  if (!memset(&(zip_archive), 0, sizeof(zip_archive))) {
    // Cannot memset zip archive
    return ZIP_EMEMSET;
  }

  if (!mz_zip_writer_init_file(&zip_archive, zipname, 0)) {
    // Cannot initialize zip_archive writer
    return ZIP_ENOINIT;
  }

  if (!memset((void *)&file_stat, 0, sizeof(struct MZ_FILE_STAT_STRUCT))) {
    return ZIP_EMEMSET;
  }

  for (i = 0; i < len; ++i) {
    const char *name = filenames[i];
    if (!name) {
      err = ZIP_EINVENTNAME;
      break;
    }

    if (MZ_FILE_STAT(name, &file_stat) != 0) {
      // problem getting information - check errno
      err = ZIP_ENOFILE;
      break;
    }

#if defined(_WIN32) || defined(__WIN32__) || defined(DJGPP)
    (void)modes; // unused
#else

    /* Initialize with permission bits--which are not implementation-optional */
    modes = file_stat.st_mode &
            (S_IRWXU | S_IRWXG | S_IRWXO | S_ISUID | S_ISGID | S_ISVTX);
    if (S_ISDIR(file_stat.st_mode))
      modes |= UNX_IFDIR;
    if (S_ISREG(file_stat.st_mode))
      modes |= UNX_IFREG;
    if (S_ISLNK(file_stat.st_mode))
      modes |= UNX_IFLNK;
    if (S_ISBLK(file_stat.st_mode))
      modes |= UNX_IFBLK;
    if (S_ISCHR(file_stat.st_mode))
      modes |= UNX_IFCHR;
    if (S_ISFIFO(file_stat.st_mode))
      modes |= UNX_IFIFO;
    if (S_ISSOCK(file_stat.st_mode))
      modes |= UNX_IFSOCK;
    ext_attributes = (modes << 16) | !(file_stat.st_mode & S_IWUSR);
    if ((file_stat.st_mode & S_IFMT) == S_IFDIR) {
      ext_attributes |= MZ_ZIP_DOS_DIR_ATTRIBUTE_BITFLAG;
    }
#endif

    if (!mz_zip_writer_add_file(&zip_archive, zip_basename(name), name, "", 0,
                                ZIP_DEFAULT_COMPRESSION_LEVEL,
                                ext_attributes)) {
      // Cannot add file to zip_archive
      err = ZIP_ENOFILE;
      break;
    }
  }

  mz_zip_writer_finalize_archive(&zip_archive);
  mz_zip_writer_end(&zip_archive);
  return err;
}

int zip_extract(const char *zipname, const char *dir,
                int (*on_extract)(const char *filename, void *arg), void *arg) {
  mz_zip_archive zip_archive;

  if (!zipname || !dir) {
    // Cannot parse zip archive name
    return ZIP_EINVZIPNAME;
  }

  if (!memset(&zip_archive, 0, sizeof(mz_zip_archive))) {
    // Cannot memset zip archive
    return ZIP_EMEMSET;
  }

  // Now try to open the archive.
  if (!mz_zip_reader_init_file(&zip_archive, zipname, 0)) {
    // Cannot initialize zip_archive reader
    return ZIP_ENOINIT;
  }

  return zip_archive_extract(&zip_archive, dir, on_extract, arg);
}
