//! Localized strings generated from `Resources.resx` by the build script.

mod generated {
    // The variants are named after the resx keys, and the resx is where their text lives.
    #![allow(missing_docs)]
    include!(concat!(env!("OUT_DIR"), "/strings.rs"));
}

pub use generated::StringId;
use generated::{COUNT, LOCALIZED, NEUTRAL};

/// The strings for one UI language, falling back to English for anything not yet translated.
#[derive(Clone, Copy, Debug)]
pub struct Strings {
    localized: Option<&'static [Option<&'static str>; COUNT]>,
}

impl Strings {
    /// The neutral strings from `Resources.resx`.
    pub const ENGLISH: Strings = Strings { localized: None };

    /// Picks the best table for a BCP-47 tag such as `de-DE`, `pt-BR` or `zh-Hans`.
    pub fn for_language(tag: &str) -> Strings {
        Strings {
            localized: pick(LOCALIZED, tag),
        }
    }

    /// The text for `id` in this language, or the English text if it is not translated yet.
    pub fn get(&self, id: StringId) -> &'static str {
        let index = id as usize;
        self.localized
            .and_then(|table| table[index])
            .unwrap_or(NEUTRAL[index])
    }

    /// Substitutes `{0}`, the placeholder the resources use.
    pub fn format(&self, id: StringId, argument: &str) -> String {
        self.get(id).replace("{0}", argument)
    }
}

impl Default for Strings {
    fn default() -> Self {
        Strings::ENGLISH
    }
}

/// Finds the table for `tag`: the same tag ignoring case, else another region of the same
/// language (`de-AT` gets `de-DE`). Chinese scripts never stand in for each other, since a reader
/// of one may not read the other; anything else gets `None`, meaning English.
fn pick<'a, T>(tables: &'a [(&str, T)], tag: &str) -> Option<&'a T> {
    let tag = tag.trim();
    if tag.is_empty() {
        return None;
    }
    let exact = tables
        .iter()
        .find(|(language, _)| language.eq_ignore_ascii_case(tag));
    let wanted = (primary(tag), script(tag));
    let same_language = || {
        tables
            .iter()
            .find(|(language, _)| (primary(language), script(language)) == wanted)
    };
    exact.or_else(same_language).map(|(_, table)| table)
}

fn primary(tag: &str) -> String {
    tag.split('-').next().unwrap_or_default().to_ascii_lowercase()
}

/// The script subtag (`Hans` in `zh-Hans-CN`), or the one a Chinese region implies.
fn script(tag: &str) -> Option<String> {
    let mut subtags = tag.split('-');
    let language = subtags.next().unwrap_or_default();
    let rest: Vec<&str> = subtags.collect();
    let explicit = rest
        .iter()
        .find(|s| s.len() == 4 && s.chars().all(|c| c.is_ascii_alphabetic()));
    if let Some(script) = explicit {
        return Some(script.to_ascii_lowercase());
    }
    if !language.eq_ignore_ascii_case("zh") {
        return None;
    }
    let traditional = rest
        .iter()
        .any(|region| ["tw", "hk", "mo"].iter().any(|r| region.eq_ignore_ascii_case(r)));
    Some(if traditional { "hant" } else { "hans" }.to_owned())
}

#[cfg(test)]
mod tests {
    use super::*;

    const TABLES: [(&str, u8); 4] = [("de-DE", 1), ("pt-BR", 2), ("zh-Hans", 3), ("zh-TW", 4)];

    #[test]
    fn exact_tags_match_ignoring_case() {
        assert_eq!(pick(&TABLES, "DE-de"), Some(&1));
        assert_eq!(pick(&TABLES, " pt-br "), Some(&2));
        assert_eq!(pick(&TABLES, "zh-hans"), Some(&3));
    }

    #[test]
    fn other_regions_of_the_same_language_match() {
        assert_eq!(pick(&TABLES, "de-AT"), Some(&1));
        assert_eq!(pick(&TABLES, "de"), Some(&1));
        assert_eq!(pick(&TABLES, "pt"), Some(&2));
        assert_eq!(pick(&TABLES, "pt-PT"), Some(&2));
    }

    #[test]
    fn chinese_scripts_do_not_stand_in_for_each_other() {
        assert_eq!(pick(&TABLES, "zh-CN"), Some(&3));
        assert_eq!(pick(&TABLES, "zh-Hans-SG"), Some(&3));
        assert_eq!(pick(&TABLES, "zh-Hant"), Some(&4));
        assert_eq!(pick(&TABLES, "zh-HK"), Some(&4));
        assert_eq!(pick(&[("zh-Hans", 3)], "zh-Hant"), None);
        assert_eq!(pick(&[("zh-Hans", 3)], "zh-TW"), None);
        assert_eq!(pick(&[("zh-TW", 4)], "zh"), None);
    }

    #[test]
    fn unknown_or_empty_tags_match_nothing() {
        assert_eq!(pick(&TABLES, ""), None);
        assert_eq!(pick(&TABLES, "  "), None);
        assert_eq!(pick(&TABLES, "fr-FR"), None);
        assert_eq!(pick(&TABLES, "d"), None, "only whole subtags count");
    }

    #[test]
    fn untranslated_strings_fall_back_to_english() {
        static PARTIAL: [Option<&str>; COUNT] = {
            let mut table = [None; COUNT];
            table[StringId::Exit as usize] = Some("Beenden");
            table
        };
        let strings = Strings {
            localized: Some(&PARTIAL),
        };
        assert_eq!(strings.get(StringId::Exit), "Beenden");
        assert_eq!(strings.get(StringId::KeepScreenOn), "Keep screen on");
        assert_eq!(strings.format(StringId::Minutes, "30"), "30 minutes");
    }

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
