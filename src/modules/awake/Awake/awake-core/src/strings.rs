//! Localized strings generated from `Resources.resx` by the build script.

include!(concat!(env!("OUT_DIR"), "/strings.rs"));

/// The strings for one UI language, falling back to English for anything not yet translated.
#[derive(Clone, Copy, Debug)]
pub struct Strings {
    localized: Option<&'static [Option<&'static str>; COUNT]>,
}

impl Strings {
    pub const ENGLISH: Strings = Strings { localized: None };

    /// Picks the best table for a BCP-47 tag such as `de-DE`, `pt-BR` or `zh-Hans`.
    pub fn for_language(tag: &str) -> Strings {
        let tag = tag.trim();
        if tag.is_empty() {
            return Strings::ENGLISH;
        }
        let primary = |s: &str| s.split('-').next().unwrap_or_default().to_ascii_lowercase();
        let wanted = primary(tag);
        let exact = LOCALIZED
            .iter()
            .find(|(language, _)| language.eq_ignore_ascii_case(tag));
        let same_language = || LOCALIZED.iter().find(|(language, _)| primary(language) == wanted);
        Strings {
            localized: exact.or_else(same_language).map(|(_, table)| table),
        }
    }

    pub fn get(&self, id: StringId) -> &'static str {
        let index = id as usize;
        self.localized
            .and_then(|table| table[index])
            .unwrap_or(NEUTRAL[index])
    }

    /// Substitutes `{0}` the way the .NET resources expected.
    pub fn format(&self, id: StringId, argument: &str) -> String {
        self.get(id).replace("{0}", argument)
    }
}

impl Default for Strings {
    fn default() -> Self {
        Strings::ENGLISH
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn english_values_come_from_the_resx() {
        let strings = Strings::ENGLISH;
        assert_eq!(strings.get(StringId::Exit), "Exit");
        assert_eq!(strings.get(StringId::KeepScreenOn), "Keep screen on");
        assert_eq!(strings.format(StringId::Minutes, "30"), "30 minutes");
    }

    #[test]
    fn unknown_language_falls_back_to_english() {
        assert_eq!(Strings::for_language("xx-YY").get(StringId::Hour), "{0} hour");
        assert_eq!(Strings::for_language("").get(StringId::Hours), "{0} hours");
    }

    #[path = "../../../build/resx.rs"]
    mod resx;

    const HEADER: &str =
        "<root><resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>";

    #[test]
    fn the_shipped_resx_is_well_formed() {
        let text = include_str!("../../Resources.resx");
        let entries = resx::parse(text).unwrap();
        assert_eq!(entries.len(), COUNT);
        assert!(
            entries
                .iter()
                .any(|(name, value)| name == "AWAKE_EXIT" && value == "Exit")
        );
    }

    #[test]
    fn resx_reader_skips_comments_and_comment_elements() {
        let text = format!(
            "{HEADER}<!-- <data name=\"Name1\"><value>x</value></data> -->             <data name=\"A\" xml:space=\"preserve\"><value>a &amp; b</value><comment>note</comment></data></root>"
        );
        assert_eq!(
            resx::parse(&text).unwrap(),
            [("A".to_owned(), "a & b".to_owned())]
        );
    }

    #[test]
    fn resx_reader_rejects_what_xml_parsers_reject() {
        for broken in [
            format!("{HEADER}<!-- never closed <data name=\"A\"><value>a</value></data></root>"),
            format!("{HEADER}<!-- run --help --></root>"),
            format!("{HEADER}<data name=\"A\"><value>a</data></value></root>"),
            format!("{HEADER}<data name=\"A\"><value>a</value></data>"),
            "<root><data name=\"A\"><value>a</value></data></root>".to_owned(),
        ] {
            assert!(resx::parse(&broken).is_err(), "accepted {broken:?}");
        }
    }
}
