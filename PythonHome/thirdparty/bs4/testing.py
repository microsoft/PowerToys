"""Helper classes for tests."""

import copy
import functools
import unittest
from unittest import TestCase
from bs4 import BeautifulSoup
from bs4.element import (
    CharsetMetaAttributeValue,
    Comment,
    ContentMetaAttributeValue,
    Doctype,
    SoupStrainer,
)

from bs4.builder import HTMLParserTreeBuilder
default_builder = HTMLParserTreeBuilder


class SoupTest(unittest.TestCase):

    @property
    def default_builder(self):
        return default_builder()

    def soup(self, markup, **kwargs):
        """Build a Beautiful Soup object from markup."""
        builder = kwargs.pop('builder', self.default_builder)
        return BeautifulSoup(markup, builder=builder, **kwargs)

    def document_for(self, markup):
        """Turn an HTML fragment into a document.

        The details depend on the builder.
        """
        return self.default_builder.test_fragment_to_document(markup)

    def assertSoupEquals(self, to_parse, compare_parsed_to=None):
        builder = self.default_builder
        obj = BeautifulSoup(to_parse, builder=builder)
        if compare_parsed_to is None:
            compare_parsed_to = to_parse

        self.assertEqual(obj.decode(), self.document_for(compare_parsed_to))


class HTMLTreeBuilderSmokeTest(object):

    """A basic test of a treebuilder's competence.

    Any HTML treebuilder, present or future, should be able to pass
    these tests. With invalid markup, there's room for interpretation,
    and different parsers can handle it differently. But with the
    markup in these tests, there's not much room for interpretation.
    """

    def assertDoctypeHandled(self, doctype_fragment):
        """Assert that a given doctype string is handled correctly."""
        doctype_str, soup = self._document_with_doctype(doctype_fragment)

        # Make sure a Doctype object was created.
        doctype = soup.contents[0]
        self.assertEqual(doctype.__class__, Doctype)
        self.assertEqual(doctype, doctype_fragment)
        self.assertEqual(str(soup)[:len(doctype_str)], doctype_str)

        # Make sure that the doctype was correctly associated with the
        # parse tree and that the rest of the document parsed.
        self.assertEqual(soup.p.contents[0], 'foo')

    def _document_with_doctype(self, doctype_fragment):
        """Generate and parse a document with the given doctype."""
        doctype = '<!DOCTYPE %s>' % doctype_fragment
        markup = doctype + '\n<p>foo</p>'
        soup = self.soup(markup)
        return doctype, soup

    def test_normal_doctypes(self):
        """Make sure normal, everyday HTML doctypes are handled correctly."""
        self.assertDoctypeHandled("html")
        self.assertDoctypeHandled(
            'html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN"')

    def test_empty_doctype(self):
        soup = self.soup("<!DOCTYPE>")
        doctype = soup.contents[0]
        self.assertEqual("", doctype.strip())

    def test_public_doctype_with_url(self):
        doctype = 'html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"'
        self.assertDoctypeHandled(doctype)

    def test_system_doctype(self):
        self.assertDoctypeHandled('foo SYSTEM "http://www.example.com/"')

    def test_namespaced_system_doctype(self):
        # We can handle a namespaced doctype with a system ID.
        self.assertDoctypeHandled('xsl:stylesheet SYSTEM "htmlent.dtd"')

    def test_namespaced_public_doctype(self):
        # Test a namespaced doctype with a public id.
        self.assertDoctypeHandled('xsl:stylesheet PUBLIC "htmlent.dtd"')

    def test_real_xhtml_document(self):
        """A real XHTML document should come out more or less the same as it went in."""
        markup = b"""<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN">
<html xmlns="http://www.w3.org/1999/xhtml">
<head><title>Hello.</title></head>
<body>Goodbye.</body>
</html>"""
        soup = self.soup(markup)
        self.assertEqual(
            soup.encode("utf-8").replace(b"\n", b""),
            markup.replace(b"\n", b""))

    def test_deepcopy(self):
        """Make sure you can copy the tree builder.

        This is important because the builder is part of a
        BeautifulSoup object, and we want to be able to copy that.
        """
        copy.deepcopy(self.default_builder)

    def test_p_tag_is_never_empty_element(self):
        """A <p> tag is never designated as an empty-element tag.

        Even if the markup shows it as an empty-element tag, it
        shouldn't be presented that way.
        """
        soup = self.soup("<p/>")
        self.assertFalse(soup.p.is_empty_element)
        self.assertEqual(str(soup.p), "<p></p>")

    def test_unclosed_tags_get_closed(self):
        """A tag that's not closed by the end of the document should be closed.

        This applies to all tags except empty-element tags.
        """
        self.assertSoupEquals("<p>", "<p></p>")
        self.assertSoupEquals("<b>", "<b></b>")

        self.assertSoupEquals("<br>", "<br/>")

    def test_br_is_always_empty_element_tag(self):
        """A <br> tag is designated as an empty-element tag.

        Some parsers treat <br></br> as one <br/> tag, some parsers as
        two tags, but it should always be an empty-element tag.
        """
        soup = self.soup("<br></br>")
        self.assertTrue(soup.br.is_empty_element)
        self.assertEqual(str(soup.br), "<br/>")

    def test_nested_formatting_elements(self):
        self.assertSoupEquals("<em><em></em></em>")

    def test_comment(self):
        # Comments are represented as Comment objects.
        markup = "<p>foo<!--foobar-->baz</p>"
        self.assertSoupEquals(markup)

        soup = self.soup(markup)
        comment = soup.find(text="foobar")
        self.assertEqual(comment.__class__, Comment)

        # The comment is properly integrated into the tree.
        foo = soup.find(text="foo")
        self.assertEqual(comment, foo.next_element)
        baz = soup.find(text="baz")
        self.assertEqual(comment, baz.previous_element)

    def test_preserved_whitespace_in_pre_and_textarea(self):
        """Whitespace must be preserved in <pre> and <textarea> tags."""
        self.assertSoupEquals("<pre>   </pre>")
        self.assertSoupEquals("<textarea> woo  </textarea>")

    def test_nested_inline_elements(self):
        """Inline elements can be nested indefinitely."""
        b_tag = "<b>Inside a B tag</b>"
        self.assertSoupEquals(b_tag)

        nested_b_tag = "<p>A <i>nested <b>tag</b></i></p>"
        self.assertSoupEquals(nested_b_tag)

        double_nested_b_tag = "<p>A <a>doubly <i>nested <b>tag</b></i></a></p>"
        self.assertSoupEquals(nested_b_tag)

    def test_nested_block_level_elements(self):
        """Block elements can be nested."""
        soup = self.soup('<blockquote><p><b>Foo</b></p></blockquote>')
        blockquote = soup.blockquote
        self.assertEqual(blockquote.p.b.string, 'Foo')
        self.assertEqual(blockquote.b.string, 'Foo')

    def test_correctly_nested_tables(self):
        """One table can go inside another one."""
        markup = ('<table id="1">'
                  '<tr>'
                  "<td>Here's another table:"
                  '<table id="2">'
                  '<tr><td>foo</td></tr>'
                  '</table></td>')

        self.assertSoupEquals(
            markup,
            '<table id="1"><tr><td>Here\'s another table:'
            '<table id="2"><tr><td>foo</td></tr></table>'
            '</td></tr></table>')

        self.assertSoupEquals(
            "<table><thead><tr><td>Foo</td></tr></thead>"
            "<tbody><tr><td>Bar</td></tr></tbody>"
            "<tfoot><tr><td>Baz</td></tr></tfoot></table>")

    def test_deeply_nested_multivalued_attribute(self):
        # html5lib can set the attributes of the same tag many times
        # as it rearranges the tree. This has caused problems with
        # multivalued attributes.
        markup = '<table><div><div class="css"></div></div></table>'
        soup = self.soup(markup)
        self.assertEqual(["css"], soup.div.div['class'])

    def test_angle_brackets_in_attribute_values_are_escaped(self):
        self.assertSoupEquals('<a b="<a>"></a>', '<a b="&lt;a&gt;"></a>')

    def test_entities_in_attributes_converted_to_unicode(self):
        expect = u'<p id="pi\N{LATIN SMALL LETTER N WITH TILDE}ata"></p>'
        self.assertSoupEquals('<p id="pi&#241;ata"></p>', expect)
        self.assertSoupEquals('<p id="pi&#xf1;ata"></p>', expect)
        self.assertSoupEquals('<p id="pi&#Xf1;ata"></p>', expect)
        self.assertSoupEquals('<p id="pi&ntilde;ata"></p>', expect)

    def test_entities_in_text_converted_to_unicode(self):
        expect = u'<p>pi\N{LATIN SMALL LETTER N WITH TILDE}ata</p>'
        self.assertSoupEquals("<p>pi&#241;ata</p>", expect)
        self.assertSoupEquals("<p>pi&#xf1;ata</p>", expect)
        self.assertSoupEquals("<p>pi&#Xf1;ata</p>", expect)
        self.assertSoupEquals("<p>pi&ntilde;ata</p>", expect)

    def test_quot_entity_converted_to_quotation_mark(self):
        self.assertSoupEquals("<p>I said &quot;good day!&quot;</p>",
                              '<p>I said "good day!"</p>')

    def test_out_of_range_entity(self):
        expect = u"\N{REPLACEMENT CHARACTER}"
        self.assertSoupEquals("&#10000000000000;", expect)
        self.assertSoupEquals("&#x10000000000000;", expect)
        self.assertSoupEquals("&#1000000000;", expect)

    def test_multipart_strings(self):
        "Mostly to prevent a recurrence of a bug in the html5lib treebuilder."
        soup = self.soup("<html><h2>\nfoo</h2><p></p></html>")
        self.assertEqual("p", soup.h2.string.next_element.name)
        self.assertEqual("p", soup.p.name)

    def test_basic_namespaces(self):
        """Parsers don't need to *understand* namespaces, but at the
        very least they should not choke on namespaces or lose
        data."""

        markup = b'<html xmlns="http://www.w3.org/1999/xhtml" xmlns:mathml="http://www.w3.org/1998/Math/MathML" xmlns:svg="http://www.w3.org/2000/svg"><head></head><body><mathml:msqrt>4</mathml:msqrt><b svg:fill="red"></b></body></html>'
        soup = self.soup(markup)
        self.assertEqual(markup, soup.encode())
        html = soup.html
        self.assertEqual('http://www.w3.org/1999/xhtml', soup.html['xmlns'])
        self.assertEqual(
            'http://www.w3.org/1998/Math/MathML', soup.html['xmlns:mathml'])
        self.assertEqual(
            'http://www.w3.org/2000/svg', soup.html['xmlns:svg'])

    def test_multivalued_attribute_value_becomes_list(self):
        markup = b'<a class="foo bar">'
        soup = self.soup(markup)
        self.assertEqual(['foo', 'bar'], soup.a['class'])

    #
    # Generally speaking, tests below this point are more tests of
    # Beautiful Soup than tests of the tree builders. But parsers are
    # weird, so we run these tests separately for every tree builder
    # to detect any differences between them.
    #

    def test_can_parse_unicode_document(self):
        # A seemingly innocuous document... but it's in Unicode! And
        # it contains characters that can't be represented in the
        # encoding found in the  declaration! The horror!
        markup = u'<html><head><meta encoding="euc-jp"></head><body>Sacr\N{LATIN SMALL LETTER E WITH ACUTE} bleu!</body>'
        soup = self.soup(markup)
        self.assertEqual(u'Sacr\xe9 bleu!', soup.body.string)

    def test_soupstrainer(self):
        """Parsers should be able to work with SoupStrainers."""
        strainer = SoupStrainer("b")
        soup = self.soup("A <b>bold</b> <meta/> <i>statement</i>",
                         parse_only=strainer)
        self.assertEqual(soup.decode(), "<b>bold</b>")

    def test_single_quote_attribute_values_become_double_quotes(self):
        self.assertSoupEquals("<foo attr='bar'></foo>",
                              '<foo attr="bar"></foo>')

    def test_attribute_values_with_nested_quotes_are_left_alone(self):
        text = """<foo attr='bar "brawls" happen'>a</foo>"""
        self.assertSoupEquals(text)

    def test_attribute_values_with_double_nested_quotes_get_quoted(self):
        text = """<foo attr='bar "brawls" happen'>a</foo>"""
        soup = self.soup(text)
        soup.foo['attr'] = 'Brawls happen at "Bob\'s Bar"'
        self.assertSoupEquals(
            soup.foo.decode(),
            """<foo attr="Brawls happen at &quot;Bob\'s Bar&quot;">a</foo>""")

    def test_ampersand_in_attribute_value_gets_escaped(self):
        self.assertSoupEquals('<this is="really messed up & stuff"></this>',
                              '<this is="really messed up &amp; stuff"></this>')

        self.assertSoupEquals(
            '<a href="http://example.org?a=1&b=2;3">foo</a>',
            '<a href="http://example.org?a=1&amp;b=2;3">foo</a>')

    def test_escaped_ampersand_in_attribute_value_is_left_alone(self):
        self.assertSoupEquals('<a href="http://example.org?a=1&amp;b=2;3"></a>')

    def test_entities_in_strings_converted_during_parsing(self):
        # Both XML and HTML entities are converted to Unicode characters
        # during parsing.
        text = "<p>&lt;&lt;sacr&eacute;&#32;bleu!&gt;&gt;</p>"
        expected = u"<p>&lt;&lt;sacr\N{LATIN SMALL LETTER E WITH ACUTE} bleu!&gt;&gt;</p>"
        self.assertSoupEquals(text, expected)

    def test_smart_quotes_converted_on_the_way_in(self):
        # Microsoft smart quotes are converted to Unicode characters during
        # parsing.
        quote = b"<p>\x91Foo\x92</p>"
        soup = self.soup(quote)
        self.assertEqual(
            soup.p.string,
            u"\N{LEFT SINGLE QUOTATION MARK}Foo\N{RIGHT SINGLE QUOTATION MARK}")

    def test_non_breaking_spaces_converted_on_the_way_in(self):
        soup = self.soup("<a>&nbsp;&nbsp;</a>")
        self.assertEqual(soup.a.string, u"\N{NO-BREAK SPACE}" * 2)

    def test_entities_converted_on_the_way_out(self):
        text = "<p>&lt;&lt;sacr&eacute;&#32;bleu!&gt;&gt;</p>"
        expected = u"<p>&lt;&lt;sacr\N{LATIN SMALL LETTER E WITH ACUTE} bleu!&gt;&gt;</p>".encode("utf-8")
        soup = self.soup(text)
        self.assertEqual(soup.p.encode("utf-8"), expected)

    def test_real_iso_latin_document(self):
        # Smoke test of interrelated functionality, using an
        # easy-to-understand document.

        # Here it is in Unicode. Note that it claims to be in ISO-Latin-1.
        unicode_html = u'<html><head><meta content="text/html; charset=ISO-Latin-1" http-equiv="Content-type"/></head><body><p>Sacr\N{LATIN SMALL LETTER E WITH ACUTE} bleu!</p></body></html>'

        # That's because we're going to encode it into ISO-Latin-1, and use
        # that to test.
        iso_latin_html = unicode_html.encode("iso-8859-1")

        # Parse the ISO-Latin-1 HTML.
        soup = self.soup(iso_latin_html)
        # Encode it to UTF-8.
        result = soup.encode("utf-8")

        # What do we expect the result to look like? Well, it would
        # look like unicode_html, except that the META tag would say
        # UTF-8 instead of ISO-Latin-1.
        expected = unicode_html.replace("ISO-Latin-1", "utf-8")

        # And, of course, it would be in UTF-8, not Unicode.
        expected = expected.encode("utf-8")

        # Ta-da!
        self.assertEqual(result, expected)

    def test_real_shift_jis_document(self):
        # Smoke test to make sure the parser can handle a document in
        # Shift-JIS encoding, without choking.
        shift_jis_html = (
            b'<html><head></head><body><pre>'
            b'\x82\xb1\x82\xea\x82\xcdShift-JIS\x82\xc5\x83R\x81[\x83f'
            b'\x83B\x83\x93\x83O\x82\xb3\x82\xea\x82\xbd\x93\xfa\x96{\x8c'
            b'\xea\x82\xcc\x83t\x83@\x83C\x83\x8b\x82\xc5\x82\xb7\x81B'
            b'</pre></body></html>')
        unicode_html = shift_jis_html.decode("shift-jis")
        soup = self.soup(unicode_html)

        # Make sure the parse tree is correctly encoded to various
        # encodings.
        self.assertEqual(soup.encode("utf-8"), unicode_html.encode("utf-8"))
        self.assertEqual(soup.encode("euc_jp"), unicode_html.encode("euc_jp"))

    def test_real_hebrew_document(self):
        # A real-world test to make sure we can convert ISO-8859-9 (a
        # Hebrew encoding) to UTF-8.
        hebrew_document = b'<html><head><title>Hebrew (ISO 8859-8) in Visual Directionality</title></head><body><h1>Hebrew (ISO 8859-8) in Visual Directionality</h1>\xed\xe5\xec\xf9</body></html>'
        soup = self.soup(
            hebrew_document, from_encoding="iso8859-8")
        self.assertEqual(soup.original_encoding, 'iso8859-8')
        self.assertEqual(
            soup.encode('utf-8'),
            hebrew_document.decode("iso8859-8").encode("utf-8"))

    def test_meta_tag_reflects_current_encoding(self):
        # Here's the <meta> tag saying that a document is
        # encoded in Shift-JIS.
        meta_tag = ('<meta content="text/html; charset=x-sjis" '
                    'http-equiv="Content-type"/>')

        # Here's a document incorporating that meta tag.
        shift_jis_html = (
            '<html><head>\n%s\n'
            '<meta http-equiv="Content-language" content="ja"/>'
            '</head><body>Shift-JIS markup goes here.') % meta_tag
        soup = self.soup(shift_jis_html)

        # Parse the document, and the charset is seemingly unaffected.
        parsed_meta = soup.find('meta', {'http-equiv': 'Content-type'})
        content = parsed_meta['content']
        self.assertEqual('text/html; charset=x-sjis', content)

        # But that value is actually a ContentMetaAttributeValue object.
        self.assertTrue(isinstance(content, ContentMetaAttributeValue))

        # And it will take on a value that reflects its current
        # encoding.
        self.assertEqual('text/html; charset=utf8', content.encode("utf8"))

        # For the rest of the story, see TestSubstitutions in
        # test_tree.py.

    def test_html5_style_meta_tag_reflects_current_encoding(self):
        # Here's the <meta> tag saying that a document is
        # encoded in Shift-JIS.
        meta_tag = ('<meta id="encoding" charset="x-sjis" />')

        # Here's a document incorporating that meta tag.
        shift_jis_html = (
            '<html><head>\n%s\n'
            '<meta http-equiv="Content-language" content="ja"/>'
            '</head><body>Shift-JIS markup goes here.') % meta_tag
        soup = self.soup(shift_jis_html)

        # Parse the document, and the charset is seemingly unaffected.
        parsed_meta = soup.find('meta', id="encoding")
        charset = parsed_meta['charset']
        self.assertEqual('x-sjis', charset)

        # But that value is actually a CharsetMetaAttributeValue object.
        self.assertTrue(isinstance(charset, CharsetMetaAttributeValue))

        # And it will take on a value that reflects its current
        # encoding.
        self.assertEqual('utf8', charset.encode("utf8"))

    def test_tag_with_no_attributes_can_have_attributes_added(self):
        data = self.soup("<a>text</a>")
        data.a['foo'] = 'bar'
        self.assertEqual('<a foo="bar">text</a>', data.a.decode())

class XMLTreeBuilderSmokeTest(object):

    def test_docstring_generated(self):
        soup = self.soup("<root/>")
        self.assertEqual(
            soup.encode(), b'<?xml version="1.0" encoding="utf-8"?>\n<root/>')

    def test_real_xhtml_document(self):
        """A real XHTML document should come out *exactly* the same as it went in."""
        markup = b"""<?xml version="1.0" encoding="utf-8"?>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN">
<html xmlns="http://www.w3.org/1999/xhtml">
<head><title>Hello.</title></head>
<body>Goodbye.</body>
</html>"""
        soup = self.soup(markup)
        self.assertEqual(
            soup.encode("utf-8"), markup)

    def test_formatter_processes_script_tag_for_xml_documents(self):
        doc = """
  <script type="text/javascript">
  </script>
"""
        soup = BeautifulSoup(doc, "xml")
        # lxml would have stripped this while parsing, but we can add
        # it later.
        soup.script.string = 'console.log("< < hey > > ");'
        encoded = soup.encode()
        self.assertTrue(b"&lt; &lt; hey &gt; &gt;" in encoded)

    def test_can_parse_unicode_document(self):
        markup = u'<?xml version="1.0" encoding="euc-jp"><root>Sacr\N{LATIN SMALL LETTER E WITH ACUTE} bleu!</root>'
        soup = self.soup(markup)
        self.assertEqual(u'Sacr\xe9 bleu!', soup.root.string)

    def test_popping_namespaced_tag(self):
        markup = '<rss xmlns:dc="foo"><dc:creator>b</dc:creator><dc:date>2012-07-02T20:33:42Z</dc:date><dc:rights>c</dc:rights><image>d</image></rss>'
        soup = self.soup(markup)
        self.assertEqual(
            unicode(soup.rss), markup)

    def test_docstring_includes_correct_encoding(self):
        soup = self.soup("<root/>")
        self.assertEqual(
            soup.encode("latin1"),
            b'<?xml version="1.0" encoding="latin1"?>\n<root/>')

    def test_large_xml_document(self):
        """A large XML document should come out the same as it went in."""
        markup = (b'<?xml version="1.0" encoding="utf-8"?>\n<root>'
                  + b'0' * (2**12)
                  + b'</root>')
        soup = self.soup(markup)
        self.assertEqual(soup.encode("utf-8"), markup)


    def test_tags_are_empty_element_if_and_only_if_they_are_empty(self):
        self.assertSoupEquals("<p>", "<p/>")
        self.assertSoupEquals("<p>foo</p>")

    def test_namespaces_are_preserved(self):
        markup = '<root xmlns:a="http://example.com/" xmlns:b="http://example.net/"><a:foo>This tag is in the a namespace</a:foo><b:foo>This tag is in the b namespace</b:foo></root>'
        soup = self.soup(markup)
        root = soup.root
        self.assertEqual("http://example.com/", root['xmlns:a'])
        self.assertEqual("http://example.net/", root['xmlns:b'])

    def test_closing_namespaced_tag(self):
        markup = '<p xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:date>20010504</dc:date></p>'
        soup = self.soup(markup)
        self.assertEqual(unicode(soup.p), markup)

    def test_namespaced_attributes(self):
        markup = '<foo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><bar xsi:schemaLocation="http://www.example.com"/></foo>'
        soup = self.soup(markup)
        self.assertEqual(unicode(soup.foo), markup)

    def test_namespaced_attributes_xml_namespace(self):
        markup = '<foo xml:lang="fr">bar</foo>'
        soup = self.soup(markup)
        self.assertEqual(unicode(soup.foo), markup)

class HTML5TreeBuilderSmokeTest(HTMLTreeBuilderSmokeTest):
    """Smoke test for a tree builder that supports HTML5."""

    def test_real_xhtml_document(self):
        # Since XHTML is not HTML5, HTML5 parsers are not tested to handle
        # XHTML documents in any particular way.
        pass

    def test_html_tags_have_namespace(self):
        markup = "<a>"
        soup = self.soup(markup)
        self.assertEqual("http://www.w3.org/1999/xhtml", soup.a.namespace)

    def test_svg_tags_have_namespace(self):
        markup = '<svg><circle/></svg>'
        soup = self.soup(markup)
        namespace = "http://www.w3.org/2000/svg"
        self.assertEqual(namespace, soup.svg.namespace)
        self.assertEqual(namespace, soup.circle.namespace)


    def test_mathml_tags_have_namespace(self):
        markup = '<math><msqrt>5</msqrt></math>'
        soup = self.soup(markup)
        namespace = 'http://www.w3.org/1998/Math/MathML'
        self.assertEqual(namespace, soup.math.namespace)
        self.assertEqual(namespace, soup.msqrt.namespace)

    def test_xml_declaration_becomes_comment(self):
        markup = '<?xml version="1.0" encoding="utf-8"?><html></html>'
        soup = self.soup(markup)
        self.assertTrue(isinstance(soup.contents[0], Comment))
        self.assertEqual(soup.contents[0], '?xml version="1.0" encoding="utf-8"?')
        self.assertEqual("html", soup.contents[0].next_element.name)

def skipIf(condition, reason):
   def nothing(test, *args, **kwargs):
       return None

   def decorator(test_item):
       if condition:
           return nothing
       else:
           return test_item

   return decorator
