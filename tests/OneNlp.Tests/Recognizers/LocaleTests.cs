using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>French and German: separators, number words, ordinals and the date and time grammar (reference date Wednesday 18 March 2026).</summary>
public class LocaleTests
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
    [InlineData("Il a payé 1 234,56 €.", "1 234,56 €|Money|1234.56")]
    [InlineData("Il a payé 1 234,56 €.", "1 234,56 €|Money|1234.56")]
    [InlineData("Prix : 250.000 EUR", "250.000 EUR|Money|250000")]
    [InlineData("quatre-vingt-dix-sept", "quatre-vingt-dix-sept|Cardinal|97")]
    [InlineData("soixante et onze", "soixante et onze|Cardinal|71")]
    [InlineData("septante-deux", "septante-deux|Cardinal|72")]
    [InlineData("quatre-vingts", "quatre-vingts|Cardinal|80")]
    [InlineData("mille deux cents", "mille deux cents|Cardinal|1200")]
    [InlineData("deux cent trente-quatre", "deux cent trente-quatre|Cardinal|234")]
    [InlineData("un million deux cent mille", "un million deux cent mille|Cardinal|1200000")]
    [InlineData("vingt et unième", "vingt et unième|Ordinal|21")]
    [InlineData("le 1er rang", "1er|Ordinal|1")]
    [InlineData("3,5 %", "3,5 %|Percent|3.5")]
    [InlineData("vingt pour cent", "vingt pour cent|Percent|20")]
    [InlineData("âgé de 82 ans", "âgé de 82 ans|Measure|82")]
    [InlineData("3,2 millions d'euros", "3,2 millions d'euros|Money|3200000")]
    [InlineData("3,2 millions d’euros", "3,2 millions d’euros|Money|3200000")]
    public void FrenchNumbers(string text, string expected) => Assert.Equal(expected, Run(Language.French, text));

    [Theory]
    [InlineData("le 3 mars 2026", "le 3 mars 2026|Date|2026-03-03")]
    [InlineData("le 1er septembre", "le 1er septembre|Date|2026-09-01")]
    [InlineData("demain", "demain|Date|2026-03-19")]
    [InlineData("après-demain", "après-demain|Date|2026-03-20")]
    [InlineData("il y a 10 jours", "il y a 10 jours|Date|2026-03-08")]
    [InlineData("dans trois semaines", "dans trois semaines|Date|2026-04-08")]
    [InlineData("mardi dernier", "mardi dernier|Date|2026-03-17")]
    [InlineData("la semaine prochaine", "la semaine prochaine|DateRange|2026-03-23/2026-03-30")]
    [InlineData("l'été prochain", "l'été prochain|DateRange|2026-06-01/2026-09-01")]
    [InlineData("le 30/06/2026", "le 30/06/2026|Date|2026-06-30")]
    [InlineData("15h30", "15h30|Time|T15:30")]
    [InlineData("huit heures du soir", "huit heures du soir|Time|T20:00")]
    [InlineData("il est six heures moins le quart", "six heures moins le quart|Time|T05:45")]
    [InlineData("de 8h à 12h", "de 8h à 12h|TimeRange|T08:00/T12:00")]
    [InlineData("du 3 mars au 7 avril 2026", "du 3 mars au 7 avril 2026|DateRange|2026-03-03/2026-04-08")]
    [InlineData("tous les samedis", "tous les samedis|Set|XXXX-WXX-6")]
    [InlineData("le lundi de Pâques 2027", "le lundi de Pâques 2027|Date|2027-03-29")]
    [InlineData("la fête des mères 2026", "la fête des mères 2026|Date|2026-05-31")]
    [InlineData("les années 1990", "les années 1990|DateRange|1990-01-01/2000-01-01")]
    public void FrenchDates(string text, string expected) => Assert.Equal(expected, Run(Language.French, text));

    [Theory]
    [InlineData("1.234,56 €", "1.234,56 €|Money|1234.56")]
    [InlineData("3,5 %", "3,5 %|Percent|3.5")]
    [InlineData("dreihundertvierundzwanzig", "dreihundertvierundzwanzig|Cardinal|324")]
    [InlineData("zweitausendfünfhundert", "zweitausendfünfhundert|Cardinal|2500")]
    [InlineData("Einundneunzig", "Einundneunzig|Cardinal|91")]
    [InlineData("eine Million zweihunderttausend", "eine Million zweihunderttausend|Cardinal|1200000")]
    [InlineData("die 3. Runde", "3.|Ordinal|3")]
    [InlineData("einundzwanzigste", "einundzwanzigste|Ordinal|21")]
    [InlineData("16 Jahre alt", "16 Jahre alt|Measure|16")]
    [InlineData("ein 4-jähriges Kind", "4-jähriges|Measure|4")]
    public void GermanNumbers(string text, string expected) => Assert.Equal(expected, Run(Language.German, text));

    [Theory]
    [InlineData("am 3. März 2026", "3. März 2026|Date|2026-03-03")]
    [InlineData("30.06.2026", "30.06.2026|Date|2026-06-30")]
    [InlineData("morgen", "morgen|Date|2026-03-19")]
    [InlineData("vor 10 Tagen", "vor 10 Tagen|Date|2026-03-08")]
    [InlineData("in einem Monat", "in einem Monat|Date|2026-04-18")]
    [InlineData("nächsten Samstag", "nächsten Samstag|Date|2026-03-28")]
    [InlineData("Dienstag, den 24. März", "Dienstag, den 24. März|Date|2026-03-24")]
    [InlineData("15 Uhr", "15 Uhr|Time|T15:00")]
    [InlineData("halb drei", "halb drei|Time|T02:30")]
    [InlineData("Viertel nach drei", "Viertel nach drei|Time|T03:15")]
    [InlineData("acht Uhr abends", "acht Uhr abends|Time|T20:00")]
    [InlineData("von 10 bis 12 Uhr", "von 10 bis 12 Uhr|TimeRange|T10:00/T12:00")]
    [InlineData("vom 3. März bis 7. April 2026", "vom 3. März bis 7. April 2026|DateRange|2026-03-03/2026-04-08")]
    [InlineData("jeden Samstag", "jeden Samstag|Set|XXXX-WXX-6")]
    [InlineData("Ostermontag 2027", "Ostermontag 2027|Date|2027-03-29")]
    [InlineData("Muttertag 2026", "Muttertag 2026|Date|2026-05-10")]
    [InlineData("in den 1990er Jahren", "1990er Jahren|DateRange|1990-01-01/2000-01-01")]
    public void GermanDates(string text, string expected) => Assert.Equal(expected, Run(Language.German, text));

    [Fact]
    public void EnglishIsNotAffected() =>
        Assert.Equal("1,234.56|Cardinal|1234.56", string.Join(" ; ", Recognizer.English.Recognize("1,234.56", new RecognitionContext(Wed)).OfType<Quantity>().Select(q => $"{q.Text}|{q.Kind}|{q.Value}")));

    [Fact]
    public void NlpVerbsTakeALanguage() =>
        Assert.Equal(new DateTime(2026, 3, 19), Nlp.ParseDate("demain", Wed, Language.French)?.Start.DateTime);
}
