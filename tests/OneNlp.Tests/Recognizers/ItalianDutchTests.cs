using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Italian and Dutch: separators, compound number words, ordinals, the date and time grammar and the national holidays (reference date Wednesday 18 March 2026).</summary>
public class ItalianDutchTests
{
    private static readonly DateTimeOffset Wed = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);

    private static string Run(Language language, string text) =>
        string.Join(" ; ", Recognizer.For(language).Recognize(text, new RecognitionContext(Wed)).Select(r => r switch
        {
            Quantity q => $"{q.Text}|{q.Kind}|{q.Value}",
            TemporalMatch t => $"{t.Text}|{t.Kind}|{t.Value}",
            _ => r.Text,
        }));

    [Theory]
    [InlineData("1.234,56 €", "1.234,56 €|Money|1234.56")]
    [InlineData("trecentoventiquattro", "trecentoventiquattro|Cardinal|324")]
    [InlineData("ventitré", "ventitré|Cardinal|23")]
    [InlineData("ventuno", "ventuno|Cardinal|21")]
    [InlineData("centottanta", "centottanta|Cardinal|180")]
    [InlineData("duemilacinquecento", "duemilacinquecento|Cardinal|2500")]
    [InlineData("tre milioni", "tre milioni|Cardinal|3000000")]
    [InlineData("ventunesimo", "ventunesimo|Ordinal|21")]
    [InlineData("la 3ª edizione", "3ª|Ordinal|3")]
    [InlineData("il 12 per cento", "12 per cento|Percent|12")]
    public void ItalianNumbers(string text, string expected) => Assert.Equal(expected, Run(Language.Italian, text));

    [Theory]
    [InlineData("il 3 marzo 2026", "il 3 marzo 2026|Date|2026-03-03")]
    [InlineData("tra 3 giorni", "tra 3 giorni|Date|2026-03-21")]
    [InlineData("alle 15:30", "15:30|Time|T15:30")]
    [InlineData("dalle 9 alle 17", "dalle 9 alle 17|TimeRange|T09:00/T17:00")]
    [InlineData("alle sei del mattino", "sei del mattino|Time|T06:00")]
    [InlineData("Pasquetta 2027", "Pasquetta 2027|Date|2027-03-29")]
    [InlineData("Ferragosto", "Ferragosto|Date|2026-08-15")]
    [InlineData("gli anni Novanta", "gli anni Novanta|DateRange|1990-01-01/2000-01-01")]
    public void ItalianDates(string text, string expected) => Assert.Equal(expected, Run(Language.Italian, text));

    [Theory]
    [InlineData("€ 1.234,56", "€ 1.234,56|Money|1234.56")]
    [InlineData("driehonderdvierentwintig", "driehonderdvierentwintig|Cardinal|324")]
    [InlineData("tweeëntwintig", "tweeëntwintig|Cardinal|22")]
    [InlineData("tweeduizend vijfhonderd", "tweeduizend vijfhonderd|Cardinal|2500")]
    [InlineData("twaalfhonderd", "twaalfhonderd|Cardinal|1200")]
    [InlineData("drieëntwintigste", "drieëntwintigste|Ordinal|23")]
    [InlineData("8ste", "8ste|Ordinal|8")]
    public void DutchNumbers(string text, string expected) => Assert.Equal(expected, Run(Language.Dutch, text));

    [Theory]
    [InlineData("20 maart 2026", "20 maart 2026|Date|2026-03-20")]
    [InlineData("over 3 dagen", "over 3 dagen|Date|2026-03-21")]
    [InlineData("zaterdagavond", "zaterdagavond|DateTimeRange|2026-03-21T18:00/2026-03-22T00:00")]
    [InlineData("half vier", "half vier|Time|T03:30")]
    [InlineData("kwart over drie", "kwart over drie|Time|T03:15")]
    [InlineData("Koningsdag", "Koningsdag|Date|2026-04-27")]
    [InlineData("Koningsdag 2031", "Koningsdag 2031|Date|2031-04-26")]
    [InlineData("de jaren negentig", "de jaren negentig|DateRange|1990-01-01/2000-01-01")]
    public void DutchDates(string text, string expected) => Assert.Equal(expected, Run(Language.Dutch, text));
}
