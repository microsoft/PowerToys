# check-spelling/check-spelling configuration

File | Purpose | Format | Info
-|-|-|-
[allow.txt](allow.txt) | Add words to the dictionary | one word per line (only letters and `'`s allowed) | [allow](https://github.com/check-spelling/check-spelling/wiki/Configuration#allow)
[reject.txt](reject.txt) | Remove words from the dictionary (after allow) | grep pattern matching whole dictionary words | [reject](https://github.com/check-spelling/check-spelling/wiki/Configuration-Examples%3A-reject)
[excludes.txt](excludes.txt) | Files to ignore entirely | perl regular expression | [excludes](https://github.com/check-spelling/check-spelling/wiki/Configuration-Examples%3A-excludes)
[only.txt](only.txt) | Only check matching files (applied after excludes) | perl regular expression | [only](https://github.com/check-spelling/check-spelling/wiki/Configuration-Examples%3A-only)
[patterns.txt](patterns.txt) | Patterns to ignore from checked lines | perl regular expression (order matters, first match wins) | [patterns](https://github.com/check-spelling/check-spelling/wiki/Configuration-Examples%3A-patterns)
[candidate.patterns](candidate.patterns) | Patterns that might be worth adding to [patterns.txt](patterns.txt) | perl regular expression with optional comment block introductions (all matches will be suggested) | [candidates](https://github.com/check-spelling/check-spelling/wiki/Feature:-Suggest-patterns)
[line_forbidden.patterns](line_forbidden.patterns) | Patterns to flag in checked lines | perl regular expression (order matters, first match wins) | [patterns](https://github.com/check-spelling/check-spelling/wiki/Configuration-Examples%3A-patterns)
[expect.txt](expect.txt) | Expected words that aren't in the dictionary | one word per line (sorted, alphabetically) | [expect](https://github.com/check-spelling/check-spelling/wiki/Configuration#expect)
[advice.md](advice.md) | Supplement for GitHub comment when unrecognized words are found | GitHub Markdown | [advice](https://github.com/check-spelling/check-spelling/wiki/Configuration-Examples%3A-advice)

Note: you can replace any of these files with a directory by the same name (minus the suffix)
and then include multiple files inside that directory (with that suffix) to merge multiple files together.
