using RecursiveAI.OneNlp.Fuzz;
using SharpFuzz;

// One fuzz executable, one target per input surface. The target is chosen by the ONENLP_FUZZ_TARGET environment variable because
// libFuzzer's .NET host starts the program without command-line arguments.
//
// To add a target: write a static method `Run(ReadOnlySpan<byte> data)` that lets only the library's documented exceptions
// escape, then add one line to `Targets` below.
//
// Without a libFuzzer host:
//   replay <target> <file>...   runs saved inputs once each (reproduces a finding)
//   seeds <target> <dir>        writes valid inputs for the target into <dir>
//   random <target> <count>     runs <count> random and mutated inputs from a fixed seed, as a smoke check
internal static class Program
{
    private delegate void Target(ReadOnlySpan<byte> data);

    private static readonly Dictionary<string, Target> Targets = new()
    {
        ["word-tokenizer"] = WordTokenizerTarget.Run,
        ["doc-json"] = SerializationTargets.Json,
        ["doc-binary"] = SerializationTargets.Binary,
        ["docbin"] = SerializationTargets.Bin,
    };

    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "seeds")
        {
            Directory.CreateDirectory(args[2]);
            switch (args[1])
            {
                case "doc-json":
                    SerializationTargets.SeedJson(args[2]);
                    break;
                case "doc-binary":
                    SerializationTargets.SeedBinary(args[2]);
                    break;
                case "docbin":
                    SerializationTargets.SeedBin(args[2]);
                    break;
            }

            return 0;
        }

        if (args.Length > 2 && args[0] == "replay" && Targets.TryGetValue(args[1], out Target? replayed))
        {
            foreach (string file in args.Skip(2))
            {
                replayed(File.ReadAllBytes(file));
                Console.WriteLine("ok " + file);
            }

            return 0;
        }

        if (args.Length == 3 && args[0] == "random" && Targets.TryGetValue(args[1], out Target? target) && int.TryParse(args[2], out int count))
        {
            var random = new Random(12345);
            byte[] seed = System.Text.Encoding.UTF8.GetBytes("Hello, world! It's 3.14 - 日本語 😀 e\u0301 end");
            for (int i = 0; i < count; i++)
            {
                byte[] input = random.Next(3) == 0 ? new byte[random.Next(64)] : (byte[])seed.Clone();
                if (input.Length > 0 && input != seed)
                {
                    random.NextBytes(input.AsSpan(0, random.Next(input.Length + 1)));
                }

                target(input);
            }

            Console.WriteLine($"ok {count} inputs");
            return 0;
        }

        string? name = Environment.GetEnvironmentVariable("ONENLP_FUZZ_TARGET") ?? (args.Length > 0 ? args[0] : null);
        if (name is null || !Targets.TryGetValue(name, out Target? libFuzzerTarget))
        {
            Console.Error.WriteLine("usage: set ONENLP_FUZZ_TARGET to one of: " + string.Join(", ", Targets.Keys));
            return 2;
        }

        Fuzzer.LibFuzzer.Run(data => libFuzzerTarget(data));
        return 0;
    }
}
