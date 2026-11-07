using RecursiveAI.OneNlp;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Samples;

// Training: the same Train call the shipped models use, on a toy set so it finishes in seconds. docs/training.md shows it.
internal static class TrainSample
{
    internal static void Run()
    {
        Console.WriteLine("training");

        // <sample>
        // Labelled texts as JSON Lines: {"text": ..., "cats": {label: 0 or 1}}. Real data comes from JsonlReader.Read(path) or ConllUReader.
        string[] lines =
        [
            "{\"text\": \"my card has not arrived\", \"cats\": {\"card\": 1, \"pin\": 0}}",
            "{\"text\": \"where is my new card\", \"cats\": {\"card\": 1, \"pin\": 0}}",
            "{\"text\": \"the card never came in the post\", \"cats\": {\"card\": 1, \"pin\": 0}}",
            "{\"text\": \"I forgot my pin\", \"cats\": {\"card\": 0, \"pin\": 1}}",
            "{\"text\": \"how do I change my pin\", \"cats\": {\"card\": 0, \"pin\": 1}}",
            "{\"text\": \"my pin is not working\", \"cats\": {\"card\": 0, \"pin\": 1}}",
        ];
        using Pipeline nlp = Pipeline.Blank(Language.English)
            .Add(new TextClassifier(FastEncoder.English(7)) { OwnsEncoder = true });
        List<Example> examples = [.. JsonlReader.Read(new StringReader(string.Join('\n', lines)), nlp.Strings, new JsonlOptions { Tokenizer = nlp.Tokenizer })];

        // A fixed seed makes the run repeatable; with CheckpointDirectory set, an interrupted run resumes from its last checkpoint.
        TrainResult result = nlp.Train(examples, new TrainOptions { Epochs = 30, Seed = 7, Dropout = 0f, DevExamples = examples });

        string path = Path.Combine(Path.GetTempPath(), "onenlp-sample-" + Guid.NewGuid().ToString("N") + ".onenlp");
        PipelineFile.Save(nlp, path);                                  // one file: tokenizer, encoder, heads, labels
        using Pipeline loaded = PipelineFile.Load(path);
        Doc doc = loaded.Process("I lost my pin");
        // doc.Cats maps each label to a probability
        // </sample>

        File.Delete(path);
        Console.WriteLine($"    cats: {string.Join(", ", doc.Cats.Select(c => $"{c.Key}={c.Value:F2}"))}");
        Program.Check("training finished", result is not null);
        Program.Check("the saved model predicts what the trained one does", doc.Cats["pin"] > doc.Cats["card"]);
    }
}
