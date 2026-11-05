using System.Globalization;
using System.Text;
using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Tests;

public class RecognizerTests
{
    // One result as "text|LABEL|value[|unit][|currency]" for a quantity, "text|LABEL|normalized" for a sequence.
    private static string Describe(Recognition r) => r switch
    {
        Quantity q => $"{q.Text}|{q.Label}|{q.Value.ToString("0.##########", CultureInfo.InvariantCulture)}"
            + (q.Unit is null ? string.Empty : "|" + q.Unit) + (q.Currency is null ? string.Empty : "|" + q.Currency),
        SequenceMatch s => $"{s.Text}|{s.Label}|{s.Normalized}",
        _ => throw new InvalidOperationException(),
    };

    // The number, unit, currency and sequence rules alone: a date such as "2020-01-15" is read as a date by the full English rules.
    private static readonly Recognizer NumberRules = Recognizer.FromRules(
        [.. typeof(Recognizer).Assembly.GetManifestResourceNames()
            .Where(n => (n.Replace('\\', '/').Contains("Recognizers/Data/en/", StringComparison.Ordinal) || n.Replace('\\', '/').Contains("Recognizers/Data/shared/", StringComparison.Ordinal))
                && !n.EndsWith("datetime.rules", StringComparison.Ordinal) && !n.EndsWith("holidays.rules", StringComparison.Ordinal))
            .Select(n =>
            {
                using Stream stream = typeof(Recognizer).Assembly.GetManifestResourceStream(n)!;
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                return (ReadOnlyMemory<byte>)copy.ToArray();
            })],
        Language.English);

    private static string Run(string text) => string.Join(" ; ", NumberRules.Recognize(text).Select(Describe));

    [Theory]
    [InlineData("I have 42 apples", "42|CARDINAL|42")]
    [InlineData("1,234,567.89", "1,234,567.89|CARDINAL|1234567.89")]
    [InlineData("1,2345", "1|CARDINAL|1 ; 2345|CARDINAL|2345")]
    [InlineData("only .5 left", ".5|CARDINAL|0.5")]
    [InlineData("twenty-one", "twenty-one|CARDINAL|21")]
    [InlineData("twenty one", "twenty one|CARDINAL|21")]
    [InlineData("two hundred and five thousand", "two hundred and five thousand|CARDINAL|205000")]
    [InlineData("one hundred twenty three", "one hundred twenty three|CARDINAL|123")]
    [InlineData("one thousand two hundred thirty-four", "one thousand two hundred thirty-four|CARDINAL|1234")]
    [InlineData("twelve hundred", "twelve hundred|CARDINAL|1200")]
    [InlineData("a hundred", "a hundred|CARDINAL|100")]
    [InlineData("a dozen eggs", "a dozen|CARDINAL|12")]
    [InlineData("two dozen", "two dozen|CARDINAL|24")]
    [InlineData("3.5 million", "3.5 million|CARDINAL|3500000")]
    [InlineData("a million", "a million|CARDINAL|1000000")]
    [InlineData("three point one four", "three point one four|CARDINAL|3.14")]
    [InlineData("one and a half", "one and a half|CARDINAL|1.5")]
    [InlineData("two and three quarters", "two and three quarters|CARDINAL|2.75")]
    [InlineData("three quarters", "three quarters|CARDINAL|0.75")]
    [InlineData("a half", "a half|CARDINAL|0.5")]
    [InlineData("half of it", "")]
    [InlineData("minus five", "minus five|CARDINAL|-5")]
    [InlineData("it was -5 degrees", "-5|CARDINAL|-5")]
    [InlineData("pages 3-5", "3|CARDINAL|3 ; 5|CARDINAL|5")]
    [InlineData("COVID-19 and B-52", "")]
    [InlineData("1/2 a cup", "1/2|CARDINAL|0.5")]
    [InlineData("on 3/4/2020", "3|CARDINAL|3 ; 4|CARDINAL|4 ; 2020|CARDINAL|2020")]
    [InlineData("x86 and H2O", "")]
    [InlineData("version 1.2.3", "")]
    [InlineData("one two", "one|CARDINAL|1 ; two|CARDINAL|2")]
    [InlineData("zero", "zero|CARDINAL|0")]
    [InlineData("5k", "5k|CARDINAL|5000")]
    [InlineData("2.5K views", "2.5K|CARDINAL|2500")]
    [InlineData("ten thousand and ten", "ten thousand and ten|CARDINAL|10010")]
    public void Cardinals(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("the 1st", "1st|ORDINAL|1")]
    [InlineData("22nd", "22nd|ORDINAL|22")]
    [InlineData("13th and 11th and 12th", "13th|ORDINAL|13 ; 11th|ORDINAL|11 ; 12th|ORDINAL|12")]
    [InlineData("21st 103rd", "21st|ORDINAL|21 ; 103rd|ORDINAL|103")]
    [InlineData("21th", "")]
    [InlineData("the first time", "")]
    [InlineData("twenty-third", "twenty-third|ORDINAL|23")]
    [InlineData("one hundred and first", "one hundred and first|ORDINAL|101")]
    [InlineData("the second time", "")]
    [InlineData("one second", "one|CARDINAL|1")]
    [InlineData("twenty second", "twenty second|ORDINAL|22")]
    public void Ordinals(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("5%", "5%|PERCENT|5|percent")]
    [InlineData("up 5 %", "5 %|PERCENT|5|percent")]
    [InlineData("fifty percent", "fifty percent|PERCENT|50|percent")]
    [InlineData("12.5 per cent", "12.5 per cent|PERCENT|12.5|percent")]
    public void Percentages(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("5 km", "5 km|QUANTITY|5|kilometer")]
    [InlineData("5km", "5km|QUANTITY|5|kilometer")]
    [InlineData("3.5 kg", "3.5 kg|QUANTITY|3.5|kilogram")]
    [InlineData("a 5m wall", "5m|QUANTITY|5|meter")]
    [InlineData("20°C", "20°C|QUANTITY|20|celsius")]
    [InlineData("68 degrees Fahrenheit", "68 degrees Fahrenheit|QUANTITY|68|fahrenheit")]
    [InlineData("60 mph", "60 mph|QUANTITY|60|mile per hour")]
    [InlineData("100 km/h", "100 km/h|QUANTITY|100|kilometer per hour")]
    [InlineData("two miles per hour", "two miles per hour|QUANTITY|2|mile per hour")]
    [InlineData("10 GB", "10 GB|QUANTITY|10|gigabyte")]
    [InlineData("10 gb", "10|CARDINAL|10")]
    [InlineData("2 square meters", "2 square meters|QUANTITY|2|square meter")]
    [InlineData("5 feet", "5 feet|QUANTITY|5|foot")]
    [InlineData("5-foot", "5-foot|QUANTITY|5|foot")]
    [InlineData("5 years old", "5 years old|QUANTITY|5|year")]
    [InlineData("a 3-year-old boy", "3-year-old|QUANTITY|3|year")]
    [InlineData("12 oz", "12 oz|QUANTITY|12|ounce")]
    [InlineData("5 kilos of flour", "5 kilos|QUANTITY|5|kilogram")]
    public void Measures(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("$5", "$5|MONEY|5|USD")]
    [InlineData("it cost $5.99.", "$5.99|MONEY|5.99|USD")]
    [InlineData("raised $5m", "$5m|MONEY|5000000|USD")]
    [InlineData("$1.2 billion", "$1.2 billion|MONEY|1200000000|USD")]
    [InlineData("five bucks", "five bucks|MONEY|5|USD")]
    [InlineData("20 euros", "20 euros|MONEY|20|EUR")]
    [InlineData("€20", "€20|MONEY|20|EUR")]
    [InlineData("20€", "20€|MONEY|20|EUR")]
    [InlineData("USD 100", "USD 100|MONEY|100|USD")]
    [InlineData("100 USD", "100 USD|MONEY|100|USD")]
    [InlineData("100 usd", "100|CARDINAL|100")]
    [InlineData("50 cents", "50 cents|MONEY|0.5|USD")]
    [InlineData("-$5", "-$5|MONEY|-5|USD")]
    [InlineData("£3.50", "£3.50|MONEY|3.5|GBP")]
    [InlineData("5 dollars and 50 cents", "5 dollars|MONEY|5|USD ; 50 cents|MONEY|0.5|USD")]
    [InlineData("US$ 7", "US$ 7|MONEY|7|USD")]
    [InlineData("5m dollars", "5m dollars|MONEY|5000000|USD")]
    [InlineData("10 zł", "10 zł|MONEY|10|PLN")]
    public void Money(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("mail John.Doe+x@Example.COM.", "John.Doe+x@Example.COM|EMAIL|John.Doe+x@example.com")]
    [InlineData("(a@b.co)", "a@b.co|EMAIL|a@b.co")]
    [InlineData("a@b.c", "")]
    [InlineData("a..b@c.com", "")]
    [InlineData("see https://example.com/a_(b)?q=1#x, ok", "https://example.com/a_(b)?q=1#x|URL|https://example.com/a_(b)?q=1#x")]
    [InlineData("(https://example.com/path).", "https://example.com/path|URL|https://example.com/path")]
    [InlineData("http://user:pw@host:8080/p", "http://user:pw@host:8080/p|URL|http://user:pw@host:8080/p")]
    [InlineData("http://[2001:db8::1]:80/x", "http://[2001:db8::1]:80/x|URL|http://[2001:db8::1]:80/x")]
    [InlineData("go to www.example.com now", "www.example.com|URL|www.example.com")]
    [InlineData("example.com/page.", "example.com/page|URL|example.com/page")]
    [InlineData("visit example.org today", "example.org|URL|example.org")]
    [InlineData("open README.md and node.js", "")]
    [InlineData("file:///tmp/x", "file:///tmp/x|URL|file:///tmp/x")]
    [InlineData("note://abc", "")]
    [InlineData("ping 192.168.0.1 now", "192.168.0.1|IP_ADDRESS|192.168.0.1")]
    [InlineData("256.1.1.1", "")]
    [InlineData("010.1.1.1", "")]
    [InlineData("1.2.3.4.5", "")]
    [InlineData("addr 2001:db8::1.", "2001:db8::1|IP_ADDRESS|2001:db8::1")]
    [InlineData("::1", "::1|IP_ADDRESS|::1")]
    [InlineData("2001:0db8:0000:0000:0000:ff00:0042:8329", "2001:0db8:0000:0000:0000:ff00:0042:8329|IP_ADDRESS|2001:db8::ff00:42:8329")]
    [InlineData("::ffff:192.0.2.1", "::ffff:192.0.2.1|IP_ADDRESS|::ffff:c000:201")]
    [InlineData("at 10:30:15", "10|CARDINAL|10 ; 30|CARDINAL|30 ; 15|CARDINAL|15")]
    [InlineData("std::vector", "")]
    public void Addresses(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("call +1 (555) 123-4567 now", "+1 (555) 123-4567|PHONE_LIKE|+15551234567")]
    [InlineData("555-123-4567", "555-123-4567|PHONE_LIKE|5551234567")]
    [InlineData("555-1234", "555-1234|PHONE_LIKE|5551234")]
    [InlineData("020 7183 8750", "020 7183 8750|PHONE_LIKE|02071838750")]
    [InlineData("+44 20 7183 8750", "+44 20 7183 8750|PHONE_LIKE|+442071838750")]
    [InlineData("+442071838750", "+442071838750|PHONE_LIKE|+442071838750")]
    [InlineData("01.23.45.67.89", "01.23.45.67.89|PHONE_LIKE|0123456789")]
    [InlineData("on 2020-01-15", "2020|CARDINAL|2020 ; 01|CARDINAL|1 ; 15|CARDINAL|15")]
    [InlineData("1234567", "1234567|CARDINAL|1234567")]
    [InlineData("pi is 3.14159265", "3.14159265|CARDINAL|3.14159265")]
    [InlineData("4111 1111 1111 1111", "4111|CARDINAL|4111 ; 1111|CARDINAL|1111 ; 1111|CARDINAL|1111 ; 1111|CARDINAL|1111")]
    public void PhoneLike(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("#NLP rocks", "#NLP|HASHTAG|nlp")]
    [InlineData("C# is fine, #2024 too", "2024|CARDINAL|2024")]
    [InlineData("hi @Alice!", "@Alice|MENTION|alice")]
    [InlineData("@user@host.social", "@user@host.social|MENTION|user@host.social")]
    [InlineData("x@y.com", "x@y.com|EMAIL|x@y.com")]
    [InlineData("id 123e4567-e89b-12d3-a456-426614174000.", "123e4567-e89b-12d3-a456-426614174000|GUID|123e4567-e89b-12d3-a456-426614174000")]
    [InlineData("{123E4567-E89B-12D3-A456-426614174000}", "{123E4567-E89B-12D3-A456-426614174000}|GUID|123e4567-e89b-12d3-a456-426614174000")]
    [InlineData("urn:uuid:123e4567-e89b-12d3-a456-426614174000", "urn:uuid:123e4567-e89b-12d3-a456-426614174000|GUID|123e4567-e89b-12d3-a456-426614174000")]
    public void Tags(string text, string expected) => Assert.Equal(expected, Run(text));

    [Fact]
    public void LongestWinsAndOverlapsNeverRemain()
    {
        IReadOnlyList<Recognition> r = Recognizer.English.Recognize("Mail a@b.com 5 times, pay $5 million for 3 km at http://x.org/3");
        for (int i = 1; i < r.Count; i++)
        {
            Assert.True(r[i - 1].End <= r[i].Start);
        }

        Assert.Equal(["a@b.com", "5", "$5 million", "3 km", "http://x.org/3"], r.Select(x => x.Text));
    }

    [Fact]
    public void OffsetsAreIntoTheOriginalString()
    {
        const string text = "😀 emoji then 12 km and caf\u00e9 \u20ac7";
        foreach (Recognition r in Recognizer.English.Recognize(text))
        {
            Assert.Equal(text.Substring(r.Start, r.Length), r.Text);
        }

        Assert.Equal(2, Recognizer.English.Recognize(text).Count);
    }

    [Fact]
    public void EmptyAndHostileInputDoNotThrow()
    {
        Assert.Empty(Recognizer.English.Recognize(string.Empty));
        Assert.Empty(Recognizer.English.Recognize("   \n\t "));
        _ = Recognizer.English.Recognize(new string('9', 5000));
        _ = Recognizer.English.Recognize(string.Concat(Enumerable.Repeat("a.", 5000)));
        _ = Recognizer.English.Recognize(string.Concat(Enumerable.Repeat("@", 3000)));
        _ = Recognizer.English.Recognize(string.Concat(Enumerable.Repeat("1-", 3000)));
        _ = Recognizer.English.Recognize(string.Concat(Enumerable.Repeat("::", 3000)));
        _ = Recognizer.English.Recognize("nine hundred ninety-nine decillion " + new string('7', 40));
        Assert.Throws<ArgumentNullException>(() => Recognizer.English.Recognize(null!));
    }

    [Fact]
    public void TooLargeNumbersAreSkippedNotWrong()
    {
        Assert.Empty(Recognizer.English.Recognize(new string('9', 40)));
        Assert.Equal("one octillion|CARDINAL|1000000000000000000000000000", Run("one octillion"));
    }

    [Fact]
    public void ComponentWritesEntitiesOnTokens()
    {
        using Pipeline pipeline = Pipeline.Blank(Language.English).Add(ComponentRegistry.Create("recognizers"));
        Doc doc = pipeline.Process("Pay $5 million to bob@example.com for 3 km by the 1st.");
        Assert.Equal(
            ["$5 million:MONEY", "bob@example.com:EMAIL", "3 km:QUANTITY", "1st:ORDINAL"],
            doc.Ents.Select(e => e.Text.ToString() + ":" + e.Label));
    }

    [Fact]
    public void ComponentKeepsExistingEntitiesUnlessAskedToOverwrite()
    {
        static Doc Make(bool overwrite)
        {
            using Pipeline p = Pipeline.Blank(Language.English);
            Doc doc = p.Process("It cost 5 million dollars in 2020");
            doc.SetEntities([new SpanSpec(2, 4, "MISC")]);
            new RecognizerComponent(Recognizer.English, overwrite).Process(doc);
            return doc;
        }

        Assert.Equal(["5 million:MISC", "2020:DATE"], Make(false).Ents.Select(e => e.Text.ToString() + ":" + e.Label));
        Assert.Equal(["5 million dollars:MONEY", "2020:DATE"], Make(true).Ents.Select(e => e.Text.ToString() + ":" + e.Label));
    }

    [Fact]
    public void ANewLanguageIsOnlyData()
    {
        const string spanishFragment = """
            [number.cardinal]
            uno | unit | 1
            dos | unit | 2
            veinte | tens | 20
            mil | scale | 1000
            [number.connector]
            y
            [percent]
            %, por ciento
            [currency.name]
            euro, euros | EUR
            """;
        Recognizer es = Recognizer.FromRules([Encoding.UTF8.GetBytes(spanishFragment)], Language.Spanish);
        Assert.Equal("dos mil|CARDINAL|2000 ; veinte por ciento|PERCENT|20|percent ; dos euros|MONEY|2|EUR",
            string.Join(" ; ", es.Recognize("dos mil gatos, veinte por ciento y dos euros").Select(Describe)));
    }

    [Theory]
    [InlineData("[nope]\nx")]
    [InlineData("a | b")]
    [InlineData("[number.cardinal]\nuno | unit")]
    [InlineData("[number.cardinal]\nuno | huge | 1")]
    [InlineData("[number.cardinal]\nuno | unit | one")]
    [InlineData("[currency.code]\nusd")]
    [InlineData("[percent")]
    [InlineData("[unit]\nm | length | ,")]
    public void MalformedRulesAreRejectedWithTheLine(string rules)
    {
        RecognizerRulesException e = Assert.Throws<RecognizerRulesException>(() => Recognizer.FromRules([Encoding.UTF8.GetBytes(rules)]));
        Assert.Contains("ine ", e.Message, StringComparison.Ordinal);
        Assert.Throws<RecognizerRulesException>(() => Recognizer.FromRules([new byte[] { 0xFF, 0xFE, 0x41 }]));
    }

    [Fact]
    public void OnlyEnglishIsBuiltIn() => Assert.Throws<NotSupportedException>(() => Recognizer.For(Language.German));

    [Fact]
    public void HappyPathVerbsFilterByKind()
    {
        const string text = "Call 555-123-4567 or bob@example.com about the 3rd of 12 items costing $9";
        Assert.Equal(["3rd", "12", "$9"], Nlp.Quantities(text).Select(q => q.Text));
        Assert.Equal(["3rd", "12"], Nlp.Numbers(text).Select(q => q.Text));
        Assert.Equal(["555-123-4567", "bob@example.com"], Nlp.Sequences(text).Select(s => s.Text));
    }
}
