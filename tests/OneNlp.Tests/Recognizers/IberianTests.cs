using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Spanish and Portuguese: number words, separators by region, ordinals, and the date and time grammar (reference date Wednesday 18 March 2026).</summary>
public class IberianTests
{
    private static readonly DateTimeOffset Wed = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);

    private static string Run(Language language, string text, string? region = null) =>
        string.Join(" ; ", Recognizer.For(language).Recognize(text, new RecognitionContext(Wed) { Region = region }).Select(r => r switch
        {
            Quantity q => $"{q.Text}|{q.Kind}|{q.Value}",
            TemporalMatch t => $"{t.Text}|{t.Kind}|{t.Value}",
            _ => r.Text,
        }));

    [Theory]
    [InlineData("Cuesta 1.234,56 €.", "1.234,56 €|Money|1234.56")]
    [InlineData("treinta y cinco", "treinta y cinco|Cardinal|35")]
    [InlineData("ciento veintiuno", "ciento veintiuno|Cardinal|121")]
    [InlineData("doscientos mil", "doscientos mil|Cardinal|200000")]
    [InlineData("vigésimo tercero", "vigésimo tercero|Ordinal|23")]
    [InlineData("la 3.ª planta", "3.ª|Ordinal|3")]
    [InlineData("el 1.er premio", "1.er|Ordinal|1")]
    [InlineData("treinta por ciento", "treinta por ciento|Percent|30")]
    [InlineData("82 años de edad", "82 años de edad|Measure|82")]
    [InlineData("Llame al 913 45 67 89.", "913 45 67 89")]
    public void Spanish(string text, string expected) => Assert.Equal(expected, Run(Language.Spanish, text));

    [Fact]
    public void MexicanSpanishUsesPointAndComma() =>
        Assert.Equal("1,234.56 pesos|Money|1234.56", Run(Language.Spanish, "Pagó 1,234.56 pesos.", "mx"));

    [Theory]
    [InlineData("el 3 de marzo de 2026", "el 3 de marzo de 2026|Date|2026-03-03")]
    [InlineData("del 12 al 20 de agosto", "del 12 al 20 de agosto|DateRange|2026-08-12/2026-08-21")]
    [InlineData("hace 10 días", "hace 10 días|Date|2026-03-08")]
    [InlineData("los años 80", "los años 80|DateRange|1980-01-01/1990-01-01")]
    [InlineData("a las 20:30", "las 20:30|Time|T20:30")]
    [InlineData("a las ocho de la noche", "las ocho de la noche|Time|T20:00")]
    [InlineData("Navidad de 2026", "Navidad de 2026|Date|2026-12-25")]
    [InlineData("Pascua de 2027", "Pascua de 2027|Date|2027-03-28")]
    [InlineData("todos los sábados", "todos los sábados|Set|XXXX-WXX-6")]
    public void SpanishDates(string text, string expected) => Assert.Equal(expected, Run(Language.Spanish, text));

    [Theory]
    [InlineData("R$ 1.234,56", "R$ 1.234,56|Money|1234.56")]
    [InlineData("cento e vinte", "cento e vinte|Cardinal|120")]
    [InlineData("dois mil e quinhentos", "dois mil e quinhentos|Cardinal|2500")]
    [InlineData("trinta e cinco", "trinta e cinco|Cardinal|35")]
    [InlineData("a 3.ª edição", "3.ª|Ordinal|3")]
    [InlineData("82 anos de idade", "82 anos de idade|Measure|82")]
    public void Portuguese(string text, string expected) => Assert.Equal(expected, Run(Language.Portuguese, text));

    [Theory]
    [InlineData("3 de março de 2026", "3 de março de 2026|Date|2026-03-03")]
    [InlineData("há 10 dias", "há 10 dias|Date|2026-03-08")]
    [InlineData("às 14:30", "às 14:30|Time|T14:30")]
    [InlineData("às oito da noite", "às oito da noite|Time|T20:00")]
    [InlineData("Corpus Christi de 2026", "Corpus Christi de 2026|Date|2026-06-04")]
    [InlineData("Carnaval de 2027", "Carnaval de 2027|Date|2027-02-08")]
    public void PortugueseDates(string text, string expected) => Assert.Equal(expected, Run(Language.Portuguese, text));
}
