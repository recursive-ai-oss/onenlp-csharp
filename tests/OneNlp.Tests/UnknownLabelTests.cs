using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>A head that already has weights has a fixed label set: values it lacks are refused by name, never skipped.</summary>
public class UnknownLabelTests
{
    private static HashEmbeddingEncoder NewEncoder() => new(width: 16, buckets: 512, seed: 1);

    private static Example Annotated(Pipeline nlp, string text, Action<Doc> annotate)
    {
        Doc doc = nlp.Tokenizer.Tokenize(text, nlp.Strings);
        annotate(doc);
        return Example.FromReference(doc);
    }

    private static void AssertRefuses(ITrainableComponent head, IReadOnlyList<Example> known, IReadOnlyList<Example> other, string value)
    {
        head.Initialize(known, seed: 1);
        Assert.Empty(head.ExtendLabels(known, seed: 2));
        ArgumentException ex = Assert.Throws<ArgumentException>(() => head.ExtendLabels(other, seed: 2));
        Assert.Contains(value, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMorphologizerRefusesCombinationsItDoesNotHave()
    {
        using Pipeline nlp = Pipeline.Blank();
        using var head = new Morphologizer(NewEncoder()) { OwnsEncoder = true };
        Example Make(string pos, string morph) => Annotated(nlp, "a b", d =>
        {
            for (int i = 0; i < d.Length; i++)
            {
                d.SetAnnotation(TokenColumn.Pos, i, pos);
                d.SetAnnotation(TokenColumn.Morph, i, morph);
            }
        });

        AssertRefuses(head, [Make("NOUN", "Number=Sing")], [Make("NOUN", "Number=Plur")], "Number=Plur");
    }

    [Fact]
    public void ALemmatizerRefusesEditTreesItDoesNotHaveButToleratesRareOnes()
    {
        using Pipeline nlp = Pipeline.Blank();
        using var head = new EditTreeLemmatizer(NewEncoder(), minClassCount: 2) { OwnsEncoder = true };
        Example Make(string text, params string[] lemmas) => Annotated(nlp, text, d =>
        {
            for (int i = 0; i < d.Length; i++)
            {
                d.SetAnnotation(TokenColumn.Lemma, i, lemmas[i]);
            }
        });

        // "cats cats dogs" -> cat cat dog: the tree "drop s" is seen three times. "went"->"go" is a whole-word substitution seen once.
        Example known = Make("cats dogs went", "cat", "dog", "go");
        Example twice = Make("cats dogs", "cat", "dog");
        head.Initialize([known, twice], seed: 1);
        Assert.Empty(head.ExtendLabels([known, twice], seed: 2));

        // A new tree seen once is below the minimum count, as at initialization, so it does not stop a resumed run.
        Assert.Empty(head.ExtendLabels([Make("ran", "run")], seed: 2));

        // A new tree seen at least the minimum number of times is refused, and named.
        ArgumentException ex = Assert.Throws<ArgumentException>(() => head.ExtendLabels([Make("playing walking", "play", "walk")], seed: 2));
        Assert.Contains("does not have", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AParserRefusesRelationsItDoesNotHave()
    {
        using Pipeline nlp = Pipeline.Blank();
        using var head = new Parser(NewEncoder(), arcSize: 16, labelSize: 16) { OwnsEncoder = true };
        Example Make(string label) => Annotated(nlp, "a b", d =>
        {
            for (int i = 0; i < d.Length; i++)
            {
                d.SetAnnotation(TokenColumn.Dep, i, label);
            }
        });

        AssertRefuses(head, [Make("nsubj")], [Make("obl:tmod")], "obl:tmod");
    }

    [Fact]
    public void ASpanCategorizerRefusesLabelsItDoesNotHave()
    {
        using Pipeline nlp = Pipeline.Blank();
        using var head = new SpanCategorizer(NewEncoder()) { OwnsEncoder = true };
        Example Make(string label)
        {
            Doc doc = nlp.Tokenizer.Tokenize("a b", nlp.Strings);
            return Example.FromReference(doc, spans: new Dictionary<string, IReadOnlyList<SpanSpec>> { ["sc"] = [new SpanSpec(0, 1, label)] });
        }

        AssertRefuses(head, [Make("PART")], [Make("WHOLE")], "WHOLE");
    }
}
