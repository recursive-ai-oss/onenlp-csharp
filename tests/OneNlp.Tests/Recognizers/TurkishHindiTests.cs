using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Turkish and Hindi: compound number words, Indian scale words, parts of periods named after the period, clock forms and the pinned holiday tables (reference date Wednesday 18 March 2026).</summary>
public class TurkishHindiTests
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
    [InlineData("Yüz yirmi üç bin dört yüz elli", "Yüz yirmi üç bin dört yüz elli|Cardinal|123450")]
    [InlineData("Saat 14.30'da başlıyor", "Saat 14.30|Time|T14:30")]
    [InlineData("Kurban Bayramı 2026'da", "Kurban Bayramı 2026|Date|2026-05-27")]
    [InlineData("2021 başında", "2021 başında|DateRange|2021-01-01/2021-05-01")]
    [InlineData("2021 baharında", "2021 baharında|DateRange|2021-03-01/2021-06-01")]
    [InlineData("Pazar yerinde domates aldık", "")]
    public void Turkish(string text, string expected) => Assert.Equal(expected, Run(Language.Turkish, text));

    [Theory]
    [InlineData("1.5 करोड़ रुपये", "1.5 करोड़ रुपये|Money|15000000")]
    [InlineData("साढ़े तीन बजे", "साढ़े तीन बजे|Time|T03:30")]
    [InlineData("गणतंत्र दिवस 2027", "गणतंत्र दिवस 2027|Date|2027-01-26")]
    [InlineData("2022 के मध्य", "2022 के मध्य|DateRange|2022-05-01/2022-09-01")]
    [InlineData("12,34,567", "12,34,567|Cardinal|1234567")]
    public void Hindi(string text, string expected) => Assert.Equal(expected, Run(Language.Hindi, text));
}
