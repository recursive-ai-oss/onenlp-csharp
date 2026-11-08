using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Data;
using RecursiveAI.OneNlp.Neural.Components;
using RecursiveAI.OneNlp.Neural.Encoders;
using RecursiveAI.OneNlp.Neural;

// The extension lives in the namespace of Pipeline, so nlp.Train(...) needs no using beyond the one every program has.
namespace RecursiveAI.OneNlp;

/// <summary>Trains the neural components of a <see cref="Pipeline"/>.</summary>
public static class PipelineTraining
{
    /// <summary>
    /// Trains every <see cref="ITrainableComponent"/> of the pipeline, and the encoders they read, on examples: AdamW with an optional
    /// schedule and gradient clipping, batches by token count, dropout on the encoder output, a deterministic train/dev split,
    /// evaluation on dev, early stopping and checkpoints. Heads that have no weights yet are initialized from the training examples; a head that already has weights keeps them and learns the labels the examples add (an entity recognizer appends them; a tagger or text classifier throws <see cref="ArgumentException"/> naming labels it does not have).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Resuming.</b> With <see cref="TrainOptions.CheckpointDirectory"/> set, the full training state (weights, optimizer moments,
    /// dropout counter, position in the data, best score and patience) is written to <c>last.ckpt</c> after evaluations, every
    /// <see cref="TrainOptions.CheckpointEvery"/> steps and at the end, each time to a temporary file that is then renamed. A later call
    /// with the same pipeline structure, examples and options continues from it and finishes with exactly the weights the
    /// uninterrupted run would have had (same machine, same thread count). The order of examples in every epoch is a function of the
    /// seed and the epoch number alone.
    /// </para>
    /// <para>
    /// Training is synchronous CPU work. Cancelling stops at the next step boundary and returns a result with
    /// <see cref="TrainResult.Completed"/> false; the last checkpoint on disk is the resume point.
    /// </para>
    /// </remarks>
    /// <param name="pipeline">The pipeline. Its components run in order to score dev examples.</param>
    /// <param name="examples">The training examples. Those whose predicted document is empty are skipped.</param>
    /// <param name="options">The settings; defaults when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run at a step boundary.</param>
    /// <returns>The outcome, including the hash of the final weights.</returns>
    /// <exception cref="InvalidOperationException">The pipeline has no trainable component; the loss became non-finite; or a checkpoint was made for different data or settings.</exception>
    /// <exception cref="ArgumentException">There are no usable training examples, or an option is out of range.</exception>
    public static TrainResult Train(this Pipeline pipeline, IReadOnlyList<Example> examples, TrainOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(examples);
        using var trainer = new Trainer(pipeline, examples, options ?? new TrainOptions(), cancellationToken);
        return trainer.Run();
    }

    /// <summary>
    /// Trains the pipeline for a number of epochs with the default settings: shorthand for the other overload with <see cref="TrainOptions.Epochs"/> set.
    /// </summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="examples">The training examples.</param>
    /// <param name="epochs">The maximum number of epochs, at least 1; early stopping may end the run sooner.</param>
    /// <param name="cancellationToken">Stops the run at a step boundary.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="InvalidOperationException">See the other overload.</exception>
    /// <exception cref="ArgumentException">See the other overload; or <paramref name="epochs"/> is out of range.</exception>
    public static TrainResult Train(this Pipeline pipeline, IReadOnlyList<Example> examples, int epochs, CancellationToken cancellationToken = default) =>
        pipeline.Train(examples, new TrainOptions { Epochs = epochs }, cancellationToken);

    private sealed class Trainer : IDisposable
    {
        private const string LastName = "last.ckpt";
        private const string BestName = "best.ckpt";
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

        private readonly Pipeline _pipeline;
        private readonly TrainOptions _o;
        private readonly CancellationToken _ct;
        private readonly List<Example> _train;
        private readonly List<Example> _dev;
        private readonly List<ITrainableComponent> _heads;
        private readonly List<IEncoder> _encoders;
        private readonly StateModule _weights;
        private readonly OneModel.Training.Dropout _dropout;
        private readonly StateModule _state;
        private readonly Tensor[] _parameters;
        private readonly AdamW _optimizer;
        private readonly LrSchedule _schedule;

        private long _step;
        private int _epoch;
        private int _batch;
        private int _evals;
        private int _sinceBest;
        private long _bestStep;
        private double _bestScore = double.NaN;
        private bool _hasBest;
        private bool _done;
        private bool _stoppedEarly;
        private byte[]? _bestSnapshot;
        private bool _bestOnDisk;

        public Trainer(Pipeline pipeline, IReadOnlyList<Example> examples, TrainOptions options, CancellationToken ct)
        {
            _pipeline = pipeline;
            _o = options;
            _ct = ct;
            Validate(options);

            (_heads, _encoders) = StateModule.Collect(pipeline);
            if (_heads.Count == 0)
            {
                throw new InvalidOperationException("The pipeline has no trainable component. Add a head such as Tagger.");
            }

            var usable = examples.Where(e => e.Predicted.Length > 0).ToList();
            if (usable.Count == 0)
            {
                throw new ArgumentException("There are no training examples with at least one token.", nameof(examples));
            }

            (_train, _dev) = Split(usable, options);
            for (int i = 0; i < _heads.Count; i++)
            {
                if (!_heads[i].IsInitialized)
                {
                    _heads[i].Initialize(_train, options.Seed + 1 + (ulong)i);
                }
                else
                {
                    // spaCy's "add a label, then train": a head with weights grows to the labels of the new data, or refuses them.
                    _heads[i].ExtendLabels(_train, options.Seed + 1 + (ulong)i);
                }
            }

            _weights = StateModule.ForWeights(_encoders, _heads);
            _dropout = new OneModel.Training.Dropout(options.Dropout, options.Seed ^ 0xD6E8FEB86659FD93UL);
            _state = new StateModule().Add("model", _weights).Add("dropout", _dropout);

            var parameters = new List<Tensor>();
            foreach (ITrainableComponent head in _heads)
            {
                parameters.AddRange(head.Weights.Parameters());
            }

            if (!options.FreezeEncoder)
            {
                foreach (IEncoder encoder in _encoders)
                {
                    if (encoder.Weights is { } module)
                    {
                        parameters.AddRange(module.Parameters());
                    }
                }
            }

            _parameters = [.. parameters];
            _optimizer = new AdamW(_parameters, options.LearningRate, weightDecay: options.WeightDecay);
            _schedule = options.Schedule
                ?? (options.WarmupSteps > 0 ? new WarmupLr(new ConstantLr(options.LearningRate), options.WarmupSteps) : new ConstantLr(options.LearningRate));
        }

        public TrainResult Run()
        {
            if (_o.CheckpointDirectory is not null)
            {
                Directory.CreateDirectory(_o.CheckpointDirectory);
                if (_o.Resume)
                {
                    TryResume();
                }
            }

            if (_o.InitializeFrom is not null && _step == 0 && _batch == 0 && !(_o.CheckpointDirectory is not null && File.Exists(Path.Combine(_o.CheckpointDirectory, LastName))))
            {
                // A fresh run that starts from the weights and optimizer moments of an earlier run: the step counter of the run restarts at 0
                // while the optimizer keeps its own count, so a schedule given by the caller sees steps continuing from the earlier run.
                Checkpoint.Load(_o.InitializeFrom, _state, _optimizer);
            }

            while (!_done)
            {
                if (_epoch >= _o.Epochs || (_o.MaxSteps > 0 && _step >= _o.MaxSteps))
                {
                    Finish(stoppedEarly: false);
                    break;
                }

                List<int[]> batches = MakeBatches(_epoch);
                while (_batch < batches.Count)
                {
                    if (_ct.IsCancellationRequested)
                    {
                        // Between two steps the state is consistent: keep everything done so far.
                        if (_o.CheckpointDirectory is not null && _step > 0)
                        {
                            SaveLast();
                        }

                        return Result(completed: false);
                    }

                    (float loss, float rate) = TrainStep(batches[_batch]);
                    _step++;
                    _batch++;
                    bool epochEnd = _batch == batches.Count;
                    if (epochEnd)
                    {
                        _epoch++;
                        _batch = 0;
                    }

                    bool evaluate = _dev.Count > 0 && (_o.EvalEvery > 0 ? _step % _o.EvalEvery == 0 : epochEnd);
                    double? score = null;
                    bool isBest = false;
                    if (evaluate)
                    {
                        (score, isBest) = EvaluateAndTrack();
                    }

                    bool stop = _o.Patience > 0 && _sinceBest >= _o.Patience;
                    bool finished = stop || _epoch >= _o.Epochs || (_o.MaxSteps > 0 && _step >= _o.MaxSteps);
                    if (finished)
                    {
                        Finish(stop);
                    }
                    else if (_o.CheckpointDirectory is not null && (evaluate || (_o.CheckpointEvery > 0 && _step % _o.CheckpointEvery == 0)))
                    {
                        SaveLast();
                    }

                    _o.Progress?.Report(new TrainProgress(_step, epochEnd ? _epoch - 1 : _epoch, loss, rate, score, isBest));
                    if (_done || epochEnd)
                    {
                        break;
                    }
                }
            }

            return Result(completed: true);
        }

        // The weights modules belong to the pipeline's heads and encoders; only what the trainer created is released here.
        public void Dispose()
        {
            _optimizer.Dispose();
            _dropout.Dispose();
        }

        private static void Validate(TrainOptions o)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(o.Epochs, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(o.BatchWords, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(o.Patience);
            ArgumentOutOfRangeException.ThrowIfNegative(o.EvalEvery);
            ArgumentOutOfRangeException.ThrowIfNegative(o.CheckpointEvery);
            ArgumentOutOfRangeException.ThrowIfNegative(o.MaxSteps);
            if (!(o.DevFraction >= 0 && o.DevFraction < 1))
            {
                throw new ArgumentOutOfRangeException(nameof(o), "DevFraction must be from 0 up to but not including 1.");
            }
        }

        private static (List<Example> Train, List<Example> Dev) Split(List<Example> all, TrainOptions o)
        {
            if (o.DevExamples is not null)
            {
                return (all, o.DevExamples.Where(e => e.Predicted.Length > 0).ToList());
            }

            if (o.DevFraction <= 0 || all.Count < 2)
            {
                return (all, []);
            }

            int devCount = Math.Clamp((int)Math.Round(all.Count * o.DevFraction), 1, all.Count - 1);
            int[] order = Shuffled(all.Count, new Generator(o.Seed ^ 0xA0761D6478BD642FUL));
            var inDev = new bool[all.Count];
            for (int i = 0; i < devCount; i++)
            {
                inDev[order[i]] = true;
            }

            var train = new List<Example>(all.Count - devCount);
            var dev = new List<Example>(devCount);
            for (int i = 0; i < all.Count; i++)
            {
                (inDev[i] ? dev : train).Add(all[i]);
            }

            return (train, dev);
        }

        private static int[] Shuffled(int count, Generator generator)
        {
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            for (int i = count - 1; i > 0; i--)
            {
                int j = (int)(generator.NextUInt64() % (ulong)(i + 1));
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }

        // Batches by word count (spaCy documents batching by words for the same reason): sentence lengths vary a lot, and
        // a fixed number of sentences would make some steps far slower than others.
        private List<int[]> MakeBatches(int epoch)
        {
            int[] order = Shuffled(_train.Count, new Generator((_o.Seed * GoldenGamma) + (ulong)epoch + 1));
            var batches = new List<int[]>();
            var current = new List<int>();
            int words = 0;
            foreach (int index in order)
            {
                int length = _train[index].Predicted.Length;
                if (current.Count > 0 && words + length > _o.BatchWords)
                {
                    batches.Add([.. current]);
                    current.Clear();
                    words = 0;
                }

                current.Add(index);
                words += length;
            }

            if (current.Count > 0)
            {
                batches.Add([.. current]);
            }

            return batches;
        }

        private (float Loss, float Rate) TrainStep(int[] indices)
        {
            SetMode(training: true);
            var batch = new Example[indices.Length];
            for (int i = 0; i < batch.Length; i++)
            {
                batch[i] = _train[indices[i]];
            }

            using var scope = new ActivationScope();
            Tensor loss = BatchLoss(batch, training: true);
            float value = loss.Data<float>()[0];
            if (!float.IsFinite(value))
            {
                throw new InvalidOperationException($"The training loss is {value} at step {_step + 1}. Lower the learning rate or check the data.");
            }

            loss.Backward();
            if (_o.ClipNorm > 0)
            {
                GradClip.ClipNorm(_parameters, _o.ClipNorm);
            }

            _schedule.Apply(_optimizer);
            float rate = _optimizer.LearningRate;
            _optimizer.Step();
            _optimizer.ZeroGrad();
            return (value, rate);
        }

        // Encodes the batch once per distinct encoder, then adds up the heads' losses.
        private Tensor BatchLoss(Example[] batch, bool training)
        {
            var docs = new Doc[batch.Length];
            for (int i = 0; i < docs.Length; i++)
            {
                docs[i] = batch[i].Predicted;
            }

            var encoded = new EncodedBatch[_encoders.Count];
            for (int e = 0; e < _encoders.Count; e++)
            {
                if (_o.FreezeEncoder || !training)
                {
                    using NoGradScope noGrad = GradMode.NoGrad();
                    encoded[e] = _encoders[e].Encode(docs);
                }
                else
                {
                    encoded[e] = _encoders[e].Encode(docs);
                }

                if (training && _o.Dropout > 0f)
                {
                    encoded[e] = encoded[e].WithVectors(_dropout.Forward(encoded[e].Vectors));
                }
            }

            Tensor total = default;
            for (int h = 0; h < _heads.Count; h++)
            {
                int e = _encoders.FindIndex(x => ReferenceEquals(x, _heads[h].Encoder));
                Tensor loss = _heads[h].Loss(encoded[e], batch);
                total = h == 0 ? loss : Ops.Add(total, loss);
            }

            return total;
        }

        private void SetMode(bool training)
        {
            _weights.Train(training);
            _dropout.Train(training);
            if (training && _o.FreezeEncoder)
            {
                foreach (IEncoder encoder in _encoders)
                {
                    encoder.Weights?.Eval();
                }
            }
        }

        private (double Score, bool IsBest) EvaluateAndTrack()
        {
            double score = Evaluate();
            _evals++;
            bool better = !_hasBest || score > _bestScore;
            if (better)
            {
                _hasBest = true;
                _bestScore = score;
                _bestStep = _step;
                _sinceBest = 0;
                if (_o.CheckpointDirectory is not null)
                {
                    // Streamed to the file: the weights of a many-head pipeline are larger than an in-memory array can be.
                    Checkpoint.Save(Path.Combine(_o.CheckpointDirectory, BestName), _weights);
                    _bestSnapshot = null;
                    _bestOnDisk = true;
                }
                else
                {
                    using var ms = new MemoryStream();
                    Checkpoint.Save(ms, _weights);
                    _bestSnapshot = ms.ToArray();
                }
            }
            else
            {
                _sinceBest++;
            }

            return (score, better);
        }

        private double Evaluate()
        {
            SetMode(training: false);
            using NoGradScope noGrad = GradMode.NoGrad();
            if (_o.Score is not null)
            {
                var scored = new List<Example>(_dev.Count);
                foreach (Example e in _dev)
                {
                    scored.Add(Example.FromReference(e.Reference, e.Cats, e.Spans));
                }

                const int Chunk = 128;
                var docs = new Doc[Chunk];
                for (int start = 0; start < scored.Count; start += Chunk)
                {
                    int n = Math.Min(Chunk, scored.Count - start);
                    for (int i = 0; i < n; i++)
                    {
                        docs[i] = scored[start + i].Predicted;
                    }

                    foreach (IPipelineComponent component in _pipeline.Components)
                    {
                        component.ProcessBatch(docs.AsSpan(0, n));
                    }
                }

                return _o.Score(scored);
            }

            // No scorer given: the negative mean dev loss, so "higher is better" holds.
            double sum = 0;
            int batches = 0;
            const int DevBatch = 32;
            for (int start = 0; start < _dev.Count; start += DevBatch)
            {
                Example[] batch = _dev.GetRange(start, Math.Min(DevBatch, _dev.Count - start)).ToArray();
                using var scope = new ActivationScope();
                Tensor loss = BatchLoss(batch, training: false);
                sum += loss.Data<float>()[0];
                batches++;
            }

            return -sum / batches;
        }

        private void Finish(bool stoppedEarly)
        {
            _done = true;
            _stoppedEarly = stoppedEarly;
            if (_o.CheckpointDirectory is not null)
            {
                SaveLast();
            }

            SetMode(training: false);
            RestoreBestWeights();
        }

        private void RestoreBestWeights()
        {
            if (!_o.RestoreBest)
            {
                return;
            }

            if (_bestOnDisk)
            {
                Checkpoint.Load(Path.Combine(_o.CheckpointDirectory!, BestName), _weights);
            }
            else if (_bestSnapshot is not null)
            {
                using var ms = new MemoryStream(_bestSnapshot);
                Checkpoint.Load(ms, _weights);
            }
        }

        private void SaveLast()
        {
            var extra = new Dictionary<string, long>
            {
                ["step"] = _step,
                ["epoch"] = _epoch,
                ["batch"] = _batch,
                ["evals"] = _evals,
                ["sinceBest"] = _sinceBest,
                ["bestStep"] = _bestStep,
                ["bestScore"] = BitConverter.DoubleToInt64Bits(_bestScore),
                ["hasBest"] = _hasBest ? 1 : 0,
                ["done"] = _done ? 1 : 0,
                ["stoppedEarly"] = _stoppedEarly ? 1 : 0,
                ["seed"] = unchecked((long)_o.Seed),
                ["trainCount"] = _train.Count,
                ["batchWords"] = _o.BatchWords,
            };
            Checkpoint.Save(Path.Combine(_o.CheckpointDirectory!, LastName), _state, _optimizer, extra);
        }

        private void TryResume()
        {
            string last = Path.Combine(_o.CheckpointDirectory!, LastName);
            if (!File.Exists(last))
            {
                return;
            }

            IReadOnlyDictionary<string, long> x = Checkpoint.Load(last, _state, _optimizer).Extra;
            if (x["seed"] != unchecked((long)_o.Seed) || x["trainCount"] != _train.Count || x["batchWords"] != _o.BatchWords)
            {
                throw new InvalidOperationException($"The checkpoint in '{_o.CheckpointDirectory}' was made with a different seed, batch size or training set. Delete it or use another directory.");
            }

            _step = x["step"];
            _epoch = (int)x["epoch"];
            _batch = (int)x["batch"];
            _evals = (int)x["evals"];
            _sinceBest = (int)x["sinceBest"];
            _bestStep = x["bestStep"];
            _bestScore = BitConverter.Int64BitsToDouble(x["bestScore"]);
            _hasBest = x["hasBest"] != 0;
            _done = x["done"] != 0;
            _stoppedEarly = x["stoppedEarly"] != 0;
            if (_hasBest)
            {
                _bestOnDisk = true;
            }

            if (_done)
            {
                SetMode(training: false);
                RestoreBestWeights();
            }
        }

        private TrainResult Result(bool completed)
        {
            string hash = CheckpointHash.Of(_weights);
            return new TrainResult(_step, _epoch, completed, _stoppedEarly, _evals, _bestScore, _bestStep, hash);
        }
    }
}
