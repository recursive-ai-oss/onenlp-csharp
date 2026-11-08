using System.Security.Cryptography;
using System.Xml.Linq;
using RecursiveAI.OneNlp.Models.Multilingual;

namespace RecursiveAI.OneNlp.Recipes.OpenNer;

internal static class PackageVerification
{
    internal static void Verify(string root)
    {
        Check("open-entities.onenlp", "src/OneNlp.Models.Multilingual/weights.manifest.props",
            typeof(MultilingualModel).Assembly.GetManifestResourceStream("weights/open-entities.onenlp")
                ?? throw new InvalidDataException("The package has no open-type head."));
        Check("tokenizer.json", "src/OneNlp.Models.Multilingual/weights.manifest.props",
            typeof(MultilingualModel).Assembly.GetManifestResourceStream("weights/tokenizer.json")
                ?? throw new InvalidDataException("The package has no tokenizer."));
        Check("student-v1.onnx", "src/OneNlp.Models.Multilingual.Parts/FrozenEncoder/weights.manifest.props",
            RecursiveAI.OneNlp.Models.Multilingual.FrozenEncoder.Part.Open());

        void Check(string name, string manifest, Stream stream)
        {
            using (stream)
            {
                XElement row = XDocument.Load(Path.Combine(root, manifest)).Descendants("MultilingualWeight")
                    .Single(e => (string?)e.Attribute("Include") == name);
                string expected = (string?)row.Attribute("Sha256") ?? throw new InvalidDataException("Missing hash.");
                long expectedSize = (long?)row.Attribute("Size") ?? throw new InvalidDataException("Missing size.");
                long size = stream.Length;
                string actual = Convert.ToHexStringLower(SHA256.HashData(stream));
                if (size != expectedSize || !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Packed {name} differs from {manifest}.");
                }

                Console.WriteLine($"verified {name} bytes={size} sha256={actual}");
            }
        }
    }
}
