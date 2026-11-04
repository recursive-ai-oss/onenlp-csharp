using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Tests;

public class TemporalTests
{
    private static readonly DateTimeOffset Wed = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);

    private static string Run(string text, DateTimeOffset? reference = null, string? region = null) =>
        string.Join(" ; ", Recognizer.English.Recognize(text, new RecognitionContext(reference ?? Wed) { Region = region })
            .OfType<TemporalMatch>().Select(t => $"{t.Text}|{t.Kind}|{t.Value}"));

    [Theory]
    [InlineData("tomorrow", "tomorrow|Date|2026-03-19")]
    [InlineData("a week ago", "a week ago|Date|2026-03-11")]
    [InlineData("in 3 months", "in 3 months|Date|2026-06-18")]
    [InlineData("Wednesday", "Wednesday|Date|2026-03-18")]
    [InlineData("next Wednesday", "next Wednesday|Date|2026-03-25")]
    [InlineData("last Wednesday", "last Wednesday|Date|2026-03-11")]
    [InlineData("Wednesday of next week", "Wednesday of next week|Date|2026-03-25")]
    [InlineData("March 5, 2027", "March 5, 2027|Date|2027-03-05")]
    [InlineData("5 Mar 2027", "5 Mar 2027|Date|2027-03-05")]
    [InlineData("Thursday, 5 March 2026", "Thursday, 5 March 2026|Date|2026-03-05")]
    [InlineData("2026-04-15", "2026-04-15|Date|2026-04-15")]
    [InlineData("04/15/2026", "04/15/2026|Date|2026-04-15")]
    [InlineData("15/04/2026", "15/04/2026|Date|2026-04-15")]
    [InlineData("2026-02-30", "")]
    [InlineData("February 29, 2027", "2027|DateRange|2027-01-01/2028-01-01")]
    [InlineData("February 29, 2028", "February 29, 2028|Date|2028-02-29")]
    [InlineData("Easter 2026", "Easter 2026|Date|2026-04-05")]
    [InlineData("Thanksgiving 2026", "Thanksgiving 2026|Date|2026-11-26")]
    [InlineData("Memorial Day 2027", "Memorial Day 2027|Date|2027-05-31")]
    [InlineData("the last Friday of May 2026", "the last Friday of May 2026|Date|2026-05-29")]
    [InlineData("the first Monday after Easter 2027", "the first Monday after Easter 2027|Date|2027-03-29")]
    [InlineData("two days before Christmas", "two days before Christmas|Date|2026-12-23")]
    public void Dates(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("this week", "this week|DateRange|2026-03-16/2026-03-23")]
    [InlineData("last quarter", "last quarter|DateRange|2025-10-01/2026-01-01")]
    [InlineData("next year", "next year|DateRange|2027-01-01/2028-01-01")]
    [InlineData("spring 2021", "spring 2021|DateRange|2021-03-01/2021-06-01")]
    [InlineData("winter 2024", "winter 2024|DateRange|2024-12-01/2025-03-01")]
    [InlineData("the second half of 2026", "the second half of 2026|DateRange|2026-07-01/2027-01-01")]
    [InlineData("Q4 2025", "Q4 2025|DateRange|2025-10-01/2026-01-01")]
    [InlineData("early 2021", "early 2021|DateRange|2021-01-01/2021-05-01")]
    [InlineData("mid-2021", "mid-2021|DateRange|2021-05-01/2021-09-01")]
    [InlineData("late March 2026", "late March 2026|DateRange|2026-03-21/2026-04-01")]
    [InlineData("the 1990s", "the 1990s|DateRange|1990-01-01/2000-01-01")]
    [InlineData("1990-2000", "1990-2000|DateRange|1990-01-01/2001-01-01")]
    [InlineData("May 4-8, 2026", "May 4-8, 2026|DateRange|2026-05-04/2026-05-09")]
    [InlineData("from Monday to Friday", "from Monday to Friday|DateRange|2026-03-23/2026-03-28")]
    [InlineData("the past three days", "the past three days|DateRange|2026-03-15/2026-03-18")]
    [InlineData("this weekend", "this weekend|DateRange|2026-03-21/2026-03-23")]
    public void Ranges(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("3pm", "3pm|Time|T15:00")]
    [InlineData("12am", "12am|Time|T00:00")]
    [InlineData("12:30 pm", "12:30 pm|Time|T12:30")]
    [InlineData("3.30pm", "3.30pm|Time|T15:30")]
    [InlineData("23:59:59", "23:59:59|Time|T23:59:59")]
    [InlineData("9:30 PST", "9:30 PST|Time|T09:30-08:00")]
    [InlineData("noon UTC+2", "noon UTC+2|Time|T12:00+02:00")]
    [InlineData("10 at night", "10 at night|Time|T22:00")]
    [InlineData("24:30", "")]
    [InlineData("9:75", "")]
    [InlineData("between 11 and 1 pm", "between 11 and 1 pm|TimeRange|T11:00/T13:00")]
    [InlineData("tomorrow at 3pm", "tomorrow at 3pm|DateTime|2026-03-19T15:00")]
    [InlineData("3pm on March 22", "3pm on March 22|DateTime|2026-03-22T15:00")]
    [InlineData("2026-06-30T09:15:00Z", "2026-06-30T09:15:00Z|DateTime|2026-06-30T09:15:00Z")]
    [InlineData("2026-06-30T09:15:00-05:00", "2026-06-30T09:15:00-05:00|DateTime|2026-06-30T09:15:00-05:00")]
    [InlineData("tomorrow evening", "tomorrow evening|DateTimeRange|2026-03-19T18:00/2026-03-20T00:00")]
    [InlineData("in an hour", "in an hour|DateTime|2026-03-18T11:30")]
    public void Times(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("two weeks", "two weeks|Duration|1209600")]
    [InlineData("1 year and 6 months", "1 year and 6 months|Duration|P1Y6M")]
    [InlineData("18 months", "18 months|Duration|P18M")]
    [InlineData("2.5 years", "2.5 years|Duration|P2Y6M")]
    [InlineData("a year and a half", "a year and a half|Duration|P1Y6M")]
    [InlineData("an hour and a half", "an hour and a half|Duration|5400")]
    [InlineData("2 and a half hours", "2 and a half hours|Duration|9000")]
    [InlineData("seven days a week", "seven days|Duration|604800 ; a week|Duration|604800")]
    [InlineData("every Monday at 9am", "every Monday at 9am|Set|XXXX-WXX-1T09:00")]
    [InlineData("every 2 weeks", "every 2 weeks|Set|P2W")]
    [InlineData("every day at noon", "every day at noon|Set|T12:00")]
    [InlineData("quarterly", "quarterly|Set|P3M")]
    [InlineData("every second Tuesday", "Tuesday|Date|2026-03-24")]
    public void DurationsAndSets(string text, string expected) => Assert.Equal(expected, Run(text));

    [Fact]
    public void ReferenceDateRules()
    {
        // The year boundary and the leap day move relative expressions, not absolute ones.
        var newYearsEve = new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero);
        Assert.Equal("tomorrow|Date|2027-01-01", Run("tomorrow", newYearsEve));
        Assert.Equal("next month|DateRange|2027-01-01/2027-02-01", Run("next month", newYearsEve));
        var leap = new DateTimeOffset(2028, 2, 28, 9, 0, 0, TimeSpan.Zero);
        Assert.Equal("the day after tomorrow|Date|2028-03-01", Run("the day after tomorrow", leap));

        // Without a reference only expressions that stand alone are reported.
        var none = Recognizer.English.Recognize("Meet March 5, 2027 or tomorrow; allow two weeks, every Monday, 3pm.", default).OfType<TemporalMatch>().Select(t => t.Value);
        Assert.Equal(["2027-03-05", "1209600", "XXXX-WXX-1", "T15:00"], none);
    }

    [Fact]
    public void RegionChangesNumericDatesAndHolidays()
    {
        Assert.Equal("04/05/2026|Date|2026-04-05", Run("04/05/2026"));
        Assert.Equal("04/05/2026|Date|2026-05-04", Run("04/05/2026", region: "gb"));
        Assert.Equal("Mother's Day 2026|Date|2026-05-10", Run("Mother's Day 2026"));
        Assert.Equal("Mother's Day 2026|Date|2026-03-15", Run("Mother's Day 2026", region: "gb"));
        Assert.Equal("Boxing Day 2026|Date|2026-12-26", Run("Boxing Day 2026", region: "gb"));
    }

    [Fact]
    public void ZoneAndOffsetKeptInResult()
    {
        TemporalMatch t = Recognizer.English.Recognize("9am JST", new RecognitionContext(Wed)).OfType<TemporalMatch>().Single();
        Assert.Equal(TimeSpan.FromHours(9), t.UtcOffset);
        Assert.Equal(new DateTimeOffset(2026, 3, 18, 9, 0, 0, TimeSpan.FromHours(9)), t.Range!.Value.Start);
        Assert.Equal(Granularity.Hour, t.Granularity);
    }

    [Fact]
    public void ApproximateExpressionsAreFlagged()
    {
        TemporalMatch t = Recognizer.English.Recognize("spring 2021", new RecognitionContext(Wed)).OfType<TemporalMatch>().Single();
        Assert.True(t.Approximate);
        Assert.Equal(Granularity.Season, t.Range!.Value.Granularity);
        Assert.False(Recognizer.English.Recognize("March 2026", new RecognitionContext(Wed)).OfType<TemporalMatch>().Single().Approximate);
    }

    [Fact]
    public void NlpHappyPath()
    {
        DateRange? when = Nlp.ParseDate("the first Monday after Easter 2027", Wed);
        Assert.Equal(new DateTimeOffset(2027, 3, 29, 0, 0, 0, TimeSpan.Zero), when!.Value.Start);
        Assert.Equal(new DateTimeOffset(2027, 3, 30, 0, 0, 0, TimeSpan.Zero), when.Value.End);
        Assert.Null(Nlp.ParseDate("nothing here", Wed));
        Assert.Equal(2, Nlp.Dates("Call me tomorrow or in two weeks.", Wed).Count());
    }

    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2038, 4, 25)]
    [InlineData(1818, 3, 22)]
    public void Computus(int year, int month, int day) => Assert.Equal(new DateTime(year, month, day), CivilCalendar.Easter(year));

    [Fact]
    public void TemporalOutranksNumbersOfTheSameLength()
    {
        // "2031" alone is a year, not a cardinal.
        Recognition r = Recognizer.English.Recognize("2031", new RecognitionContext(Wed)).Single();
        Assert.Equal("DATE", r.Label);
    }

    [Fact]
    public void BadTemporalRulesAreReportedWithTheirLine()
    {
        RecognizerRulesException e = Assert.Throws<RecognizerRulesException>(() => Recognizer.FromRules([System.Text.Encoding.UTF8.GetBytes("[time.month]\n13 | Smarch\n")]));
        Assert.Contains("Line 2", e.Message);
        Assert.Throws<RecognizerRulesException>(() => Recognizer.FromRules([System.Text.Encoding.UTF8.GetBytes("[time.holiday]\nFoo Day | sometimes | all\n")]));
        Assert.Throws<RecognizerRulesException>(() => Recognizer.FromRules([System.Text.Encoding.UTF8.GetBytes("[time.word]\nnonsense | x\n")]));
    }

    [Fact]
    public void ALoneEqualsSignIsAFormNotAMarker()
    {
        // Found by fuzzing: "=" alone is a one-symbol form, not an empty case-sensitive form.
        Recognizer r = Recognizer.FromRules([System.Text.Encoding.UTF8.GetBytes("[time.month]\n1 | =\n[time.word]\ntoday | =, =x\n")]);
        Assert.Empty(r.Recognize("= x", new RecognitionContext(Wed)).OfType<TemporalMatch>().Where(t => t.Kind == TemporalKind.Duration));
    }
}
