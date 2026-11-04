OneNLP recognizers (English) against Microsoft.Recognizers.Text 1.8.13 (English) on recognizer-gold-en test (paired bootstrap over sentences, 1000 resamples, seed 20260101). Verdict: the two 95% intervals overlap = within noise, else ahead or behind.

| Type | Gold | F1 ours | F1 reference | F1 difference | F1 verdict | Value acc. ours | Value acc. reference | Value difference | Value verdict |
|---|---:|---|---|---|---|---|---|---|---|
| number | 19 | 95.0 [86.5, 100.0] | 95.0 [86.5, 100.0] | 0.0 [0.0, 0.0] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| ordinal | 11 | 88.0 [70.0, 100.0] | 88.0 [70.0, 100.0] | 0.0 [0.0, 0.0] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| percentage | 12 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| dimension | 20 | 94.7 [85.7, 100.0] | 67.9 [50.0, 80.0] | +26.8 [+15.6, +42.2] | ahead | 100.0 [100.0, 100.0] | 94.4 [81.3, 100.0] | +5.6 [0.0, +18.8] | within noise |
| temperature | 11 | 90.0 [72.7, 100.0] | 100.0 [100.0, 100.0] | -10.0 [-27.3, 0.0] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| age | 11 | 95.2 [83.3, 100.0] | 100.0 [100.0, 100.0] | -4.8 [-16.7, 0.0] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| currency | 13 | 100.0 [100.0, 100.0] | 92.3 [76.9, 100.0] | +7.7 [0.0, +23.1] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| email | 11 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| url | 11 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| ip | 11 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| phone | 10 | 100.0 [100.0, 100.0] | 90.0 [66.7, 100.0] | +10.0 [0.0, +33.3] | within noise | n/a | n/a | n/a | n/a |
| hashtag | 11 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| mention | 12 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| guid | 10 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| date | 35 | 0.0 [0.0, 0.0] | 97.2 [93.0, 100.0] | -97.2 [-100.0, -93.0] | behind | n/a | n/a | n/a | n/a |
| time | 15 | 0.0 [0.0, 0.0] | 51.6 [25.0, 76.9] | -51.6 [-76.9, -25.0] | behind | n/a | n/a | n/a | n/a |
| datetime | 14 | 0.0 [0.0, 0.0] | 64.3 [35.7, 90.9] | -64.3 [-90.9, -35.7] | behind | n/a | n/a | n/a | n/a |
| daterange | 26 | 0.0 [0.0, 0.0] | 86.8 [73.1, 96.7] | -86.8 [-96.7, -73.1] | behind | n/a | n/a | n/a | n/a |
| timerange | 11 | 0.0 [0.0, 0.0] | 100.0 [100.0, 100.0] | -100.0 [-100.0, -100.0] | behind | n/a | n/a | n/a | n/a |
| datetimerange | 11 | 0.0 [0.0, 0.0] | 100.0 [100.0, 100.0] | -100.0 [-100.0, -100.0] | behind | n/a | n/a | n/a | n/a |
| duration | 14 | 0.0 [0.0, 0.0] | 100.0 [100.0, 100.0] | -100.0 [-100.0, -100.0] | behind | n/a | n/a | n/a | n/a |
| set | 13 | 0.0 [0.0, 0.0] | 92.3 [75.0, 100.0] | -92.3 [-100.0, -75.0] | behind | n/a | n/a | n/a | n/a |
| temporal (any subtype) | 112 | 0.0 [0.0, 0.0] | 85.1 [78.2, 90.6] | -85.1 [-90.6, -78.2] | behind | n/a | n/a | n/a | n/a |
| all but date/time, duration, set | 173 | 95.7 [92.8, 98.2] | 63.7 [59.3, 68.2] | +32.1 [+27.2, +36.7] | ahead | 100.0 [100.0, 100.0] | 98.9 [96.7, 100.0] | +1.1 [0.0, +3.3] | within noise |
| all types | 312 | 53.9 [47.3, 59.6] | 59.9 [56.2, 63.1] | -6.0 [-11.8, -0.8] | within noise | 100.0 [100.0, 100.0] | 95.9 [93.1, 98.5] | +4.1 [+1.5, +6.9] | ahead |
