# Recognizers

Behaviour of the recognizers for numbers, ordinals, percentages, measures, money, address-like sequences, and dates and times. Language-neutral where it can be; the English rules are the first data set. Rules are data: adding a language means writing rule files (see "Rule data"), not code.

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
| `TemporalMatch`, `Kind = Date` or `DateRange` | `DATE` | A day or a period of days: `March 5, 2026`, `next Friday`, `spring 2021`. |
| `TemporalMatch`, `Kind = Time` or `TimeRange` | `TIME` | A time of day or a range of times of day: `3pm`, `from 9am to 5pm`. |
| `TemporalMatch`, `Kind = DateTime` or `DateTimeRange` | `DATETIME` | A day and a time, or a part of a day: `tomorrow at 3pm`, `Saturday evening`. |
| `TemporalMatch`, `Kind = Duration` | `DURATION` | An amount of time: `two weeks`, `1 year and 6 months`. |
| `TemporalMatch`, `Kind = Set` | `SET` | A recurrence: `every Monday at 9am`, `daily`. |

`CARDINAL`, `ORDINAL`, `PERCENT`, `MONEY` and `QUANTITY` are the names OntoNotes uses for numbers, and spaCy's English models use them too. A NER model that predicts the UNER types (`PER`, `ORG`, `LOC`) does not use these names, so recognizer entities and model entities coexist in one `Doc`.

**`Value` is a `decimal`.** Money must be exact (`19.99` is `19.99`), and decimal holds 28 digits. Numbers beyond ±7.9e28 do not fit and are not reported (`one octillion`, 1e27, still fits). Values are normalized so that `2.50` and `2.5` are the same value. A `double` would have given a wider range and lost the cents.

Values are not converted between units: `5 km` is 5 with unit `kilometer`.

## Component

`RecognizerComponent` (registered as `recognizers`) writes the results into `Doc.Ents`. Entities cover whole tokens; a result that starts or ends inside a token (`5km` as one token) is widened to the tokens it touches. When two results touch the same token the earlier one stays. Existing entities (from a model) stay and win over overlapping results unless `overwrite_ents` is set, in which case the recognizer results win and the entities they overlap are removed.

## Overlaps

Every rule proposes results independently. Overlaps are settled the same way every time:

1. the longest result wins;
2. of equal length, the type earlier in this order: `URL`, `EMAIL`, `MENTION`, `HASHTAG`, `GUID`, `IP_ADDRESS`, `DATE`/`TIME`/`DATETIME`/`DURATION`/`SET`, `PHONE_LIKE`, `MONEY`, `PERCENT`, `QUANTITY`, `ORDINAL`, `CARDINAL`;
3. then the earlier start, then the higher confidence.

The date and time labels rank after `IP_ADDRESS` and before `PHONE_LIKE`, so `2031` is a year, not a cardinal, and `30 minutes` is a duration, not a measure.

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

## Dates and times

`Recognizer.Recognize(text, new RecognitionContext(reference) { Region = "us" })` resolves dates and times to values. `Nlp.Dates(text, reference)` lists them, and `Nlp.ParseDate(text, reference)` returns the first as a `DateRange`. The English rules are `Data/en/datetime.rules` (vocabulary) and `Data/en/holidays.rules` (holiday table); the grammar that combines the words is code.

### Result

`TemporalMatch` has the `Kind` (below), a `Value` string, a `Granularity` (null for durations), an `Approximate` flag, the `UtcOffset` the text names, and a `Range` (`DateRange`: `Start` inclusive, `End` exclusive, `Granularity`, `Approximate`) on the calendar. A day runs from its midnight to the next; an instant such as `3pm` runs for its precision (an hour; `3:05pm` a minute). A time of day with no date is placed on the reference day; with no reference date its `Range` is null. Durations and sets have no `Range`; a fixed-length duration has `Amount` as a `TimeSpan`.

| Kind | Examples | `Value` |
|---|---|---|
| `Date` | `March 5, 2026`, `tomorrow`, `the 4th of July`, `Easter 2027`, `in 3 days`, `a week ago` | `2026-03-05` |
| `Time` | `3pm`, `7:05 AM`, `15:45`, `noon`, `5 in the afternoon`, `3.30pm`, `9am JST` | `T15:00`, `T09:00+09:00`; `Z` for UTC and GMT |
| `DateTime` | `tomorrow at 3pm`, `3pm on March 22`, `in 45 minutes`, `2026-06-30T09:15:00Z` | `2026-03-19T15:00` |
| `DateRange` | `next week`, `last winter`, `the first quarter of 2026`, `late 2023`, `the 1990s`, `from April 6 to April 10`, `May 4-8`, `1990-2000`, `the past three days` | `2026-03-23/2026-03-30` |
| `TimeRange` | `9am-5pm`, `from 6 pm until 9 pm`, `between 12 and 1 pm` | `T09:00/T17:00` |
| `DateTimeRange` | `tomorrow morning`, `Saturday evening`, `Monday from 9am to 11am`, `from Monday 8am to Monday 6pm` | `2026-03-23T09:00/2026-03-23T11:00` |
| `Duration` | `two weeks`, `45 seconds`, `an hour and a half`, `2 hours and 30 minutes` | seconds as an integer (`1209600`); ISO 8601 `P1Y6M` when it names months or years |
| `Set` | `every Monday`, `every Monday at 9am`, `daily`, `every 3 hours`, `every other day`, `every day at noon` | TIMEX3: `XXXX-WXX-1`, `XXXX-WXX-1T09:00`, `P1D`, `PT3H`, `P2D`, `T12:00` |

`Granularity` is the unit the expression is stated in: for an instant its precision (`Second`, `Minute`, `Hour`, `Day`); for a range the kind of period (`Week`, `Month`, `Quarter`, `Half`, `Season`, `Year`, `Decade`, or `Day` for explicit day ranges and weekends, `Hour` or `Minute` for ranges of times); for a set the recurrence unit. This is the gold-set convention (`docs/recognizer-gold.md`).

### Reference date, zones and region

- **Relative expressions need a reference date** (`today`, `next Friday`, `in 3 days`, `Christmas` with no year, `the 25th`, `March 5` with no year). Without one (`RecognitionContext.ReferenceDate` null) they are not reported; absolute ones are (`March 5, 2027`, `3pm`, `two weeks`, `every Monday`). Wall-clock expressions are read in the reference's offset.
- **Zones are explicit.** `PST`, `UTC+2`, `GMT-05:00` and a trailing `Z` or `+hh:mm` after a time are kept in `Value` and `UtcOffset` and become the offset of `Range`. The recognizer carries no time zone database: a named zone is a fixed offset, so there is no daylight-saving arithmetic (`PST` is always -08:00; `PDT` is -07:00). A reference date in `America/New_York` is just its `DateTimeOffset`. The abbreviation table is `time.zone`; ambiguous abbreviations (`CST`, `BST`, `IST`) take the reading English text usually means, and `IST` is left out.
- **Region** (`RecognitionContext.Region`, `us` default or `gb`) picks the holiday rows and the order of numeric dates: `04/05/2026` is April 5 in `us` and 4 May in `gb`; a first number above 12 and a dotted date are always day first.

### How expressions resolve

- **Weekdays.** A bare weekday or `this` weekday is the first such day on or after the reference day (today counts). `next Friday` is Friday of the next week (weeks run Monday to Sunday). `last Friday` is the latest one before today. `Wednesday of next week` is that day in that week. `Monday, March 23` is the date; the weekday is not checked.
- **Missing year.** A month and day, a holiday or `the 25th` is the first such day on or after the reference day, so a date that has passed this year is next year's.
- **Ranges are half-open.** The end of a range of days or longer periods is the start of the period after the stated end (`from April 6 to April 10` ends at `2026-04-11`; `through 2027` at `2028-01-01`). A clock end point is the instant stated (`from 3pm to 5pm` is `T15:00/T17:00`). A second weekday or month-day counts from the first (`from Monday to Friday` is the week of the Monday it names; `December 28 to January 3` crosses the year).
- **Times of day.** `3pm` and `3:30 pm` have am/pm; `15:45` and `09:00` are 24-hour; `H:MM` with no am/pm is read as a clock time; a bare hour (`at 9`) is not a time, because it has no am/pm. In a range the first end takes the second's am/pm (`between 12 and 1 pm`, `from 9 to 5 pm`), moving to am when that would put it after the end (`between 11 and 1 pm`). `5 in the afternoon` and `10 at night` use the day part. `noon` and `midnight` are `12:00` and `00:00`.
- **Parts of a day** are approximate ranges: morning 06:00-12:00, afternoon 12:00-18:00, evening 18:00-24:00, night 20:00-24:00. `tonight` is the night of today, `last night` the night of yesterday, `this morning` the morning of today.
- **Periods.** Weeks run Monday to Sunday; `this weekend` is Saturday and Sunday of the current week and `next weekend` those of the next week. Seasons are meteorological, northern hemisphere (spring March-May, summer June-August, fall September-November, winter December-February), approximate, and `winter 2024` starts in December 2024. `early`, `mid` and `late` split a year into January-April, May-August and September-December and a month into days 1-10, 11-20 and 21-end, approximate. `H1`/`Q3` and `the second half of ...` give halves and quarters; with no year they are in the reference year. `last winter` is the latest season that has ended; `this summer` the one that contains today or comes next.
- **Offsets.** `in 3 days`, `3 days ago`, `a year from now`, `the day after tomorrow`, `two days before Christmas`, `the third Thursday of April`, `the first Monday after Easter 2027`. Units of a day or more give a date; hours, minutes and seconds give a date-time at that precision.
- **Durations** are an amount and a unit (`two weeks`, `a day`, `half an hour`, `1.5 hours`, `an hour and a half`); several terms join with `and` or a comma when each is smaller than the one before (`2 hours and 30 minutes`; `seven days a week` is not one duration). `a second` alone is not a duration.
- **Sets** are `daily`, `weekly`, `monthly`, `quarterly`, `yearly`, `every`/`each` with a weekday or an optional count and a unit, and `every other`. With a time (`every Monday at 9am`, `daily at noon`) the value carries it.

### Holidays

The table is `Data/en/holidays.rules`: forms | rule | regions (`us`, `gb`, `all`). Rules are `fixed MM-DD`, `nth N|last WEEKDAY MONTH [+-DAYS]`, `easter +-DAYS` and `nearest WEEKDAY MM-DD`. Easter is the Western (Gregorian) date from the "Anonymous Gregorian" computus (Meeus, *Astronomical Algorithms* ch. 8, after Butcher 1876), valid for every Gregorian year; the other moveable feasts count from it (Good Friday is -2, Ash Wednesday -46, Pentecost +49). The dates are the calendar days of the observances, not the weekday a holiday is observed in lieu of a weekend.

| Region | Holidays |
|---|---|
| `us` | the United States federal holidays of 5 U.S.C. 6103 (New Year's Day, Martin Luther King Jr. Day, Washington's Birthday/Presidents' Day, Memorial Day, Juneteenth, Independence Day, Labor Day, Columbus Day, Veterans Day, Thanksgiving, Christmas), Election Day, Tax Day, Flag Day, Groundhog Day, Mother's Day (second Sunday of May) |
| `gb` | the bank holidays of England and Wales as published at gov.uk/bank-holidays (New Year's Day, Good Friday, Easter Monday, Early May, Spring and Summer bank holidays, Christmas Day, Boxing Day), Mothering Sunday (three weeks before Easter, also "Mother's Day"), Remembrance Sunday, Bonfire Night, St David's, St George's and St Andrew's days, Burns Night, Whit Monday |
| `all` | Valentine's Day, St Patrick's Day, April Fools' Day, Halloween, Thanksgiving, Black Friday, Cyber Monday, Christmas Eve and Day, Boxing Day, New Year's Eve, Easter and its feasts, Father's Day, Pi Day, May Day, Cinco de Mayo, Earth Day, Epiphany, Armistice Day, Leap Day |

Holidays that follow a lunar calendar (Eid, Diwali, Passover, Hanukkah, Ramadan, Lunar New Year) are not in the table: they need a calendar table per year, which is data a later set of rules can add.

### Rule data for dates and times

| Section | Fields | Meaning |
|---|---|---|
| `time.month` | `1-12 \| forms` | Month names and abbreviations. Forms starting `=` are case-sensitive, which keeps `May` and `March` from matching the verbs |
| `time.weekday` | `1-7 \| forms [\| plural forms]` | Monday is 1; plurals make a weekly set |
| `time.unit` | `name \| forms \| seconds \| months` | `day \| day, days \| 86400 \| 0` |
| `time.season` | `name \| forms \| first month \| months` | |
| `time.daypart` | `name \| forms \| start hour \| end hour` | End 24 is midnight |
| `time.meridiem` | `am or pm \| forms` | `a.m.`, `p.m.` included |
| `time.word` | `role \| forms` | The words the grammar uses: `today`, `next`, `ago`, `from`, `rangeto` (to, until, `-`), `early`, `approx`, `zonebase`, ... The roles are listed in `datetime.rules` |
| `time.recurrence` | `TIMEX3 period \| unit \| forms` | `P1W \| week \| weekly` |
| `time.zone` | `forms \| offset minutes [\| z]` | `z` writes the zone as `Z` |
| `time.holiday` | `forms \| rule \| regions` | See above |

A malformed temporal row throws `RecognizerRulesException` with the line, like the others.

### Limits

- English only. Day-first order for slashes is by region and by the numbers (a first number above 12), not by learned usage.
- No time zone database, no daylight saving, no calendars other than Gregorian, and no lunar holidays.
- No fuzzy or vague quantities (`a couple of days`, `a few weeks`), no `end of the month`, `the weekend of March 21`, `Friday the 13th`, `every second Tuesday`, `twice a week`, or fiscal periods.
- A bare weekday is the next one, even in past-tense text (`said on Tuesday` resolves forward); a bare month or season (`in March`, `in summer`) and `M/D` without a year are not reported, because they need context to mean one thing.
- A bare four-digit number from 1000 to 2999 is read as a year (`2031`, `1969`) at confidence 0.8, so a count such as `2000 people` is also reported as a year; a measure or money amount of the same length wins when the number has a unit.
- `in 45 minutes` is a time ahead, also in text such as `ran 5 miles in 45 minutes`.

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
- The engine takes a `RecognitionContext` with a reference date and region; the number, unit, currency and sequence rules ignore it, the date and time rules use it.
