using System.Text;
using RecursiveAI.OneNlp.LanguageId;

namespace RecursiveAI.OneNlp.Fuzz;

/// <summary>
/// Feeds arbitrary text through the language detector and checks: confidences are probabilities that sum to 1, the best language is the
/// first confidence, restricting never answers outside the set, and the mixed-language runs tile the text in order.
/// </summary>
internal static class LanguageIdTargets
{
    private static readonly LanguageDetector? Restricted = LanguageDetector.TryGetDefault(out LanguageDetector? d0) ? d0.Restrict([Language.English, Language.German, Language.FromCode("ru")]) : null;

    internal static void Detect(ReadOnlySpan<byte> data)
    {
        // An odd first byte selects raw UTF-16 units, which reach lone surrogates that UTF-8 decoding never produces.
        string text = (data.Length > 0 && (data[0] & 1) == 1) ? FromUnits(data[1..]) : Encoding.UTF8.GetString(data);
        if (!LanguageDetector.TryGetDefault(out LanguageDetector? d))
        {
            return; // a build without the model has nothing to fuzz here
        }

        Language best = d.Detect(text, out double conf);
        IReadOnlyList<LanguageConfidence> all = d.Confidences(text);
        Require(all.Count == 0 ? best == default : all[0].Language == best, "best equals the first confidence");
        double sum = 0;
        foreach (LanguageConfidence c in all)
        {
            Require(c.Confidence is >= 0 and <= 1.000001, "confidence is a probability");
            sum += c.Confidence;
        }

        Require(all.Count == 0 || Math.Abs(sum - 1) < 1e-3, "confidences sum to 1");
        Require(conf is >= 0 and <= 1.000001, "confidence range");

        Language r = Restricted!.Detect(text);
        Require(r == default || Restricted.Languages.Contains(r), "restriction holds");

        IReadOnlyList<LanguageSpan> spans = d.DetectSpans(text);
        int pos = 0;
        foreach (LanguageSpan s in spans)
        {
            Require(s.Start == pos && s.End > s.Start && s.End <= text.Length, "spans tile the text");
            Require(s.Confidence is >= 0 and <= 1.000001, "span confidence");
            pos = s.End;
        }

        Require(spans.Count == 0 || pos == text.Length, "spans reach the end");
    }

    internal static void Model(ReadOnlySpan<byte> data)
    {
        try
        {
            using var ms = new MemoryStream(data.ToArray());
            LanguageDetector d = LanguageDetector.Load(ms);
            _ = d.Detect("Hello world, this is a test");
            _ = d.DetectSpans("Hello welt");
        }
        catch (InvalidDataException)
        {
        }
    }

    internal static void SeedModel(string dir)
    {
        using Stream? s = typeof(LanguageDetector).Assembly.GetManifestResourceStream(LanguageDetector.ResourceName);
        if (s is null)
        {
            return;
        }

        // The libFuzzer host passes inputs through a bounded buffer, so the multi-megabyte shipped model cannot be a seed itself. Re-emit it
        // in the same format with every group's bucket count cut to at most 2^8: a valid, small model whose header, class tables and weight
        // section all go through the loader.
        using var input = new BinaryReader(s);
        using var output = new MemoryStream();
        using var w = new BinaryWriter(output);
        w.Write(input.ReadBytes(5)); // "ONLI" and the version
        ushort languages = input.ReadUInt16();
        w.Write(languages);
        for (int i = 0; i < languages; i++)
        {
            byte length = input.ReadByte();
            w.Write(length);
            w.Write(input.ReadBytes(length));
        }

        w.Write(input.ReadBytes(8)); // exponent and temperature
        byte groups = input.ReadByte();
        w.Write(groups);
        for (int g = 0; g < groups; g++)
        {
            w.Write(input.ReadUInt32());
            byte bits = input.ReadByte();
            byte classes = input.ReadByte();
            byte kept = (byte)Math.Min(bits, (byte)8);
            w.Write(kept);
            w.Write(classes);
            w.Write(input.ReadBytes(2 * classes));
            w.Write(input.ReadBytes(4 + (4 * classes))); // weight scale and biases
            if (classes > 1)
            {
                byte[] rows = input.ReadBytes(checked((1 << bits) * classes));
                w.Write(rows, 0, (1 << kept) * classes);
            }
        }

        w.Flush();
        File.WriteAllBytes(Path.Combine(dir, "model-small"), output.ToArray());
    }

    private static string FromUnits(ReadOnlySpan<byte> data)
    {
        var chars = new char[data.Length / 2];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(data[2 * i] | (data[(2 * i) + 1] << 8));
        }

        return new string(chars);
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException("invariant failed: " + what);
        }
    }
}
