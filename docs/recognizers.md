# Recognizers

Behaviour of the recognizers for numbers, ordinals, percentages, measures, money and address-like sequences. Language-neutral where it can be; the English rules are the first data set. Rules are data: adding a language means writing rule files (see "Rule data"), not code.

## Results

`Recognizer.Recognize(text)` returns the results in text order without overlaps. Each result has UTF-16 offsets `Start` and `End` into the string passed in, its exact `Text`, a `Label`, and a `Confidence` from 0 to 1.

| Type | Label | Meaning |
|---|---|---|
| `Quantity`, `Kind = Cardinal` | `CARDINAL` | A plain number: `42`, `3.5 million`, `two hundred and five`, `a dozen`, `3/4`. |
| `Quantity`, `Kind = Ordinal` | `ORDINAL` | `1st`, `twenty-third`. `Value` is the position. |
| `Quantity`, `Kind = Percent` | `PERCENT` | `5%`, `fifty percent`. `Value` is in percent: 5, not 0.05. `Unit` and `Dimension` are `percent`. |
| `Quantity`, `Kind = Money` | `MONEY` | `$5m`, `five bucks`, `USD 20`, `50 cents`. `Currency` is the ISO 4217 code. `Value` is in that currency: 50 cents is 0.5. |
| `Quantity`, `Kind = Measure` | `QUANTITY` | A number with a unit: `5 km`, `20°C`, `3-year-old`. `Unit` is the canonical unit name, `Dimension` what it measures: `length`, `mass`, `volume`, `area`, `speed`, `information`, `temperature`, `age`. |
| `SequenceMatch`, `Email` | `EMAIL` | |
| `SequenceMatch`, `Url` | `URL` | |
| `SequenceMatch`, `IpAddress` | `IP_ADDRESS` | IPv4 or IPv6. |
| `SequenceMatch`, `PhoneLike` | `PHONE_LIKE` | **Phone-like sequences, unvalidated; use a dedicated contact-parsing library for validated, canonical phones.** |
| `SequenceMatch`, `Hashtag` | `HASHTAG` | |
| `SequenceMatch`, `Mention` | `MENTION` | |
| `SequenceMatch`, `Uuid` | `GUID` | |

`CARDINAL`, `ORDINAL`, `PERCENT`, `MONEY` and `QUANTITY` are the names OntoNotes uses for numbers, and spaCy's English models use them too. A NER model that predicts the UNER types (`PER`, `ORG`, `LOC`) does not use these names, so recognizer entities and model entities coexist in one `Doc`.

**`Value` is a `decimal`.** Money must be exact (`19.99` is `19.99`), and decimal holds 28 digits. Numbers beyond ±7.9e28 do not fit and are not reported (`one octillion`, 1e27, still fits). Values are normalized so that `2.50` and `2.5` are the same value. A `double` would have given a wider range and lost the cents.

Values are not converted between units: `5 km` is 5 with unit `kilometer`.

## Component

`RecognizerComponent` (registered as `recognizers`) writes the results into `Doc.Ents`. Entities cover whole tokens; a result that starts or ends inside a token (`5km` as one token) is widened to the tokens it touches. When two results touch the same token the earlier one stays. Existing entities (from a model) stay and win over overlapping results unless `overwrite_ents` is set, in which case the recognizer results win and the entities they overlap are removed.

## Overlaps

Every rule proposes results independently. Overlaps are settled the same way every time:

1. the longest result wins;
2. of equal length, the type earlier in this order: `URL`, `EMAIL`, `MENTION`, `HASHTAG`, `GUID`, `IP_ADDRESS`, `PHONE_LIKE`, `MONEY`, `PERCENT`, `QUANTITY`, `ORDINAL`, `CARDINAL`;
3. then the earlier start, then the higher confidence.

A result overlapping one already taken is dropped. So `$5 million` (money) beats `5 million` (a number), and `a@b.com` beats the `b` and `com` inside it.

## Numbers

### Digits

- Integer and decimal numbers with `.` as the decimal separator and `,` as the group separator (set in the rule data). A group is exactly three digits after a first run of one to three digits: `1,234,567.89` is one number; `1,2345` is two; `12,34` is two.
- A leading decimal point is a number: `.5` is 0.5.
- A dotted run of more than one decimal point (`1.2.3`, `25.12.2020`) is not a number: it is a version or a date.
- A sign: `-5` is negative when the hyphen follows white space, an opening bracket, a quote, `:`, `;`, `=`, `<`, `>`, `~`, `,` or a currency sign, or starts the text. After a digit it is a range (`3-5` is 3 and 5). After a letter it is part of a name (`COVID-19`, `B-52`: no number). `−` (U+2212) is treated like `-`. `minus` and `negative` before a number also negate it.
- A number touching letters is not a number unless the letters are an ordinal ending, a multiplier suffix, or the start of a unit: `x86`, `H2O`, `5abc` give no number.
- A proper fraction in digits: `1/2`, `3/4` (numerator below denominator, denominator up to 1000). `3/4/2020` is three numbers. `1/2` is also a date to some readers; a date recognizer takes precedence by being longer or earlier in the type order.
- Multipliers touching digits: `3.5k` is 3500. Others are only multipliers next to a currency (`$5m`, `5m dollars`), because `5m` is also five metres.
- A scale word after digits multiplies them: `3.5 million`, `2 dozen`, `5 hundred`.

### Words

- Units, teens, tens, `hundred`, and the scale words of the short scale (`thousand` to `octillion`). A group of hundreds is built by addition inside the group; scale words close a group and must descend (`one million two thousand` is fine, `one thousand million` is not read as one number).
- Words may be joined by white space (at most two white-space characters, none a line break) or a hyphen touching both words. `and` joins `hundred` or a scale word to what follows: `one hundred and five`, `two thousand and ten`.
- `a`/`an` is one before `hundred`, a scale word, `dozen` and a fraction word (`a hundred`, `a dozen`, `a half`). It is not a number elsewhere.
- `twelve hundred` is 1200 (a group below 100 times a hundred).
- `zero` stands alone.
- Fractions: `three quarters` (0.75), `a half`, `two thirds`; a fraction word needs a numerator, so `half` alone is not a number. Mixed numbers: `two and three quarters`, `one and a half`.
- Decimals: `three point one four` is 3.14: every word after `point` is one digit. A scale word may follow a decimal or a mixed number: `two and a half million`.
- The longest well-formed prefix wins: `one two` is two numbers; `one hundred hundred` is 100 followed by an unread `hundred`.

### Ordinals

- Digits with an ending that fits the number: `1st`, `2nd`, `3rd`, `4th`, `11th`, `12th`, `13th`, `21st`, `22nd`. A wrong ending (`21th`) is not an ordinal and not a number either.
- Words: `first` to `nineteenth`, `twentieth` to `ninetieth`, `hundredth`, `thousandth`, ..., and compounds `twenty-third`, `one hundred and first`.
- `second` is also a unit of time, so on its own right after a number it is not an ordinal (`one second` is the number one); after tens it is (`twenty second` is 22nd).

## Percent

A number followed (at most one white-space character between) by `%` or a percent word: `5%`, `5 %`, `fifty percent`, `12.5 per cent`.

## Measures

A number, then (touching it, after one white-space character, or after one hyphen touching both) a unit from the data: `5km`, `5 km`, `5-foot`, `two miles per hour`, `100 km/h`, `20°C`, `68 degrees Fahrenheit`. The longest unit form wins. A unit must not be followed by a letter or digit touching it (`5 kg2` is no unit).

Unit forms are matched without regard to case unless the data marks them case-sensitive (`MB` is megabytes, `Mb` megabits, `m` metres). Short forms that are everyday words (`in`, `a`, `at`, `min`) are not units.

**Age** is a number and a time unit followed by `old`: `5 years old`, `a 3-year-old boy` (Unit `year`, Dimension `age`). A bare duration (`5 years`) is not recognized here.

## Money

- A currency sign or code before the number, touching it or after one white-space character: `$5`, `$ 5`, `USD 5`, `€20`, `US$5`; a leading minus before the sign: `-$5`; a minus after it: `$-5`.
- A sign, code or name after the number: `20€`, `100 USD`, `5 dollars`, `five bucks`, `50 cents`, `10 zł`.
- Currency names may have several words (`new zealand dollars`). A name may carry a factor: `cents` is 0.01 of its currency.
- Codes are the alphabetic codes of ISO 4217 in capitals. Lower-case `usd` is not read because many codes are English words (`ALL`, `TRY`, `TOP`).
- Ambiguous signs get the common reading and a confidence below 1: `$` is USD (0.9), `¥` is JPY (0.7), `dollars` is USD (0.9). Signs that pin down a currency (`US$`, `A$`, `CA$`, `€`, `£`) have confidence 1.
- `pounds` is a unit of mass; `pound sterling` and `pounds sterling` are currency.
- A multiplier touching the number counts next to a currency: `$5m` is 5,000,000; `$1.2 billion`; `5m dollars`.

## Sequences

The shape of each sequence is fixed by a standard, so it lives in code; what varies by deployment (URL schemes, domain endings) is rule data.

### E-mail

A simplified addr-spec of RFC 5322 §3.4.1: a dot-atom local part of at most 64 characters (RFC 5321 §4.5.3.1.1) (no quoted strings, comments or folding), `@`, and a domain of dot-separated labels (up to 63 characters each, 253 in all, RFC 1035 §2.3.1), with at least two labels, the last a known domain ending (or an `xn--` ending). Non-ASCII letters and digits are accepted in both parts (RFC 6531). The local part starts at a letter, digit or `_`, and ends before the `@` without a dot; two dots in a row end the match. Trailing dots and hyphens after the domain are not part of it. `Normalized` lower-cases the domain.

### URL

RFC 3986 §3 (`scheme://[userinfo@]host[:port]/path?query#fragment`) with the WHATWG URL Standard's leniency for addresses written without a scheme:

- With `scheme://`: the scheme must be one listed in the rule data (so `note://abc` in prose is left alone). The host is a registered name, an IPv4 address, or a bracketed IPv6 address; the port is 1 to 5 digits; the authority may be empty (`file:///tmp/x`).
- Starting with `www.`: the host needs at least three labels and a known domain ending.
- Bare (`example.com/page`): the last host label must be in the shorter bare list of the rule data (`com`, `org`, `uk`, ...; not `md`, `py`, `sh`, `it`, which are also file extensions and words), so that `README.md`, `node.js` and `e.g.` are not URLs.
- Path, query and fragment run over RFC 3986 characters and non-ASCII letters, digits and symbols, up to 2048 characters in all.
- Trailing `! ' * , . : ; ?` are dropped (RFC 3986 Appendix C), and a closing `)` or `]` is dropped unless its opening bracket is inside the match.
- An address is not resolved or checked to exist.

### IP address

- IPv4 (RFC 791 dotted decimal): four decimal parts 0–255 without leading zeros (`010.1.1.1` and `256.1.1.1` are not addresses), not part of a longer dotted run (`1.2.3.4.5`).
- IPv6 (RFC 4291 §2.2): eight 16-bit hex groups, one `::`, an IPv4 tail allowed. `Normalized` is the RFC 5952 §4 form (lower case, no leading zeros, the longest run of at least two zero groups compressed, the first on a tie). Trailing sentence punctuation is dropped. `::` alone, text where a hex word touches letters (`std::vector`) and runs with too many groups are not addresses. Zone identifiers (`%eth0`) are not read.

### Phone-like

Syntax only, nothing verified, confidence 0.7. `Normalized` is an optional `+` and the digits. Accepted shapes (digits in all 7 to 15; E.164 caps a number at 15):

- `+` then a country code of one to three digits and more groups: `+44 20 7183 8750`, `+1 (555) 123-4567`; or `+` and 8–15 digits together;
- groups with a parenthesized group: `(415) 555-0132` (9 or more digits);
- groups joined by one hyphen or dot, three to five groups of two to four digits (a leading group of one digit is allowed with four or more groups: `1-800-555-0199`): `555-123-4567`, `01.23.45.67.89`; or exactly `ddd-dddd`;
- space-separated groups only when they start with `0` (national prefix): `020 7183 8750`.

Not accepted: dates (`2020-01-15`, `15.01.2020`), decimals (`3.14159265`), plain digit runs, groups of digits separated by spaces without a prefix (`4111 1111 1111 1111`), anything touching letters.

### Hashtag, mention, GUID

- `#` and one or more letters, digits or `_` (Unicode letters and marks included), not touching a preceding letter, digit, `_`, `&`, `#` or `/`; all-digit tags (`#2024`) are numbers, not hashtags; at most 100 characters. `Normalized` is the tag lower-cased without the `#`.
- `@` and a user name of letters, digits and `_`, with `.` and `-` inside, not touching a preceding letter, digit, `_`, `@`, `+` or `.`; an `@host.example` after the name is part of a fediverse handle. `Normalized` is the name lower-cased without the `@`.
- GUID (RFC 9562 §4): 8-4-4-4-12 hexadecimal digits, optionally in braces or after `urn:uuid:`. Version and variant bits are not checked. Not touching a letter, digit or hyphen. `Normalized` is 36 lower-case characters.

## Rule data

Rule files are UTF-8 text. `#` starts a comment. `[section]` starts a section; each other line is a rule whose fields are separated by `|`. A section may appear in several files; a language is a set of files (`Recognizers/Data/<code>/*.rules` embedded in the assembly, or files given to `Recognizer.FromFiles`).

In a list of forms, forms are separated by commas, and may have several words. A form starting with `=` is matched case-sensitively; others are matched without regard to case. Forms are cut into words, digit runs and single symbols by the same lexer as the text, so `km/h` in the data is three items touching each other, and matches `km/h` but not `km / h`.

| Section | Fields | Meaning |
|---|---|---|
| `number.digits` | `key \| value` | `decimal`, `group` (one character each), `negative` (characters read as a minus) |
| `number.cardinal` | `word \| kind \| value` | `kind` is `unit` (0–9), `teen` (10–19), `tens`, `scale` (100 and up; 100 is "hundred") or `multiplier` ("dozen") |
| `number.ordinal` | `word \| value [\| not-after-number]` | An ordinal word; the flag keeps `second` out right after a number |
| `number.fraction` | `word \| denominator` | `half`, `thirds`, ... |
| `number.article`, `number.connector`, `number.negative`, `number.point` | `word` | `a`/`an`; `and`; `minus`; `point` |
| `number.suffix` | `form \| multiplier \| any or money` | `k`, `m`, `bn` touching digits |
| `number.ordinal-suffix` | `ending \| last digits \| any, teen or not-teen` | `st \| 1 \| not-teen`: `1st`, not `11st` |
| `percent` | `forms` | `%`, `percent`, `per cent` |
| `unit` | `canonical \| dimension \| forms [\| confidence]` | |
| `age.unit`, `age.suffix` | `canonical \| forms`; `forms` | Combined as `5 years old` and `5-year-old` |
| `currency.symbol` | `form \| ISO code \| prefix, suffix or both [\| confidence]` | |
| `currency.name` | `forms \| ISO code [\| factor [\| confidence]]` | |
| `currency.code` | `codes` | Alphabetic ISO 4217 codes, in capitals, separated by commas or spaces |
| `url.scheme`, `url.tld`, `url.tld.bare` | `items` | Schemes read with `://`; all domain endings; the endings allowed for bare addresses |

Limits, checked while loading: 8 MB per file, 200,000 lines, 8192 characters per line, 8 fields per rule, 16 words or symbols per form. A malformed file throws `RecognizerRulesException` naming the line: invalid UTF-8, a rule before any section, an unknown section, a wrong field count, a number that does not parse, an unknown kind or position.

Words and symbols (not numbers or dates) are what the rules recognize; the sequence syntaxes and the arithmetic of the number grammar are code. A language whose numbers are not built from additive and multiplicative words in the short scale (French `quatre-vingts`, German `einundzwanzig`) needs engine support in addition to data; that is a known limit, and a language of the same shape as English needs data only.

## Limits and decisions

- Grouping in the Indian style (`12,34,567`) is not read as one number.
- Scientific notation (`1e5`) is not read.
- `5 feet 3 inches` gives two measures, not one; there is no unit conversion.
- Number words glued to digits (`5 hundred thousand`) read the first scale word only.
- Phone-like detection will miss shapes outside the list above and will accept some number groups that are not phones: it is a guess, with confidence 0.7.
- The engine takes a `RecognitionContext` with a reference date so that rules for relative dates (added later) use the same call; the rules here ignore it.
