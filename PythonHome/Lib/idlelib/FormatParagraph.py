"""Extension to format a paragraph or selection to a max width.

Does basic, standard text formatting, and also understands Python
comment blocks. Thus, for editing Python source code, this
extension is really only suitable for reformatting these comment
blocks or triple-quoted strings.

Known problems with comment reformatting:
* If there is a selection marked, and the first line of the
  selection is not complete, the block will probably not be detected
  as comments, and will have the normal "text formatting" rules
  applied.
* If a comment block has leading whitespace that mixes tabs and
  spaces, they will not be considered part of the same block.
* Fancy comments, like this bulleted list, aren't handled :-)
"""

import re
from idlelib.configHandler import idleConf

class FormatParagraph:

    menudefs = [
        ('format', [   # /s/edit/format   dscherer@cmu.edu
            ('Format Paragraph', '<<format-paragraph>>'),
         ])
    ]

    def __init__(self, editwin):
        self.editwin = editwin

    def close(self):
        self.editwin = None

    def format_paragraph_event(self, event, limit=None):
        """Formats paragraph to a max width specified in idleConf.

        If text is selected, format_paragraph_event will start breaking lines
        at the max width, starting from the beginning selection.

        If no text is selected, format_paragraph_event uses the current
        cursor location to determine the paragraph (lines of text surrounded
        by blank lines) and formats it.

        The length limit parameter is for testing with a known value.
        """
        if limit == None:
            limit = idleConf.GetOption(
                    'main', 'FormatParagraph', 'paragraph', type='int')
        text = self.editwin.text
        first, last = self.editwin.get_selection_indices()
        if first and last:
            data = text.get(first, last)
            comment_header = get_comment_header(data)
        else:
            first, last, comment_header, data = \
                    find_paragraph(text, text.index("insert"))
        if comment_header:
            newdata = reformat_comment(data, limit, comment_header)
        else:
            newdata = reformat_paragraph(data, limit)
        text.tag_remove("sel", "1.0", "end")

        if newdata != data:
            text.mark_set("insert", first)
            text.undo_block_start()
            text.delete(first, last)
            text.insert(first, newdata)
            text.undo_block_stop()
        else:
            text.mark_set("insert", last)
        text.see("insert")
        return "break"

def find_paragraph(text, mark):
    """Returns the start/stop indices enclosing the paragraph that mark is in.

    Also returns the comment format string, if any, and paragraph of text
    between the start/stop indices.
    """
    lineno, col = map(int, mark.split("."))
    line = text.get("%d.0" % lineno, "%d.end" % lineno)

    # Look for start of next paragraph if the index passed in is a blank line
    while text.compare("%d.0" % lineno, "<", "end") and is_all_white(line):
        lineno = lineno + 1
        line = text.get("%d.0" % lineno, "%d.end" % lineno)
    first_lineno = lineno
    comment_header = get_comment_header(line)
    comment_header_len = len(comment_header)

    # Once start line found, search for end of paragraph (a blank line)
    while get_comment_header(line)==comment_header and \
              not is_all_white(line[comment_header_len:]):
        lineno = lineno + 1
        line = text.get("%d.0" % lineno, "%d.end" % lineno)
    last = "%d.0" % lineno

    # Search back to beginning of paragraph (first blank line before)
    lineno = first_lineno - 1
    line = text.get("%d.0" % lineno, "%d.end" % lineno)
    while lineno > 0 and \
              get_comment_header(line)==comment_header and \
              not is_all_white(line[comment_header_len:]):
        lineno = lineno - 1
        line = text.get("%d.0" % lineno, "%d.end" % lineno)
    first = "%d.0" % (lineno+1)

    return first, last, comment_header, text.get(first, last)

# This should perhaps be replaced with textwrap.wrap
def reformat_paragraph(data, limit):
    """Return data reformatted to specified width (limit)."""
    lines = data.split("\n")
    i = 0
    n = len(lines)
    while i < n and is_all_white(lines[i]):
        i = i+1
    if i >= n:
        return data
    indent1 = get_indent(lines[i])
    if i+1 < n and not is_all_white(lines[i+1]):
        indent2 = get_indent(lines[i+1])
    else:
        indent2 = indent1
    new = lines[:i]
    partial = indent1
    while i < n and not is_all_white(lines[i]):
        # XXX Should take double space after period (etc.) into account
        words = re.split("(\s+)", lines[i])
        for j in range(0, len(words), 2):
            word = words[j]
            if not word:
                continue # Can happen when line ends in whitespace
            if len((partial + word).expandtabs()) > limit and \
                   partial != indent1:
                new.append(partial.rstrip())
                partial = indent2
            partial = partial + word + " "
            if j+1 < len(words) and words[j+1] != " ":
                partial = partial + " "
        i = i+1
    new.append(partial.rstrip())
    # XXX Should reformat remaining paragraphs as well
    new.extend(lines[i:])
    return "\n".join(new)

def reformat_comment(data, limit, comment_header):
    """Return data reformatted to specified width with comment header."""

    # Remove header from the comment lines
    lc = len(comment_header)
    data = "\n".join(line[lc:] for line in data.split("\n"))
    # Reformat to maxformatwidth chars or a 20 char width,
    # whichever is greater.
    format_width = max(limit - len(comment_header), 20)
    newdata = reformat_paragraph(data, format_width)
    # re-split and re-insert the comment header.
    newdata = newdata.split("\n")
    # If the block ends in a \n, we dont want the comment prefix
    # inserted after it. (Im not sure it makes sense to reformat a
    # comment block that is not made of complete lines, but whatever!)
    # Can't think of a clean solution, so we hack away
    block_suffix = ""
    if not newdata[-1]:
        block_suffix = "\n"
        newdata = newdata[:-1]
    return '\n'.join(comment_header+line for line in newdata) + block_suffix

def is_all_white(line):
    """Return True if line is empty or all whitespace."""

    return re.match(r"^\s*$", line) is not None

def get_indent(line):
    """Return the initial space or tab indent of line."""
    return re.match(r"^([ \t]*)", line).group()

def get_comment_header(line):
    """Return string with leading whitespace and '#' from line or ''.

    A null return indicates that the line is not a comment line. A non-
    null return, such as '    #', will be used to find the other lines of
    a comment block with the same  indent.
    """
    m = re.match(r"^([ \t]*#*)", line)
    if m is None: return ""
    return m.group(1)

if __name__ == "__main__":
    import unittest
    unittest.main('idlelib.idle_test.test_formatparagraph',
            verbosity=2, exit=False)
