using System.Globalization;
using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Repeated numerals ("sixty (60) days"), business days and duration counts, and the smaller number-rule fixes.</summary>
public class NumeralAndDurationTests
{
    private static readonly DateTimeOffset Wed = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);

    private static string Describe(Recognition r)
    {
        string unit = string.Empty;
        switch (r)
        {
            case Quantity q:
                unit = "|" + q.Value.ToString("0.###############", CultureInfo.InvariantCulture) + (q.Unit is null ? string.Empty : "|" + q.Unit)
                    + (q.Currency is null ? string.Empty : "|" + q.Currency);
                break;
            case TemporalMatch t:
                unit = "|" + t.Value + (t.Count is decimal c ? $"|{c.ToString(CultureInfo.InvariantCulture)} {t.Unit}" : string.Empty);
                break;
        }

        return $"{r.Text}|{r.Label}{unit}";
    }

    private static string Run(string text) =>
        string.Join(" ; ", Recognizer.English.Recognize(text, new RecognitionContext(Wed)).Select(Describe));

    [Theory]
    [InlineData("within sixty (60) days of receipt", "sixty (60) days|DURATION|5184000|60 Day")]
    [InlineData("ten (10) business days", "ten (10) business days|DURATION|P10BD|10 BusinessDay")]
    [InlineData("One hundred and twenty (120) days", "One hundred and twenty (120) days|DURATION|10368000|120 Day")]
    [InlineData("five thousand dollars ($5,000)", "five thousand dollars ($5,000)|MONEY|5000|USD")]
    [InlineData("five thousand dollars (5,000)", "five thousand dollars (5,000)|MONEY|5000|USD")]
    [InlineData("one hundred dollars (USD 100)", "one hundred dollars (USD 100)|MONEY|100|USD")]
    [InlineData("fifty percent (50%)", "fifty percent (50%)|PERCENT|50|percent")]
    [InlineData("two (2) km", "two (2) km|QUANTITY|2|kilometer")]
    [InlineData("twenty-one (21) widgets", "twenty-one (21)|CARDINAL|21")]
    [InlineData("sixty (60)", "sixty (60)|CARDINAL|60")]
    public void RepeatedNumeralMerges(string text, string expected) => Assert.Equal(expected, Run(text));

    [Fact]
    public void DisagreeingNumeralKeepsTheWordsAtLowerConfidence()
    {
        IReadOnlyList<Recognition> r = Recognizer.English.Recognize("The term is sixty (90) days.", new RecognitionContext(Wed));
        Quantity words = Assert.IsType<Quantity>(r.First(x => x.Text == "sixty"));
        Assert.Equal(60m, words.Value);
        Assert.True(words.Confidence < 1f);
        Assert.Contains(r, x => x.Text == "90");
        Assert.DoesNotContain(r, x => x.Text.Contains('(', StringComparison.Ordinal));

        IReadOnlyList<Recognition> money = Recognizer.English.Recognize("five thousand dollars ($4,000)", new RecognitionContext(Wed));
        Quantity paid = Assert.IsType<Quantity>(money.First(x => x.Text == "five thousand dollars"));
        Assert.Equal(5000m, paid.Value);
        Assert.True(paid.Confidence < 0.9f);
    }

    [Theory]
    [InlineData("ten business days", "ten business days|DURATION|P10BD|10 BusinessDay")]
    [InlineData("5 working days", "5 working days|DURATION|P5BD|5 BusinessDay")]
    [InlineData("a business day", "a business day|DURATION|P1BD|1 BusinessDay")]
    [InlineData("48 business hours", "48 business hours|DURATION|PT48BH|48 BusinessHour")]
    [InlineData("14 calendar days", "14 calendar days|DURATION|1209600|14 Day")]
    [InlineData("2 calendar months", "2 calendar months|DURATION|P2M|2 Month")]
    [InlineData("60 days", "60 days|DURATION|5184000|60 Day")]
    [InlineData("18 months", "18 months|DURATION|P18M|18 Month")]
    [InlineData("3 quarters", "3 quarters|DURATION|P9M|9 Month")]
    [InlineData("two decades", "two decades|DURATION|P20Y|20 Year")]
    [InlineData("a fortnight", "a fortnight|DURATION|1209600|2 Week")]
    [InlineData("1.5 hours", "1.5 hours|DURATION|5400|1.5 Hour")]
    [InlineData("2 hours and 30 minutes", "2 hours and 30 minutes|DURATION|9000|150 Minute")]
    [InlineData("1 year and 6 months", "1 year and 6 months|DURATION|P1Y6M|18 Month")]
    [InlineData("an hour and a half", "an hour and a half|DURATION|5400|1.5 Hour")]
    [InlineData("1 year and 3 days", "1 year and 3 days|DURATION|P1Y3D")]
    public void DurationsCarryCountAndUnit(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("in 3 business days", "in 3 business days|DATE|2026-03-23")]
    [InlineData("in 5 working days", "in 5 working days|DATE|2026-03-25")]
    [InlineData("5 business days ago", "5 business days ago|DATE|2026-03-11")]
    [InlineData("in 0 business days", "0|CARDINAL|0")]
    public void BusinessDaysSkipWeekends(string text, string expected) => Assert.Equal(expected, Run(text));

    [Fact]
    public void BusinessDayArithmeticFromWeekends()
    {
        var saturday = new DateTime(2026, 3, 21);
        var sunday = new DateTime(2026, 3, 22);
        Assert.Equal(new DateTime(2026, 3, 23), CivilCalendar.AddBusinessDays(saturday, 1));
        Assert.Equal(new DateTime(2026, 3, 23), CivilCalendar.AddBusinessDays(sunday, 1));
        Assert.Equal(new DateTime(2026, 3, 27), CivilCalendar.AddBusinessDays(saturday, 5));
        Assert.Equal(new DateTime(2026, 3, 20), CivilCalendar.AddBusinessDays(saturday, -1));
        Assert.Equal(new DateTime(2026, 3, 20), CivilCalendar.AddBusinessDays(sunday, -1));
        Assert.Equal(new DateTime(2026, 3, 13), CivilCalendar.AddBusinessDays(new DateTime(2026, 3, 20), -5));
        Assert.Equal(new DateTime(2026, 4, 1), CivilCalendar.AddBusinessDays(new DateTime(2026, 3, 18), 10));
    }

    [Fact]
    public void CountAndUnitStayEmptyOutsideDurations()
    {
        TemporalMatch t = Assert.IsType<TemporalMatch>(Recognizer.English.Recognize("tomorrow", new RecognitionContext(Wed)).Single());
        Assert.Null(t.Unit);
        Assert.Null(t.Count);
    }

    [Theory]
    [InlineData("Wait a second, please.", "a second|DURATION|1|1 Second")]
    [InlineData("Give me a second to think.", "a second|DURATION|1|1 Second")]
    [InlineData("Just a second.", "a second|DURATION|1|1 Second")]
    [InlineData("A second opinion helps.", "second|ORDINAL|2")]
    [InlineData("This is the second floor.", "second|ORDINAL|2")]
    [InlineData("She finished second.", "second|ORDINAL|2")]
    [InlineData("It took one second.", "one second|DURATION|1|1 Second")]
    public void SecondIsAUnitByContext(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("No one answered.", "")]
    [InlineData("One of them left.", "")]
    [InlineData("One another's help.", "")]
    [InlineData("Which one is it?", "")]
    [InlineData("This is the one.", "")]
    [InlineData("One's own view.", "")]
    [InlineData("I bought one apple.", "one|CARDINAL|1")]
    [InlineData("I have one.", "one|CARDINAL|1")]
    [InlineData("one hundred and one", "one hundred and one|CARDINAL|101")]
    [InlineData("For the first time in years.", "")]
    [InlineData("He won for the second time.", "")]
    [InlineData("He came first.", "first|ORDINAL|1")]
    [InlineData("The first floor.", "first|ORDINAL|1")]
    public void LoneWordsThatAreNotNumbers(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("5 feet 3 inches", "5 feet 3 inches|QUANTITY|63|inch")]
    [InlineData("5 feet and 3 inches", "5 feet and 3 inches|QUANTITY|63|inch")]
    [InlineData("5 feet, 3 inches", "5 feet, 3 inches|QUANTITY|63|inch")]
    [InlineData("6 pounds, 4 ounces", "6 pounds, 4 ounces|QUANTITY|100|ounce")]
    [InlineData("3 miles 200 yards", "3 miles 200 yards|QUANTITY|5480|yard")]
    [InlineData("1 meter 20 centimeters", "1 meter 20 centimeters|QUANTITY|120|centimeter")]
    [InlineData("5 feet 15 inches", "5 feet|QUANTITY|5|foot ; 15 inches|QUANTITY|15|inch")]
    [InlineData("5 feet 3 miles", "5 feet|QUANTITY|5|foot ; 3 miles|QUANTITY|3|mile")]
    public void CompoundMeasures(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("1,00,000 people", "1,00,000|CARDINAL|100000")]
    [InlineData("12,34,567", "12,34,567|CARDINAL|1234567")]
    [InlineData("1,23,45,678", "1,23,45,678|CARDINAL|12345678")]
    [InlineData("1,23,45", "1|CARDINAL|1 ; 23|CARDINAL|23 ; 45|CARDINAL|45")]
    [InlineData("12,345", "12,345|CARDINAL|12345")]
    [InlineData("6.02e23", "6.02e23|CARDINAL|602000000000000000000000")]
    [InlineData("2E-3", "2E-3|CARDINAL|0.002")]
    [InlineData("1.5\u00D710^3", "1.5\u00D710^3|CARDINAL|1500")]
    [InlineData("6.02 \u00D7 10^23", "6.02 \u00D7 10^23|CARDINAL|602000000000000000000000")]
    [InlineData("3 x 10^8 m/s", "3 x 10^8 m/s|QUANTITY|300000000|meter per second")]
    [InlineData("2.5\u00D710\u00B3", "2.5\u00D710\u00B3|CARDINAL|2500")]
    [InlineData("3 x 10 apples", "3|CARDINAL|3 ; 10|CARDINAL|10")]
    [InlineData("7e5x", "")]
    [InlineData("1e999", "")]
    public void IndianGroupingAndScientificNotation(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("the 1,000th visitor", "1,000th|ORDINAL|1000")]
    [InlineData("He was 21st in line.", "21st|ORDINAL|21")]
    [InlineData("1,000 pounds for parking", "1,000 pounds|MONEY|1000|GBP")]
    [InlineData("The fine was 1,000 pounds.", "1,000 pounds|MONEY|1000|GBP")]
    [InlineData("The turkey weighs 10 pounds.", "10 pounds|QUANTITY|10|pound")]
    [InlineData("He lost 10 pounds last month.", "10 pounds|QUANTITY|10|pound ; last month|DATE|2026-02-01/2026-03-01")]
    [InlineData("10 pounds of flour", "10 pounds|QUANTITY|10|pound")]
    [InlineData("10 pounds", "10 pounds|QUANTITY|10|pound")]
    [InlineData("For 1,000 pounds he bought 3 pounds of tea.", "1,000 pounds|MONEY|1000|GBP ; 3 pounds|QUANTITY|3|pound")]
    public void OrdinalsAndAmbiguousCurrencyNames(string text, string expected) => Assert.Equal(expected, Run(text));

    [Fact]
    public void CurrencyCodesFollowTheStandardList()
    {
        // Codes added when the list was checked against the maintenance agency's List One of 2026-09-17.
        Assert.Equal("5 XCG|MONEY|5|XCG", Run("5 XCG"));
        Assert.Equal("XAD 7|MONEY|7|XAD", Run("XAD 7"));
        Assert.Equal("5 EUR|MONEY|5|EUR", Run("5 EUR"));
        Assert.Equal("5|CARDINAL|5", Run("5 XXX"));
    }
}
