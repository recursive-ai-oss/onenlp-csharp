using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;

namespace RecursiveAI.OneNlp.Neural.Training;

/// <summary>Settings for <see cref="PipelineTraining.Train(Pipeline, IReadOnlyList{Example}, TrainOptions, CancellationToken)"/>. Every property has a default that trains a small model sensibly.</summary>
public sealed class TrainOptions
{
    /// <summary>The most passes over the training examples. Default 10.</summary>
    public int Epochs { get; init; } = 10;

    /// <summary>Stops after this many optimizer steps in total, mid-epoch if need be; 0 means no limit (default).</summary>
    public long MaxSteps { get; init; }

    /// <summary>
    /// The size of a batch in tokens: examples are added to a batch, in shuffled order, until the next would push it past this
    /// number (a batch always holds at least one example). Default 1000.
    /// </summary>
    public int BatchWords { get; init; } = 1000;

    /// <summary>The AdamW learning rate before any schedule. Default 0.003.</summary>
    public float LearningRate { get; init; } = 0.003f;

    /// <summary>AdamW's decoupled weight decay. Default 0.01.</summary>
    public float WeightDecay { get; init; } = 0.01f;

    /// <summary>Steps of linear learning-rate warm-up (ignored when <see cref="Schedule"/> is set). Default 0.</summary>
    public int WarmupSteps { get; init; }

    /// <summary>The learning-rate schedule; a constant <see cref="LearningRate"/> after <see cref="WarmupSteps"/> when <see langword="null"/>.</summary>
    public LrSchedule? Schedule { get; init; }

    /// <summary>The gradient norm to clip to; 0 turns clipping off. Default 1.</summary>
    public float ClipNorm { get; init; } = 1f;

    /// <summary>The probability of dropping an encoder output element during training, from 0 up to but not including 1. Default 0.1.</summary>
    public float Dropout { get; init; } = 0.1f;

    /// <summary>Seed for shuffling, the train/dev split, dropout and the initial weights of heads. Default 0.</summary>
    public ulong Seed { get; init; }

    /// <summary>Do not train the encoder's weights (the heads still train). Default false.</summary>
    public bool FreezeEncoder { get; init; }

    /// <summary>Held-out examples to select on. When <see langword="null"/>, <see cref="DevFraction"/> of the training examples are split off.</summary>
    public IReadOnlyList<Example>? DevExamples { get; init; }

    /// <summary>The fraction of examples held out as dev when <see cref="DevExamples"/> is not given; 0 means no dev set. Default 0.1.</summary>
    public double DevFraction { get; init; } = 0.1;

    /// <summary>
    /// Scores the pipeline on the dev set; higher is better. It receives the dev examples with their <see cref="Example.Predicted"/>
    /// documents annotated by the pipeline. When <see langword="null"/> the score is the negative mean dev loss.
    /// </summary>
    public Func<IReadOnlyList<Example>, double>? Score { get; init; }

    /// <summary>Evaluate on dev every this many steps; 0 means at the end of each epoch (default).</summary>
    public int EvalEvery { get; init; }

    /// <summary>Stop when this many evaluations in a row have not beaten the best score; 0 turns early stopping off (default).</summary>
    public int Patience { get; init; }

    /// <summary>After training, put back the weights of the best evaluation. Default true.</summary>
    public bool RestoreBest { get; init; } = true;

    /// <summary>
    /// A directory for <c>last.ckpt</c> (everything needed to resume) and <c>best.ckpt</c> (the best weights); created if missing.
    /// When <see langword="null"/> nothing is written and a run cannot be resumed.
    /// </summary>
    public string? CheckpointDirectory { get; init; }

    /// <summary>Write <c>last.ckpt</c> every this many steps, and always after an evaluation; 0 means only after evaluations and at the end (default).</summary>
    public int CheckpointEvery { get; init; }

    /// <summary>Continue from <c>last.ckpt</c> in <see cref="CheckpointDirectory"/> when it exists. Default true.</summary>
    public bool Resume { get; init; } = true;

    /// <summary>Receives a report after every step. The library writes nothing to the console.</summary>
    public IProgress<TrainProgress>? Progress { get; init; }
}

/// <summary>A report on one finished training step.</summary>
/// <param name="Step">The number of optimizer steps taken so far, counting any before a resume.</param>
/// <param name="Epoch">The epoch this step belonged to, from 0.</param>
/// <param name="Loss">The training loss of the batch.</param>
/// <param name="LearningRate">The learning rate used for the step.</param>
/// <param name="DevScore">The dev score when an evaluation followed this step, else <see langword="null"/>.</param>
/// <param name="IsBest">Whether that evaluation set a new best score.</param>
public sealed record TrainProgress(long Step, int Epoch, float Loss, float LearningRate, double? DevScore, bool IsBest);

/// <summary>What <see cref="PipelineTraining.Train(Pipeline, IReadOnlyList{Example}, TrainOptions, CancellationToken)"/> did.</summary>
/// <param name="Steps">The total number of optimizer steps, counting any before a resume.</param>
/// <param name="Epochs">The number of epochs finished.</param>
/// <param name="Completed"><see langword="false"/> when the run was cancelled before it finished.</param>
/// <param name="StoppedEarly">Whether patience ended the run.</param>
/// <param name="Evaluations">The number of dev evaluations.</param>
/// <param name="BestScore">The best dev score, or <see cref="double.NaN"/> when there was no evaluation.</param>
/// <param name="BestStep">The step of the best evaluation, or 0.</param>
/// <param name="WeightsSha256">The SHA-256 (hex, lower case) of the encoder and head weights in their checkpoint encoding, after any restore of the best.</param>
public sealed record TrainResult(
    long Steps,
    int Epochs,
    bool Completed,
    bool StoppedEarly,
    int Evaluations,
    double BestScore,
    long BestStep,
    string WeightsSha256);
