using System.Buffers.Binary;
using System.Text;

namespace RecursiveAI.OneNlp.LanguageId;

/// <summary>
/// The trained detector: for each group of languages that share a script, a table of int8 weights indexed by hashed n-gram bucket
/// and class. The score of a class is a bias plus the sum of the weights of the text's features (a linear softmax model,
/// Joulin et al. 2016 without the low-rank layer).
/// </summary>
/// <remarks>
/// File layout (all integers little-endian): <c>"ONLI"</c>, version byte (1), language count (u16), then per language a length
/// byte and the ASCII code; exponent (f32), temperature (f32); group count (u8); per group: script mask (u32), bucket bits (u8),
/// class count (u8), class language indexes (u16 each), weight scale (f32), class biases (f32 each), and, when the group has more
/// than one class, <c>2^bits × classes</c> signed bytes row-major by bucket.
/// </remarks>
internal sealed class LanguageModel
{
    private const int MaxLanguages = 1024;
    private const int MaxGroups = 32;
    private const int MaxBits = 22;
    private const long MaxWeightBytes = 96L << 20;

    /// <summary>One script group.</summary>
    internal sealed class Group
    {
        public uint ScriptMask;
        public int Bits;
        public int[] Classes = [];
        public float Scale;
        public float[] Bias = [];
        public sbyte[] Weights = [];
    }

    /// <summary>ISO 639 codes, in model order.</summary>
    public string[] Codes { get; private set; } = [];

    /// <summary>The script groups.</summary>
    public Group[] Groups { get; private set; } = [];

    /// <summary>Exponent of the feature count that divides the summed weights.</summary>
    public float LengthExponent { get; private set; }

    /// <summary>Multiplier on the scores before the softmax that gives confidences.</summary>
    public float Temperature { get; private set; }

    private readonly int[] _scriptToGroup = new int[Scripts.Count];

    /// <summary>Builds a model from parts (the trainer uses this).</summary>
    public static LanguageModel Create(string[] codes, Group[] groups, float exponent, float temperature)
    {
        var m = new LanguageModel { Codes = codes, Groups = groups, LengthExponent = exponent, Temperature = temperature };
        m.Index();
        return m;
    }

    private void Index()
    {
        Array.Fill(_scriptToGroup, -1);
        for (int g = 0; g < Groups.Length; g++)
        {
            for (int s = 0; s < Scripts.Count; s++)
            {
                if ((Groups[g].ScriptMask & (1u << s)) != 0 && _scriptToGroup[s] < 0)
                {
                    _scriptToGroup[s] = g;
                }
            }
        }
    }

    /// <summary>The group that handles a script, or -1.</summary>
    public int GroupOf(Script script) => _scriptToGroup[(int)script];

    /// <summary>Reads a model.</summary>
    /// <exception cref="InvalidDataException">The data is truncated, has a wrong magic number or version, or declares sizes beyond the limits.</exception>
    public static LanguageModel Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var br = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        try
        {
            if (br.ReadUInt32() != 0x494C4E4Fu || br.ReadByte() != 1)
            {
                throw new InvalidDataException("Not a OneNLP language model (bad magic number or version).");
            }

            int nLang = br.ReadUInt16();
            if (nLang is 0 or > MaxLanguages)
            {
                throw new InvalidDataException("Language count out of range.");
            }

            var codes = new string[nLang];
            for (int i = 0; i < nLang; i++)
            {
                int len = br.ReadByte();
                if (len is < 2 or > 3)
                {
                    throw new InvalidDataException("Language code length out of range.");
                }

                codes[i] = Encoding.ASCII.GetString(br.ReadBytes(len));
                if (codes[i].Length != len || !codes[i].All(char.IsAsciiLetterLower))
                {
                    throw new InvalidDataException("Bad language code.");
                }
            }

            float exponent = br.ReadSingle();
            float temperature = br.ReadSingle();
            if (!float.IsFinite(exponent) || !float.IsFinite(temperature) || exponent is < 0 or > 2 || temperature is <= 0 or > 1000)
            {
                throw new InvalidDataException("Model parameters out of range.");
            }

            int nGroups = br.ReadByte();
            if (nGroups is 0 or > MaxGroups)
            {
                throw new InvalidDataException("Group count out of range.");
            }

            var groups = new Group[nGroups];
            long total = 0;
            for (int g = 0; g < nGroups; g++)
            {
                var grp = new Group { ScriptMask = br.ReadUInt32(), Bits = br.ReadByte() };
                int nc = br.ReadByte();
                if (nc == 0 || grp.Bits > MaxBits || (nc > 1 && grp.Bits < 4))
                {
                    throw new InvalidDataException("Group shape out of range.");
                }

                grp.Classes = new int[nc];
                for (int c = 0; c < nc; c++)
                {
                    grp.Classes[c] = br.ReadUInt16();
                    if (grp.Classes[c] >= nLang)
                    {
                        throw new InvalidDataException("Class language index out of range.");
                    }
                }

                grp.Scale = br.ReadSingle();
                if (!float.IsFinite(grp.Scale))
                {
                    throw new InvalidDataException("Weight scale is not finite.");
                }

                grp.Bias = new float[nc];
                for (int c = 0; c < nc; c++)
                {
                    grp.Bias[c] = br.ReadSingle();
                    if (!float.IsFinite(grp.Bias[c]))
                    {
                        throw new InvalidDataException("Bias is not finite.");
                    }
                }

                if (nc > 1)
                {
                    long bytes = (1L << grp.Bits) * nc;
                    total += bytes;
                    if (total > MaxWeightBytes)
                    {
                        throw new InvalidDataException("Weights exceed the size limit.");
                    }

                    if (stream.CanSeek && stream.Length - stream.Position < bytes)
                    {
                        throw new InvalidDataException("The model is truncated.");
                    }

                    byte[] raw = br.ReadBytes((int)bytes);
                    if (raw.Length != bytes)
                    {
                        throw new InvalidDataException("The model is truncated.");
                    }

                    grp.Weights = new sbyte[bytes];
                    Buffer.BlockCopy(raw, 0, grp.Weights, 0, raw.Length);
                }

                groups[g] = grp;
            }

            return Create(codes, groups, exponent, temperature);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException("The model is truncated.", e);
        }
    }

    /// <summary>Writes the model in the layout above.</summary>
    public void Write(Stream stream)
    {
        using var bw = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        bw.Write(0x494C4E4Fu);
        bw.Write((byte)1);
        bw.Write((ushort)Codes.Length);
        foreach (string c in Codes)
        {
            bw.Write((byte)c.Length);
            bw.Write(Encoding.ASCII.GetBytes(c));
        }

        bw.Write(LengthExponent);
        bw.Write(Temperature);
        bw.Write((byte)Groups.Length);
        foreach (Group g in Groups)
        {
            bw.Write(g.ScriptMask);
            bw.Write((byte)g.Bits);
            bw.Write((byte)g.Classes.Length);
            foreach (int c in g.Classes)
            {
                bw.Write((ushort)c);
            }

            bw.Write(g.Scale);
            foreach (float b in g.Bias)
            {
                bw.Write(b);
            }

            if (g.Classes.Length > 1)
            {
                byte[] raw = new byte[g.Weights.Length];
                Buffer.BlockCopy(g.Weights, 0, raw, 0, raw.Length);
                bw.Write(raw);
            }
        }
    }

    /// <summary>
    /// Adds up the weights of the features of words <c>[from, to)</c> that belong to <paramref name="group"/> and writes the class
    /// scores (bias plus normalized sum) into <paramref name="scores"/>.
    /// </summary>
    /// <returns>The number of features that were added.</returns>
    public int Score(LangIdText t, int from, int to, int group, Span<float> scores)
    {
        Group g = Groups[group];
        int nc = g.Classes.Length;
        scores = scores[..nc];
        if (nc == 1)
        {
            scores[0] = 0;
            return 0;
        }

        Span<int> acc = stackalloc int[nc]; // nc <= 255 (a byte); a few hundred bytes at most
        acc.Clear();
        int count = 0;
        int shift = 32 - g.Bits;
        sbyte[] w = g.Weights;
        for (int word = from; word < to; word++)
        {
            if (GroupOf(t.WordScript[word]) != group)
            {
                continue;
            }

            for (int i = t.HashStart[word]; i < t.HashStart[word + 1]; i++)
            {
                int row = (int)((uint)t.Hashes[i] >> shift) * nc;
                ReadOnlySpan<sbyte> r = w.AsSpan(row, nc);
                for (int c = 0; c < nc; c++)
                {
                    acc[c] += r[c];
                }

                count++;
            }
        }

        float norm = count == 0 ? 1f : g.Scale / MathF.Pow(count, LengthExponent);
        for (int c = 0; c < nc; c++)
        {
            scores[c] = g.Bias[c] + (acc[c] * norm);
        }

        return count;
    }
}
