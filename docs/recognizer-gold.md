# Recognizer gold set (English)

A hand-labelled set of numbers, quantities, sequences and dates/times in short sentences. It judges recognizers: ours and two black-box comparators. We wrote every sentence and label ourselves from the type definitions, ISO 4217, the RFCs and SI, and everyday text. No comparator's specification or test data was used.

## Files

| File | Role |
|---|---|
| `testdata/recognizers/en/gold.jsonl` | **Development split** (245 sentences, 237 items). Open for everyone. |
| `testdata/recognizers/en/test.jsonl` | **Held-out test split** (313 sentences, 312 items). |
| `tools/recognizers-gold/en.src.txt` | The authoring source both files are built from (it holds both splits). |
| `tools/recognizers-gold/build.py` | Validates the source and writes the two files. |
| `testdata/recognizers/en/comparators/` | Scores of the comparators on both splits. |

SHA-256 at freeze (second batch): `gold.jsonl` `6ad9be4e3f9fd8aa25c1565769b95adfc97e02c006d64e6893a34a9f03f09cd2`, `test.jsonl` `dd81db526c51e91404e256b0804c7bc1a361a610876db142a70e3c027b7b3bef`. The first freeze (220 + 160 sentences, test `7e985dc1…`) is a subset of both: no sentence changed split.

**The separation rule.** Work on a recognizer reads and tunes against `gold.jsonl` only. Nobody who builds or tunes a recognizer opens `test.jsonl`, `en.src.txt` or the comparators' scores on `test`, and test items are never copied into rules, vocabularies, unit tests or training data. The test split is scored once per milestone and reported as it comes. The files are committed, so the rule rests on discipline. Adding sentences later is allowed, but never moves one between splits: re-run `build.py` only after reading its stratification rule below, and compare the test hash.

**Split.** The first 380 sentences were assigned by a stratified hash rule: they are grouped by the type of their first item (sentences with no item form their own group). Within a group they are ordered by the SHA-256 of their id and the first 40% (rounded up) are test. The second batch (a second set of test sentences that raises every type to at least 10 test items, and a few development sentences with the same patterns) is assigned explicitly by `@split test` and `@split dev` lines in the source, so adding it moved no earlier sentence. The id is the first 12 hex digits of the SHA-256 of `reference + "\n" + text`.

## Line schema (JSON Lines, UTF-8)

```json
{"id":"…","text":"…","reference":"2026-03-18T10:30:00","timezone":"UTC",
 "items":[{"start":21,"end":27,"type":"duration","text":"a week","value":"604800"}]}
```

- `reference` is the instant relative expressions resolve against, as a UTC wall-clock time without an offset; `timezone` is always `UTC`. Almost every sentence uses `2026-03-18T10:30:00` (a Wednesday). Three groups of sentences use other references: `2026-12-31T23:30:00` (year boundary), `2028-02-28T09:00:00` (leap day), `2026-01-05T08:00:00` (a Monday).
- `start`/`end` are offsets into `text` in UTF-16 code units, `end` exclusive. `text` is the covered text.
- `type` is one of the 22 types below. `value` is the normalized resolution (absent for sequences). `grain` and `approx` (default false) apply to the temporal types. `alt` lists other accepted values for an ambiguous expression.
- A prediction file has the same item shape (`id`, then `items` with `start`, `end`, `type`, `value`, `grain`, `approx`) and no text.

## Types and value forms

All numbers are the shortest decimal string: no thousands separators, no exponent, no trailing zeros (`1250`, `0.75`, `-7`, `7900000000`).

| Type | Covers | Value |
|---|---|---|
| `number` | cardinals in digits or words, fractions of a million etc. (`a dozen`, `2.5 thousand`, `minus twelve`) | the number |
| `ordinal` | `third`, `21st`, `1,000th` | the integer |
| `percentage` | `12%`, `twelve percent`, `7 per cent` | the number without `%` |
| `dimension` | length, mass, volume, area, speed, information, power | `<number> <unit>` |
| `temperature` | `25 degrees Celsius`, `72°F`, `20 degrees` | `<number> celsius\|fahrenheit\|kelvin\|degree` |
| `age` | `5 years old`, `3-year-old`, `90 years of age` | `<number> year\|month\|week\|day` |
| `currency` | symbol, ISO code or word, with the amount | `<number> <ISO 4217 code>` |
| `email`, `url`, `ip`, `phone`, `hashtag`, `mention`, `guid` | the Sequence set | none |
| `date` | one calendar day | `YYYY-MM-DD` |
| `time` | a time of day, no date | `Thh:mm[:ss]` + zone |
| `datetime` | a day and a time of day | `YYYY-MM-DDThh:mm[:ss]` + zone |
| `daterange` | a named period (week, month, quarter, year, season…) or an explicit range of days | `start/end` dates |
| `timerange` | a range of times of day | `Thh:mm/Thh:mm` |
| `datetimerange` | a range between date-times | `…T…/…T…` |
| `duration` | an amount of time | seconds as an integer; if it names years or months, ISO 8601 `P…Y…M` (`P1Y6M`, `P3M`) |
| `set` | a recurrence | TIMEX3-style: `P1D` (daily), `P1W`, `P2W`, `P1M`, `P1Y`, `PT3H`, `XXXX-WXX-1` (every Monday), `XXXX-WXX-1T09:00`, `T12:00` (every day at noon) |

- **Units** are lower-case singular English names: `kilometer`, `foot`, `mile per hour`, `kilometer per hour`, `square meter`, `gigabyte`, `kilowatt`. `degree` is a temperature with no scale.
- **Zones.** A zone is written only when the expression names one: `Z` for UTC and GMT, otherwise the offset of the abbreviation (`PST` -08:00, `EST` -05:00, `CST` -06:00, `CET` +01:00, `JST` +09:00). The abbreviation is part of the span (`3pm PST`).
- **Temporal values.** An instant (`date`, `time`, `datetime`) has a `grain` (`second`, `minute`, `hour`, `day`) and the value is its start; precision follows the expression (`3pm` is `T15:00` with grain `hour`; `10:15:30` has seconds). A value that spells out zero seconds equals one that does not. A range (`…range`) is half-open `start/end`. `grain` is the unit the range is stated in (`week`, `month`, `quarter`, `half`, `season`, `year`, `decade`, or `day` for explicit day ranges, `hour`/`minute` for time ranges). For `set`, the unit of the recurrence.
- **Range ends.** The end of a range of days or longer periods is the start of the period after the stated end point, so "from April 6 to April 10" ends at `2026-04-11` and "through 2027" ends at `2028-01-01`. A clock end point is the stated instant: "from 3pm to 5pm" is `T15:00/T17:00`.
- **Time of day only.** A gold `time` or `timerange` value starts with `T`. A prediction that adds a date (as a system does when it resolves "3pm" to a day) is compared by its time of day alone.
- **`approx`** is true for expressions that name a loose part of a period: `early/mid/late` + period, seasons, `around 3pm`, and day parts (`tomorrow morning`). Early/mid/late split a year into Jan–Apr, May–Aug, Sep–Dec; a month into days 1–10, 11–20, 21–end. Seasons are the meteorological ones in the northern hemisphere: spring Mar–May, summer Jun–Aug, fall Sep–Nov, winter Dec–Feb. Day parts: morning 06:00–12:00, afternoon 12:00–18:00, evening 18:00–24:00.

## Labelling rules

1. **Span.** The smallest span that carries the value, including the unit, currency symbol, zone, the connectives of a range (`from … to`, `between … and`, `-`) and sign words (`minus`), but not a leading preposition (`on`, `at`, `in`, `by`) or trailing punctuation. `3pm on March 22` is one `datetime`; `tomorrow at 3pm` is one `datetime`.
2. **One label per span.** The outermost expression wins. The `3` of `3 km`, the `2021` of `early 2021` and the `22` of `3pm on March 22` are not separate numbers. The scorer ignores a prediction strictly inside a gold item of another type; a prediction with the same span but another type is a false positive.
3. **Number words.** Labelled when they are numbers (`three pizzas`), not when pronouns (`one of them`). No sentence of the set depends on that distinction.
4. **Sequences.** A URL needs a scheme, `www.` or a recognisable host with a path (`example.com/pricing`) and does not include trailing punctuation. An e-mail address contains no separate URL or mention. Phone-like means a syntactic phone number (unvalidated); the numbers are in the fictional `555-01xx` range or documented examples.
5. **Sentences with nothing.** Fifteen sentences contain nothing to find; any prediction on them is a false positive.
6. **Missing year.** An expression with a month and day but no year is in the reference year; the set keeps those after the reference date. A holiday or weekday with no further words is the next occurrence on or after the reference date.

### Ambiguity policy

An expression with two defensible readings gets the primary value and the other readings under `alt`. A system is right if it matches any of them.

- `next <weekday>`: if the weekday is later in the ISO week than the reference (Thu–Sun for a Wednesday), the next occurrence this week and next week's both count (`next Friday`: `2026-03-27`, alt `2026-03-20`). For earlier weekdays it is the one in the following week.
- `last <weekday>`: the most recent past occurrence; for a weekday earlier in the reference week, last week's also counts (`last Monday`: `2026-03-16`, alt `2026-03-09`).
- `from Monday to Friday`, when Monday has passed this week: the coming week, alt this week.
- Day-first or month-first dates: month first (en-US) for `04/15/1990`. The set has no sentence where day-first would change the value.
- Weeks run Monday to Sunday. `this weekend` is Saturday and Sunday.
- Time of day without am/pm: only 24-hour forms (`15:45`, `09:00`) or forms with a day part (`5 in the afternoon`) are used.

## Scoring (`tools/OneNlp.Eval recognizers`)

```
dotnet run --project tools/OneNlp.Eval -- recognizers --gold testdata/recognizers/en/test.jsonl --pred PRED.jsonl \
    --component NAME --dataset NAME [--resamples 1000] [--seed N] [--json OUT] [--markdown OUT] [--errors OUT.tsv]
```

Per type, and for two pooled views, **temporal (any subtype)** (matches regardless of which of the six date/time types is used, because a system with no notion of the split cannot be faulted for it) and **all types**:

- **Precision, recall, F1:** exact match of start, end and type. Duplicate predictions of one span and type count once.
- **Relaxed F1:** a gold item is found, and a prediction is right, if it overlaps an item of the same type. Read this next to the exact row to separate span conventions from missing items.
- **Value accuracy:** among exactly matched gold items that have a value, the fraction whose predicted value equals the gold value or an `alt`. **Value+grain+approx** also needs the same `grain` and `approx`. **Value accuracy on overlapping spans** uses the widest-overlapping prediction of the same type instead of an exact match.
- **Intervals:** 95% percentile bootstrap over sentences (1000 resamples, seed 20260101), the method of `docs/measurement.md`. With a handful of items the interval is degenerate (`100.0 [100.0, 100.0]`) or very wide; the table gives the item counts.
- `--errors` lists misses, wrong values and spurious predictions with the sentence text. Use it on the development split only.

## Comparators

Both run as black boxes: released builds, called through their documented public interfaces, with the same reference time per sentence.

| System | Version | Licence | How it runs |
|---|---|---|---|
| Microsoft.Recognizers.Text | `Microsoft.Recognizers.Text`, `.Number`, `.NumberWithUnit`, `.DateTime`, `.Sequence` 1.8.13 (NuGet) | MIT (NuGet metadata) | `tools/oracles/Recognizers`: English culture; the per-model `Recognize…` calls; date/time with `DateTimeOptions.None` and the sentence's reference time |
| Duckling | `rasa/duckling:0.2.0.2-r4`, image digest `sha256:55e7adfb6d63eb0d1c1bdb8a28080a74d4d281871258c2ca2d7cd0eaf29b2d11` (linux/amd64) | BSD (Duckling's README) | `tools/oracles/duckling`: `fetch.sh` downloads the published image's layers; the released `duckling-example-exe` runs as a local server; `POST /parse` with `locale=en_US`, `reftime`, `tz=UTC` |

### Comparator mappings

Both comparators are mapped into this schema by scripts of ours (`tools/oracles/Recognizers/map_raw.py`, `tools/oracles/duckling/map_duckling.py`); the headers of those files are the normative description. The choices that move scores:

- **Candidates.** Recognizers.Text lists several values for ambiguous input (past and future Friday, am and pm). We take the first that starts on or after the reference date, else the last. Duckling's primary value is taken.
- **Types.** Recognizers.Text has the six date/time types and `duration` and `set`. Duckling has one `time` dimension: an instant with a grain of day becomes `date`; a longer grain becomes a `daterange` of one period; a finer grain becomes `time` or `datetime` depending on whether the matched text names a day; intervals become the matching range type. Duckling has no percentage, age, IP, hashtag, mention, GUID or recurrence dimension, so those rows score 0 by construction. Its interval results for money and quantities are dropped.
- **Spans.** Recognizers.Text reports an inclusive end (we add 1); both report their own span conventions. Duckling includes leading prepositions (`on Christmas Day 2027`, `at 3pm`) and some trailing words in its spans, so its exact-span rows are low where its relaxed rows are not. We do not trim spans.
- **Range ends.** Duckling's interval ends are exclusive at the end point's grain; for ranges with clock end points we subtract one grain so that "3pm to 5pm" ends at 17:00. Recognizers.Text states the end of "from A to B" day ranges as B itself, not the day after, and we take it as reported; those items count against it.
- **Years.** Both put a past month and day in the next year. The development set avoids such expressions; this is why `from April 6 to April 10` replaced a March example.
- **Zones.** Neither keeps a stated zone as an offset in the form of the gold (Recognizers.Text drops it, Duckling converts to UTC); items with a zone are scored as they come.
- **Not mapped.** Duckling's `quantity` products and credit-card or other dimensions; Recognizers.Text's relative ordinals (`next`, `last`).
