//! The four keep-awake modes and their integer values in `settings.json`.

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Hash)]
pub enum Mode {
    /// Off: the active power plan decides.
    #[default]
    Passive,
    Indefinite,
    /// For a fixed duration from when it was chosen.
    Timed,
    /// Until a date and time.
    Expirable,
}

impl Mode {
    pub const fn value(self) -> i64 {
        match self {
            Mode::Passive => 0,
            Mode::Indefinite => 1,
            Mode::Timed => 2,
            Mode::Expirable => 3,
        }
    }

    pub const fn from_value(value: i64) -> Option<Mode> {
        match value {
            0 => Some(Mode::Passive),
            1 => Some(Mode::Indefinite),
            2 => Some(Mode::Timed),
            3 => Some(Mode::Expirable),
            _ => None,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn values_match_the_settings_contract() {
        for mode in [Mode::Passive, Mode::Indefinite, Mode::Timed, Mode::Expirable] {
            assert_eq!(Mode::from_value(mode.value()), Some(mode));
        }
        assert_eq!(Mode::Expirable.value(), 3);
        assert_eq!(Mode::from_value(4), None);
        assert_eq!(Mode::from_value(-1), None);
    }
}
