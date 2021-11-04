/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { LRUCache } from './map.js';
import * as strings from './strings.js';
// Combined filters
/**
 * @returns A filter which combines the provided set
 * of filters with an or. The *first* filters that
 * matches defined the return value of the returned
 * filter.
 */
export function or(...filter) {
    return function (word, wordToMatchAgainst) {
        for (let i = 0, len = filter.length; i < len; i++) {
            const match = filter[i](word, wordToMatchAgainst);
            if (match) {
                return match;
            }
        }
        return null;
    };
}
export const matchesPrefix = _matchesPrefix.bind(undefined, true);
function _matchesPrefix(ignoreCase, word, wordToMatchAgainst) {
    if (!wordToMatchAgainst || wordToMatchAgainst.length < word.length) {
        return null;
    }
    let matches;
    if (ignoreCase) {
        matches = strings.startsWithIgnoreCase(wordToMatchAgainst, word);
    }
    else {
        matches = wordToMatchAgainst.indexOf(word) === 0;
    }
    if (!matches) {
        return null;
    }
    return word.length > 0 ? [{ start: 0, end: word.length }] : [];
}
// Contiguous Substring
export function matchesContiguousSubString(word, wordToMatchAgainst) {
    const index = wordToMatchAgainst.toLowerCase().indexOf(word.toLowerCase());
    if (index === -1) {
        return null;
    }
    return [{ start: index, end: index + word.length }];
}
// Substring
export function matchesSubString(word, wordToMatchAgainst) {
    return _matchesSubString(word.toLowerCase(), wordToMatchAgainst.toLowerCase(), 0, 0);
}
function _matchesSubString(word, wordToMatchAgainst, i, j) {
    if (i === word.length) {
        return [];
    }
    else if (j === wordToMatchAgainst.length) {
        return null;
    }
    else {
        if (word[i] === wordToMatchAgainst[j]) {
            let result = null;
            if (result = _matchesSubString(word, wordToMatchAgainst, i + 1, j + 1)) {
                return join({ start: j, end: j + 1 }, result);
            }
            return null;
        }
        return _matchesSubString(word, wordToMatchAgainst, i, j + 1);
    }
}
// CamelCase
function isLower(code) {
    return 97 /* a */ <= code && code <= 122 /* z */;
}
export function isUpper(code) {
    return 65 /* A */ <= code && code <= 90 /* Z */;
}
function isNumber(code) {
    return 48 /* Digit0 */ <= code && code <= 57 /* Digit9 */;
}
function isWhitespace(code) {
    return (code === 32 /* Space */
        || code === 9 /* Tab */
        || code === 10 /* LineFeed */
        || code === 13 /* CarriageReturn */);
}
const wordSeparators = new Set();
'`~!@#$%^&*()-=+[{]}\\|;:\'",.<>/?'
    .split('')
    .forEach(s => wordSeparators.add(s.charCodeAt(0)));
function isWordSeparator(code) {
    return isWhitespace(code) || wordSeparators.has(code);
}
function charactersMatch(codeA, codeB) {
    return (codeA === codeB) || (isWordSeparator(codeA) && isWordSeparator(codeB));
}
function isAlphanumeric(code) {
    return isLower(code) || isUpper(code) || isNumber(code);
}
function join(head, tail) {
    if (tail.length === 0) {
        tail = [head];
    }
    else if (head.end === tail[0].start) {
        tail[0].start = head.start;
    }
    else {
        tail.unshift(head);
    }
    return tail;
}
function nextAnchor(camelCaseWord, start) {
    for (let i = start; i < camelCaseWord.length; i++) {
        const c = camelCaseWord.charCodeAt(i);
        if (isUpper(c) || isNumber(c) || (i > 0 && !isAlphanumeric(camelCaseWord.charCodeAt(i - 1)))) {
            return i;
        }
    }
    return camelCaseWord.length;
}
function _matchesCamelCase(word, camelCaseWord, i, j) {
    if (i === word.length) {
        return [];
    }
    else if (j === camelCaseWord.length) {
        return null;
    }
    else if (word[i] !== camelCaseWord[j].toLowerCase()) {
        return null;
    }
    else {
        let result = null;
        let nextUpperIndex = j + 1;
        result = _matchesCamelCase(word, camelCaseWord, i + 1, j + 1);
        while (!result && (nextUpperIndex = nextAnchor(camelCaseWord, nextUpperIndex)) < camelCaseWord.length) {
            result = _matchesCamelCase(word, camelCaseWord, i + 1, nextUpperIndex);
            nextUpperIndex++;
        }
        return result === null ? null : join({ start: j, end: j + 1 }, result);
    }
}
// Heuristic to avoid computing camel case matcher for words that don't
// look like camelCaseWords.
function analyzeCamelCaseWord(word) {
    let upper = 0, lower = 0, alpha = 0, numeric = 0, code = 0;
    for (let i = 0; i < word.length; i++) {
        code = word.charCodeAt(i);
        if (isUpper(code)) {
            upper++;
        }
        if (isLower(code)) {
            lower++;
        }
        if (isAlphanumeric(code)) {
            alpha++;
        }
        if (isNumber(code)) {
            numeric++;
        }
    }
    const upperPercent = upper / word.length;
    const lowerPercent = lower / word.length;
    const alphaPercent = alpha / word.length;
    const numericPercent = numeric / word.length;
    return { upperPercent, lowerPercent, alphaPercent, numericPercent };
}
function isUpperCaseWord(analysis) {
    const { upperPercent, lowerPercent } = analysis;
    return lowerPercent === 0 && upperPercent > 0.6;
}
function isCamelCaseWord(analysis) {
    const { upperPercent, lowerPercent, alphaPercent, numericPercent } = analysis;
    return lowerPercent > 0.2 && upperPercent < 0.8 && alphaPercent > 0.6 && numericPercent < 0.2;
}
// Heuristic to avoid computing camel case matcher for words that don't
// look like camel case patterns.
function isCamelCasePattern(word) {
    let upper = 0, lower = 0, code = 0, whitespace = 0;
    for (let i = 0; i < word.length; i++) {
        code = word.charCodeAt(i);
        if (isUpper(code)) {
            upper++;
        }
        if (isLower(code)) {
            lower++;
        }
        if (isWhitespace(code)) {
            whitespace++;
        }
    }
    if ((upper === 0 || lower === 0) && whitespace === 0) {
        return word.length <= 30;
    }
    else {
        return upper <= 5;
    }
}
export function matchesCamelCase(word, camelCaseWord) {
    if (!camelCaseWord) {
        return null;
    }
    camelCaseWord = camelCaseWord.trim();
    if (camelCaseWord.length === 0) {
        return null;
    }
    if (!isCamelCasePattern(word)) {
        return null;
    }
    if (camelCaseWord.length > 60) {
        return null;
    }
    const analysis = analyzeCamelCaseWord(camelCaseWord);
    if (!isCamelCaseWord(analysis)) {
        if (!isUpperCaseWord(analysis)) {
            return null;
        }
        camelCaseWord = camelCaseWord.toLowerCase();
    }
    let result = null;
    let i = 0;
    word = word.toLowerCase();
    while (i < camelCaseWord.length && (result = _matchesCamelCase(word, camelCaseWord, 0, i)) === null) {
        i = nextAnchor(camelCaseWord, i + 1);
    }
    return result;
}
// Matches beginning of words supporting non-ASCII languages
// If `contiguous` is true then matches word with beginnings of the words in the target. E.g. "pul" will match "Git: Pull"
// Otherwise also matches sub string of the word with beginnings of the words in the target. E.g. "gp" or "g p" will match "Git: Pull"
// Useful in cases where the target is words (e.g. command labels)
export function matchesWords(word, target, contiguous = false) {
    if (!target || target.length === 0) {
        return null;
    }
    let result = null;
    let i = 0;
    word = word.toLowerCase();
    target = target.toLowerCase();
    while (i < target.length && (result = _matchesWords(word, target, 0, i, contiguous)) === null) {
        i = nextWord(target, i + 1);
    }
    return result;
}
function _matchesWords(word, target, i, j, contiguous) {
    if (i === word.length) {
        return [];
    }
    else if (j === target.length) {
        return null;
    }
    else if (!charactersMatch(word.charCodeAt(i), target.charCodeAt(j))) {
        return null;
    }
    else {
        let result = null;
        let nextWordIndex = j + 1;
        result = _matchesWords(word, target, i + 1, j + 1, contiguous);
        if (!contiguous) {
            while (!result && (nextWordIndex = nextWord(target, nextWordIndex)) < target.length) {
                result = _matchesWords(word, target, i + 1, nextWordIndex, contiguous);
                nextWordIndex++;
            }
        }
        return result === null ? null : join({ start: j, end: j + 1 }, result);
    }
}
function nextWord(word, start) {
    for (let i = start; i < word.length; i++) {
        if (isWordSeparator(word.charCodeAt(i)) ||
            (i > 0 && isWordSeparator(word.charCodeAt(i - 1)))) {
            return i;
        }
    }
    return word.length;
}
// Fuzzy
const fuzzyContiguousFilter = or(matchesPrefix, matchesCamelCase, matchesContiguousSubString);
const fuzzySeparateFilter = or(matchesPrefix, matchesCamelCase, matchesSubString);
const fuzzyRegExpCache = new LRUCache(10000); // bounded to 10000 elements
export function matchesFuzzy(word, wordToMatchAgainst, enableSeparateSubstringMatching = false) {
    if (typeof word !== 'string' || typeof wordToMatchAgainst !== 'string') {
        return null; // return early for invalid input
    }
    // Form RegExp for wildcard matches
    let regexp = fuzzyRegExpCache.get(word);
    if (!regexp) {
        regexp = new RegExp(strings.convertSimple2RegExpPattern(word), 'i');
        fuzzyRegExpCache.set(word, regexp);
    }
    // RegExp Filter
    const match = regexp.exec(wordToMatchAgainst);
    if (match) {
        return [{ start: match.index, end: match.index + match[0].length }];
    }
    // Default Filter
    return enableSeparateSubstringMatching ? fuzzySeparateFilter(word, wordToMatchAgainst) : fuzzyContiguousFilter(word, wordToMatchAgainst);
}
export function anyScore(pattern, lowPattern, _patternPos, word, lowWord, _wordPos) {
    const result = fuzzyScore(pattern, lowPattern, 0, word, lowWord, 0, true);
    if (result) {
        return result;
    }
    let matches = [];
    let score = 0;
    let idx = _wordPos;
    for (let patternPos = 0; patternPos < lowPattern.length && patternPos < _maxLen; ++patternPos) {
        const wordPos = lowWord.indexOf(lowPattern.charAt(patternPos), idx);
        if (wordPos >= 0) {
            score += 1;
            matches.unshift(wordPos);
            idx = wordPos + 1;
        }
        else if (matches.length > 0) {
            // once we have started matching things
            // we need to match the remaining pattern
            // characters
            break;
        }
    }
    return [score, _wordPos, ...matches];
}
//#region --- fuzzyScore ---
export function createMatches(score) {
    if (typeof score === 'undefined') {
        return [];
    }
    const res = [];
    const wordPos = score[1];
    for (let i = score.length - 1; i > 1; i--) {
        const pos = score[i] + wordPos;
        const last = res[res.length - 1];
        if (last && last.end === pos) {
            last.end = pos + 1;
        }
        else {
            res.push({ start: pos, end: pos + 1 });
        }
    }
    return res;
}
const _maxLen = 128;
function initTable() {
    const table = [];
    const row = [];
    for (let i = 0; i <= _maxLen; i++) {
        row[i] = 0;
    }
    for (let i = 0; i <= _maxLen; i++) {
        table.push(row.slice(0));
    }
    return table;
}
function initArr(maxLen) {
    const row = [];
    for (let i = 0; i <= maxLen; i++) {
        row[i] = 0;
    }
    return row;
}
const _minWordMatchPos = initArr(2 * _maxLen); // min word position for a certain pattern position
const _maxWordMatchPos = initArr(2 * _maxLen); // max word position for a certain pattern position
const _diag = initTable(); // the length of a contiguous diagonal match
const _table = initTable();
const _arrows = initTable();
const _debug = false;
function printTable(table, pattern, patternLen, word, wordLen) {
    function pad(s, n, pad = ' ') {
        while (s.length < n) {
            s = pad + s;
        }
        return s;
    }
    let ret = ` |   |${word.split('').map(c => pad(c, 3)).join('|')}\n`;
    for (let i = 0; i <= patternLen; i++) {
        if (i === 0) {
            ret += ' |';
        }
        else {
            ret += `${pattern[i - 1]}|`;
        }
        ret += table[i].slice(0, wordLen + 1).map(n => pad(n.toString(), 3)).join('|') + '\n';
    }
    return ret;
}
function printTables(pattern, patternStart, word, wordStart) {
    pattern = pattern.substr(patternStart);
    word = word.substr(wordStart);
    console.log(printTable(_table, pattern, pattern.length, word, word.length));
    console.log(printTable(_arrows, pattern, pattern.length, word, word.length));
    console.log(printTable(_diag, pattern, pattern.length, word, word.length));
}
function isSeparatorAtPos(value, index) {
    if (index < 0 || index >= value.length) {
        return false;
    }
    const code = value.codePointAt(index);
    switch (code) {
        case 95 /* Underline */:
        case 45 /* Dash */:
        case 46 /* Period */:
        case 32 /* Space */:
        case 47 /* Slash */:
        case 92 /* Backslash */:
        case 39 /* SingleQuote */:
        case 34 /* DoubleQuote */:
        case 58 /* Colon */:
        case 36 /* DollarSign */:
        case 60 /* LessThan */:
        case 40 /* OpenParen */:
        case 91 /* OpenSquareBracket */:
            return true;
        case undefined:
            return false;
        default:
            if (strings.isEmojiImprecise(code)) {
                return true;
            }
            return false;
    }
}
function isWhitespaceAtPos(value, index) {
    if (index < 0 || index >= value.length) {
        return false;
    }
    const code = value.charCodeAt(index);
    switch (code) {
        case 32 /* Space */:
        case 9 /* Tab */:
            return true;
        default:
            return false;
    }
}
function isUpperCaseAtPos(pos, word, wordLow) {
    return word[pos] !== wordLow[pos];
}
export function isPatternInWord(patternLow, patternPos, patternLen, wordLow, wordPos, wordLen, fillMinWordPosArr = false) {
    while (patternPos < patternLen && wordPos < wordLen) {
        if (patternLow[patternPos] === wordLow[wordPos]) {
            if (fillMinWordPosArr) {
                // Remember the min word position for each pattern position
                _minWordMatchPos[patternPos] = wordPos;
            }
            patternPos += 1;
        }
        wordPos += 1;
    }
    return patternPos === patternLen; // pattern must be exhausted
}
export var FuzzyScore;
(function (FuzzyScore) {
    /**
     * No matches and value `-100`
     */
    FuzzyScore.Default = ([-100, 0]);
    function isDefault(score) {
        return !score || (score[0] === -100 && score[1] === 0 && score[2] === 0);
    }
    FuzzyScore.isDefault = isDefault;
})(FuzzyScore || (FuzzyScore = {}));
export function fuzzyScore(pattern, patternLow, patternStart, word, wordLow, wordStart, firstMatchCanBeWeak) {
    const patternLen = pattern.length > _maxLen ? _maxLen : pattern.length;
    const wordLen = word.length > _maxLen ? _maxLen : word.length;
    if (patternStart >= patternLen || wordStart >= wordLen || (patternLen - patternStart) > (wordLen - wordStart)) {
        return undefined;
    }
    // Run a simple check if the characters of pattern occur
    // (in order) at all in word. If that isn't the case we
    // stop because no match will be possible
    if (!isPatternInWord(patternLow, patternStart, patternLen, wordLow, wordStart, wordLen, true)) {
        return undefined;
    }
    // Find the max matching word position for each pattern position
    // NOTE: the min matching word position was filled in above, in the `isPatternInWord` call
    _fillInMaxWordMatchPos(patternLen, wordLen, patternStart, wordStart, patternLow, wordLow);
    let row = 1;
    let column = 1;
    let patternPos = patternStart;
    let wordPos = wordStart;
    const hasStrongFirstMatch = [false];
    // There will be a match, fill in tables
    for (row = 1, patternPos = patternStart; patternPos < patternLen; row++, patternPos++) {
        // Reduce search space to possible matching word positions and to possible access from next row
        const minWordMatchPos = _minWordMatchPos[patternPos];
        const maxWordMatchPos = _maxWordMatchPos[patternPos];
        const nextMaxWordMatchPos = (patternPos + 1 < patternLen ? _maxWordMatchPos[patternPos + 1] : wordLen);
        for (column = minWordMatchPos - wordStart + 1, wordPos = minWordMatchPos; wordPos < nextMaxWordMatchPos; column++, wordPos++) {
            let score = Number.MIN_SAFE_INTEGER;
            let canComeDiag = false;
            if (wordPos <= maxWordMatchPos) {
                score = _doScore(pattern, patternLow, patternPos, patternStart, word, wordLow, wordPos, wordLen, wordStart, _diag[row - 1][column - 1] === 0, hasStrongFirstMatch);
            }
            let diagScore = 0;
            if (score !== Number.MAX_SAFE_INTEGER) {
                canComeDiag = true;
                diagScore = score + _table[row - 1][column - 1];
            }
            const canComeLeft = wordPos > minWordMatchPos;
            const leftScore = canComeLeft ? _table[row][column - 1] + (_diag[row][column - 1] > 0 ? -5 : 0) : 0; // penalty for a gap start
            const canComeLeftLeft = wordPos > minWordMatchPos + 1 && _diag[row][column - 1] > 0;
            const leftLeftScore = canComeLeftLeft ? _table[row][column - 2] + (_diag[row][column - 2] > 0 ? -5 : 0) : 0; // penalty for a gap start
            if (canComeLeftLeft && (!canComeLeft || leftLeftScore >= leftScore) && (!canComeDiag || leftLeftScore >= diagScore)) {
                // always prefer choosing left left to jump over a diagonal because that means a match is earlier in the word
                _table[row][column] = leftLeftScore;
                _arrows[row][column] = 3 /* LeftLeft */;
                _diag[row][column] = 0;
            }
            else if (canComeLeft && (!canComeDiag || leftScore >= diagScore)) {
                // always prefer choosing left since that means a match is earlier in the word
                _table[row][column] = leftScore;
                _arrows[row][column] = 2 /* Left */;
                _diag[row][column] = 0;
            }
            else if (canComeDiag) {
                _table[row][column] = diagScore;
                _arrows[row][column] = 1 /* Diag */;
                _diag[row][column] = _diag[row - 1][column - 1] + 1;
            }
            else {
                throw new Error(`not possible`);
            }
        }
    }
    if (_debug) {
        printTables(pattern, patternStart, word, wordStart);
    }
    if (!hasStrongFirstMatch[0] && !firstMatchCanBeWeak) {
        return undefined;
    }
    row--;
    column--;
    const result = [_table[row][column], wordStart];
    let backwardsDiagLength = 0;
    let maxMatchColumn = 0;
    while (row >= 1) {
        // Find the column where we go diagonally up
        let diagColumn = column;
        do {
            const arrow = _arrows[row][diagColumn];
            if (arrow === 3 /* LeftLeft */) {
                diagColumn = diagColumn - 2;
            }
            else if (arrow === 2 /* Left */) {
                diagColumn = diagColumn - 1;
            }
            else {
                // found the diagonal
                break;
            }
        } while (diagColumn >= 1);
        // Overturn the "forwards" decision if keeping the "backwards" diagonal would give a better match
        if (backwardsDiagLength > 1 // only if we would have a contiguous match of 3 characters
            && patternLow[patternStart + row - 1] === wordLow[wordStart + column - 1] // only if we can do a contiguous match diagonally
            && !isUpperCaseAtPos(diagColumn + wordStart - 1, word, wordLow) // only if the forwards chose diagonal is not an uppercase
            && backwardsDiagLength + 1 > _diag[row][diagColumn] // only if our contiguous match would be longer than the "forwards" contiguous match
        ) {
            diagColumn = column;
        }
        if (diagColumn === column) {
            // this is a contiguous match
            backwardsDiagLength++;
        }
        else {
            backwardsDiagLength = 1;
        }
        if (!maxMatchColumn) {
            // remember the last matched column
            maxMatchColumn = diagColumn;
        }
        row--;
        column = diagColumn - 1;
        result.push(column);
    }
    if (wordLen === patternLen) {
        // the word matches the pattern with all characters!
        // giving the score a total match boost (to come up ahead other words)
        result[0] += 2;
    }
    // Add 1 penalty for each skipped character in the word
    const skippedCharsCount = maxMatchColumn - patternLen;
    result[0] -= skippedCharsCount;
    return result;
}
function _fillInMaxWordMatchPos(patternLen, wordLen, patternStart, wordStart, patternLow, wordLow) {
    let patternPos = patternLen - 1;
    let wordPos = wordLen - 1;
    while (patternPos >= patternStart && wordPos >= wordStart) {
        if (patternLow[patternPos] === wordLow[wordPos]) {
            _maxWordMatchPos[patternPos] = wordPos;
            patternPos--;
        }
        wordPos--;
    }
}
function _doScore(pattern, patternLow, patternPos, patternStart, word, wordLow, wordPos, wordLen, wordStart, newMatchStart, outFirstMatchStrong) {
    if (patternLow[patternPos] !== wordLow[wordPos]) {
        return Number.MIN_SAFE_INTEGER;
    }
    let score = 1;
    let isGapLocation = false;
    if (wordPos === (patternPos - patternStart)) {
        // common prefix: `foobar <-> foobaz`
        //                            ^^^^^
        score = pattern[patternPos] === word[wordPos] ? 7 : 5;
    }
    else if (isUpperCaseAtPos(wordPos, word, wordLow) && (wordPos === 0 || !isUpperCaseAtPos(wordPos - 1, word, wordLow))) {
        // hitting upper-case: `foo <-> forOthers`
        //                              ^^ ^
        score = pattern[patternPos] === word[wordPos] ? 7 : 5;
        isGapLocation = true;
    }
    else if (isSeparatorAtPos(wordLow, wordPos) && (wordPos === 0 || !isSeparatorAtPos(wordLow, wordPos - 1))) {
        // hitting a separator: `. <-> foo.bar`
        //                                ^
        score = 5;
    }
    else if (isSeparatorAtPos(wordLow, wordPos - 1) || isWhitespaceAtPos(wordLow, wordPos - 1)) {
        // post separator: `foo <-> bar_foo`
        //                              ^^^
        score = 5;
        isGapLocation = true;
    }
    if (score > 1 && patternPos === patternStart) {
        outFirstMatchStrong[0] = true;
    }
    if (!isGapLocation) {
        isGapLocation = isUpperCaseAtPos(wordPos, word, wordLow) || isSeparatorAtPos(wordLow, wordPos - 1) || isWhitespaceAtPos(wordLow, wordPos - 1);
    }
    //
    if (patternPos === patternStart) { // first character in pattern
        if (wordPos > wordStart) {
            // the first pattern character would match a word character that is not at the word start
            // so introduce a penalty to account for the gap preceding this match
            score -= isGapLocation ? 3 : 5;
        }
    }
    else {
        if (newMatchStart) {
            // this would be the beginning of a new match (i.e. there would be a gap before this location)
            score += isGapLocation ? 2 : 0;
        }
        else {
            // this is part of a contiguous match, so give it a slight bonus, but do so only if it would not be a prefered gap location
            score += isGapLocation ? 0 : 1;
        }
    }
    if (wordPos + 1 === wordLen) {
        // we always penalize gaps, but this gives unfair advantages to a match that would match the last character in the word
        // so pretend there is a gap after the last character in the word to normalize things
        score -= isGapLocation ? 3 : 5;
    }
    return score;
}
//#endregion
//#region --- graceful ---
export function fuzzyScoreGracefulAggressive(pattern, lowPattern, patternPos, word, lowWord, wordPos, firstMatchCanBeWeak) {
    return fuzzyScoreWithPermutations(pattern, lowPattern, patternPos, word, lowWord, wordPos, true, firstMatchCanBeWeak);
}
function fuzzyScoreWithPermutations(pattern, lowPattern, patternPos, word, lowWord, wordPos, aggressive, firstMatchCanBeWeak) {
    let top = fuzzyScore(pattern, lowPattern, patternPos, word, lowWord, wordPos, firstMatchCanBeWeak);
    if (top && !aggressive) {
        // when using the original pattern yield a result we`
        // return it unless we are aggressive and try to find
        // a better alignment, e.g. `cno` -> `^co^ns^ole` or `^c^o^nsole`.
        return top;
    }
    if (pattern.length >= 3) {
        // When the pattern is long enough then try a few (max 7)
        // permutations of the pattern to find a better match. The
        // permutations only swap neighbouring characters, e.g
        // `cnoso` becomes `conso`, `cnsoo`, `cnoos`.
        const tries = Math.min(7, pattern.length - 1);
        for (let movingPatternPos = patternPos + 1; movingPatternPos < tries; movingPatternPos++) {
            const newPattern = nextTypoPermutation(pattern, movingPatternPos);
            if (newPattern) {
                const candidate = fuzzyScore(newPattern, newPattern.toLowerCase(), patternPos, word, lowWord, wordPos, firstMatchCanBeWeak);
                if (candidate) {
                    candidate[0] -= 3; // permutation penalty
                    if (!top || candidate[0] > top[0]) {
                        top = candidate;
                    }
                }
            }
        }
    }
    return top;
}
function nextTypoPermutation(pattern, patternPos) {
    if (patternPos + 1 >= pattern.length) {
        return undefined;
    }
    const swap1 = pattern[patternPos];
    const swap2 = pattern[patternPos + 1];
    if (swap1 === swap2) {
        return undefined;
    }
    return pattern.slice(0, patternPos)
        + swap2
        + swap1
        + pattern.slice(patternPos + 2);
}
//#endregion
