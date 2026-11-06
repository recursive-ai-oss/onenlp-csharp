using System.Globalization;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural;

/// <summary>
/// A module with no computation of its own that gathers the weights of a pipeline's encoders and heads under stable names
/// (<c>encoder0</c>, <c>head0</c>, ...) so one checkpoint covers all of them. It does not dispose its children: the encoders and
/// heads own their modules.
/// </summary>
internal sealed class StateModule : NnModule
{
    public StateModule Add(string name, NnModule module)
    {
        RegisterModule(name, module);
        return this;
    }

    public override Tensor Forward(Tensor input) => throw new NotSupportedException("A state module only holds weights.");

    /// <summary>The pipeline's trainable heads, in pipeline order, and the distinct encoders they read, in order of first use.</summary>
    public static (List<ITrainableComponent> Heads, List<IEncoder> Encoders) Collect(Pipeline pipeline)
    {
        var heads = new List<ITrainableComponent>();
        var encoders = new List<IEncoder>();

        // A trained word segmenter is the pipeline's tokenizer; its tagger and encoder come first, so a file without one numbers its weights as before.
        if (pipeline.Tokenizer is NeuralWordSegmenter segmenter)
        {
            heads.Add(segmenter.Head);
            encoders.Add(segmenter.Head.Encoder);
        }

        foreach (IPipelineComponent component in pipeline.Components)
        {
            if (component is ITrainableComponent head)
            {
                heads.Add(head);
                if (!encoders.Exists(e => ReferenceEquals(e, head.Encoder)))
                {
                    encoders.Add(head.Encoder);
                }
            }
        }

        return (heads, encoders);
    }

    /// <summary>The weights of encoders and heads under their stable names. Every head must be initialized.</summary>
    public static StateModule ForWeights(IReadOnlyList<IEncoder> encoders, IReadOnlyList<ITrainableComponent> heads, IEnumerable<int>? skipEncoders = null)
    {
        var state = new StateModule();
        HashSet<int> skip = skipEncoders is null ? [] : [.. skipEncoders];
        for (int i = 0; i < encoders.Count; i++)
        {
            if (!skip.Contains(i) && encoders[i].Weights is { } module)
            {
                state.Add("encoder" + i.ToString(CultureInfo.InvariantCulture), module);
            }
        }

        for (int i = 0; i < heads.Count; i++)
        {
            state.Add("head" + i.ToString(CultureInfo.InvariantCulture), heads[i].Weights);
        }

        return state;
    }
}
