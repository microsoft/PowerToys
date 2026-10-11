//! Reads the string entries of a `.resx` file.
//!
//! Shared by `build.rs` and the unit tests in `strings.rs`. Not a general XML parser, but strict
//! about the structure the localization tooling relies on: comments are closed and contain no
//! `--`, elements nest and close in order, and the file declares itself as resx. Anything the
//! loc pipeline's XML reader would reject fails the build instead of silently dropping
//! translations.

/// The `<data name="X"><value>text</value></data>` entries under `<root>`, in file order.
pub fn parse(text: &str) -> Result<Vec<(String, String)>, String> {
    let mut entries = Vec::new();
    let mut open: Vec<&str> = Vec::new();
    let mut data: Option<(String, String)> = None;
    let mut header: Option<String> = None;
    let mut capture: Option<String> = None;
    let mut is_resx = false;
    let mut seen_root = false;
    let mut rest = text.strip_prefix('\u{feff}').unwrap_or(text);

    while !rest.is_empty() {
        let lt = rest.find('<').unwrap_or(rest.len());
        let content = &rest[..lt];
        if let Some(capture) = capture.as_mut() {
            capture.push_str(content);
        } else if open.is_empty() && !content.trim().is_empty() {
            return Err(format!("line {}: text outside <root>", line(text, content)));
        }
        rest = &rest[lt..];
        if rest.is_empty() {
            break;
        }

        if let Some(body) = rest.strip_prefix("<!--") {
            let end = body
                .find("-->")
                .ok_or_else(|| format!("line {}: comment is never closed", line(text, rest)))?;
            if body[..end].contains("--") || body[..end].ends_with('-') {
                return Err(format!("line {}: comment contains '--'", line(text, rest)));
            }
            rest = &body[end + 3..];
            continue;
        }
        if rest.starts_with("<![CDATA[") {
            return Err(format!(
                "line {}: CDATA sections are not supported",
                line(text, rest)
            ));
        }
        let end = rest
            .find('>')
            .ok_or_else(|| format!("line {}: tag is never closed", line(text, rest)))?;
        let tag = &rest[1..end];
        let at = line(text, rest);
        rest = &rest[end + 1..];
        if tag.starts_with('?') || tag.starts_with('!') {
            continue;
        }

        if let Some(name) = tag.strip_prefix('/') {
            let name = name.trim();
            match open.pop() {
                Some(expected) if expected == name => {}
                Some(expected) => return Err(format!("line {at}: </{name}> closes <{expected}>")),
                None => return Err(format!("line {at}: </{name}> has no opening tag")),
            }
            match (open.len(), name) {
                (2, "value") => {
                    let value = capture.take().unwrap_or_default();
                    if let Some((_, slot)) = data.as_mut() {
                        *slot = value;
                    } else if header.as_deref() == Some("resmimetype") {
                        is_resx |= value.trim() == "text/microsoft-resx";
                    }
                }
                (1, "data") => entries.extend(data.take().map(|(n, v)| (n, unescape(&v)))),
                (1, "resheader") => header = None,
                _ => {}
            }
            continue;
        }

        let self_closing = tag.ends_with('/');
        let tag = tag.trim_end_matches('/');
        let name = tag
            .split_whitespace()
            .next()
            .ok_or_else(|| format!("line {at}: empty tag"))?;
        match (open.len(), name) {
            (0, "root") if !seen_root => seen_root = true,
            (0, other) => return Err(format!("line {at}: <{other}> outside <root>")),
            (1, "data") => {
                let key =
                    attribute(tag, "name").ok_or_else(|| format!("line {at}: <data> without a name"))?;
                if self_closing {
                    entries.push((key, String::new()));
                } else {
                    data = Some((key, String::new()));
                }
            }
            (1, "resheader") => header = attribute(tag, "name"),
            (2, "value") if !self_closing && (data.is_some() || header.is_some()) => {
                capture = Some(String::new());
            }
            _ => {}
        }
        if !self_closing {
            open.push(name);
        }
    }

    if let Some(unclosed) = open.last() {
        return Err(format!("<{unclosed}> is never closed"));
    }
    if !is_resx {
        return Err("missing <resheader name=\"resmimetype\"> with text/microsoft-resx".into());
    }
    Ok(entries)
}

/// The 1-based line on which `part`, a slice of `text`, starts.
fn line(text: &str, part: &str) -> usize {
    let offset = part.as_ptr() as usize - text.as_ptr() as usize;
    text[..offset].matches('\n').count() + 1
}

fn attribute(tag: &str, attr: &str) -> Option<String> {
    let needle = format!(" {attr}=\"");
    let start = tag.find(&needle)? + needle.len();
    let len = tag[start..].find('"')?;
    Some(unescape(&tag[start..start + len]))
}

fn unescape(text: &str) -> String {
    let mut out = String::with_capacity(text.len());
    let mut rest = text;
    while let Some(amp) = rest.find('&') {
        out.push_str(&rest[..amp]);
        rest = &rest[amp..];
        let Some(semi) = rest.find(';') else { break };
        let entity = &rest[1..semi];
        let decoded = match entity {
            "lt" => Some('<'),
            "gt" => Some('>'),
            "amp" => Some('&'),
            "quot" => Some('"'),
            "apos" => Some('\''),
            _ if entity.starts_with("#x") => u32::from_str_radix(&entity[2..], 16)
                .ok()
                .and_then(char::from_u32),
            _ if entity.starts_with('#') => entity[1..].parse().ok().and_then(char::from_u32),
            _ => None,
        };
        match decoded {
            Some(c) => {
                out.push(c);
                rest = &rest[semi + 1..];
            }
            None => {
                out.push('&');
                rest = &rest[1..];
            }
        }
    }
    out.push_str(rest);
    out.replace("\r\n", "\n")
}
