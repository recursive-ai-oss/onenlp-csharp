OneNLP recognizers (English) against Microsoft.Recognizers.Text 1.8.13 (English) on recognizer-gold-en gold (paired bootstrap over sentences, 1000 resamples, seed 20260101). Verdict: the two 95% intervals overlap = within noise, else ahead or behind.

| Type | Gold | F1 ours | F1 reference | F1 difference | F1 verdict | Value acc. ours | Value acc. reference | Value difference | Value verdict |
|---|---:|---|---|---|---|---|---|---|---|
| number | 15 | 90.9 [76.9, 100.0] | 90.9 [76.9, 100.0] | 0.0 [0.0, 0.0] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| ordinal | 8 | 87.5 [61.5, 100.0] | 94.1 [75.0, 100.0] | -6.6 [-24.2, 0.0] | within noise | 100.0 [100.0, 100.0] | 87.5 [60.0, 100.0] | +12.5 [0.0, +40.0] | within noise |
| percentage | 9 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| dimension | 16 | 93.8 [83.9, 100.0] | 75.0 [58.1, 88.5] | +18.8 [+7.4, +33.3] | within noise | 100.0 [100.0, 100.0] | 93.3 [78.6, 100.0] | +6.7 [0.0, +21.4] | within noise |
| temperature | 8 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| age | 7 | 92.3 [66.7, 100.0] | 100.0 [100.0, 100.0] | -7.7 [-33.3, 0.0] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| currency | 13 | 96.0 [85.7, 100.0] | 92.9 [80.0, 100.0] | +3.1 [-10.5, +17.2] | within noise | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise |
| email | 5 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| url | 7 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| ip | 7 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| phone | 7 | 100.0 [100.0, 100.0] | 93.3 [66.7, 100.0] | +6.7 [0.0, +33.3] | within noise | n/a | n/a | n/a | n/a |
| hashtag | 6 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| mention | 3 | 100.0 [0.0, 100.0] | 100.0 [0.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| guid | 5 | 100.0 [100.0, 100.0] | 100.0 [100.0, 100.0] | 0.0 [0.0, 0.0] | within noise | n/a | n/a | n/a | n/a |
| date | 35 | 0.0 [0.0, 0.0] | 94.3 [86.2, 100.0] | -94.3 [-100.0, -86.2] | behind | n/a | n/a | n/a | n/a |
| time | 13 | 0.0 [0.0, 0.0] | 51.9 [24.0, 80.0] | -51.9 [-80.0, -24.0] | behind | n/a | n/a | n/a | n/a |
| datetime | 12 | 0.0 [0.0, 0.0] | 88.0 [66.7, 100.0] | -88.0 [-100.0, -66.7] | behind | n/a | n/a | n/a | n/a |
| daterange | 24 | 0.0 [0.0, 0.0] | 88.5 [77.8, 96.3] | -88.5 [-96.3, -77.8] | behind | n/a | n/a | n/a | n/a |
| timerange | 7 | 0.0 [0.0, 0.0] | 82.4 [54.5, 100.0] | -82.4 [-100.0, -54.5] | behind | n/a | n/a | n/a | n/a |
| datetimerange | 8 | 0.0 [0.0, 0.0] | 100.0 [100.0, 100.0] | -100.0 [-100.0, -100.0] | behind | n/a | n/a | n/a | n/a |
| duration | 12 | 0.0 [0.0, 0.0] | 96.0 [84.6, 100.0] | -96.0 [-100.0, -84.6] | behind | n/a | n/a | n/a | n/a |
| set | 10 | 0.0 [0.0, 0.0] | 100.0 [100.0, 100.0] | -100.0 [-100.0, -100.0] | behind | n/a | n/a | n/a | n/a |
| temporal (any subtype) | 99 | 0.0 [0.0, 0.0] | 85.6 [79.0, 91.6] | -85.6 [-91.6, -79.0] | behind | n/a | n/a | n/a | n/a |
| all but date/time, duration, set | 116 | 95.3 [91.7, 98.2] | 64.1 [59.4, 69.2] | +31.3 [+25.5, +36.6] | ahead | 100.0 [100.0, 100.0] | 97.3 [93.2, 100.0] | +2.7 [0.0, +6.8] | within noise |
| all types | 237 | 49.3 [42.5, 56.6] | 62.0 [58.7, 65.9] | -12.7 [-20.1, -5.6] | behind | 100.0 [100.0, 100.0] | 93.5 [89.8, 96.8] | +6.5 [+3.2, +10.2] | ahead |
