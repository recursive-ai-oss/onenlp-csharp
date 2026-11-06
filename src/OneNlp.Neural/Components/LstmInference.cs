using System.Buffers;
using RecursiveAI.OneModel.Tensors;
using RecursiveAI.OneModel.Training;
using RecursiveAI.OneNlp.Neural.Encoders;

namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A bidirectional, multi-layer LSTM run without tensor operations, for prediction: the weights of a <see cref="Recurrent"/> module are copied once into the
/// layout <see cref="RowKernels.Dense"/> wants (transposed, [inputs, 4 x hidden]) and each document's rows are walked step by step, so a step costs two
/// matrix-vector products and a gate update where the tensor graph spends many small operations. The arithmetic is that of <c>torch.nn.LSTM</c> (gate order
/// input, forget, cell, output; zero initial states; no dropout between layers), with the sigmoid and tanh of <see cref="RowKernels"/>.
/// </summary>
/// <remarks>
/// A document's rows are one sequence in the forward direction and, read from its last row back, one in the reverse direction, so a result never depends on
/// the other documents of a batch. The input projection of a block of rows is computed before the steps that need it, which keeps the scratch buffer to a fixed
/// size however long the document is.
/// </remarks>
internal sealed class LstmInference
{
    private const int Block = 128;

    private readonly Cell[] _cells; // layer-major: [layer * 2 + direction]
    private readonly int _hidden;
    private readonly int _layers;

    private LstmInference(Cell[] cells, int hidden, int layers, long stamp)
    {
        _cells = cells;
        _hidden = hidden;
        _layers = layers;
        Stamp = stamp;
    }

    public long Stamp { get; }

    /// <summary>The width of the output rows: both directions of the last layer.</summary>
    public int OutputWidth => 2 * _hidden;

    /// <summary>
    /// Builds the inference copy from a module's named parameters (<c>weight_ih_l0</c>, <c>weight_hh_l0</c>, <c>bias_ih_l0</c>, <c>bias_hh_l0</c> and the
    /// <c>_reverse</c> versions, layer by layer).
    /// </summary>
    public static LstmInference Create(IReadOnlyList<(string Name, Tensor Tensor)> parameters, int hidden, int layers, long stamp)
    {
        var byName = new Dictionary<string, Tensor>(StringComparer.Ordinal);
        foreach ((string name, Tensor tensor) in parameters)
        {
            byName[name] = tensor;
        }

        var cells = new Cell[layers * 2];
        for (int l = 0; l < layers; l++)
        {
            for (int d = 0; d < 2; d++)
            {
                string suffix = "_l" + l.ToString(System.Globalization.CultureInfo.InvariantCulture) + (d == 1 ? "_reverse" : string.Empty);
                cells[(l * 2) + d] = new Cell(byName["weight_ih" + suffix], byName["weight_hh" + suffix], byName["bias_ih" + suffix], byName["bias_hh" + suffix], hidden);
            }
        }

        return new LstmInference(cells, hidden, layers, stamp);
    }

    /// <summary>
    /// Runs the stack over <paramref name="x"/> ([rows, input]) whose rows are cut into documents by <paramref name="starts"/> and <paramref name="lengths"/>,
    /// and returns the last layer's hidden states [rows, 2 x hidden] (forward half first), owned by the caller.
    /// </summary>
    public float[] Run(ReadOnlySpan<float> x, int rows, ReadOnlySpan<int> starts, ReadOnlySpan<int> lengths)
    {
        int h = _hidden;
        float[] current = x.ToArray();
        int inputWidth = _cells[0].Input;
        float[] output = Array.Empty<float>();
        float[] gates = ArrayPool<float>.Shared.Rent(Block * 4 * h);
        float[] state = ArrayPool<float>.Shared.Rent(2 * h);
        float[] pre = ArrayPool<float>.Shared.Rent(4 * h);
        try
        {
            for (int l = 0; l < _layers; l++)
            {
                output = new float[rows * 2 * h];
                for (int d = 0; d < starts.Length; d++)
                {
                    for (int direction = 0; direction < 2; direction++)
                    {
                        RunSequence(_cells[(l * 2) + direction], current, inputWidth, starts[d], lengths[d], direction == 1, output, direction * h, gates, pre, state);
                    }
                }

                current = output;
                inputWidth = 2 * h;
            }

            return output;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(gates);
            ArrayPool<float>.Shared.Return(state);
            ArrayPool<float>.Shared.Return(pre);
        }
    }

    // One document, one direction: rows start..start+length of input; the hidden state of each row goes to output[row * 2h + column ..].
    private void RunSequence(Cell cell, float[] input, int inputWidth, int start, int length, bool reverse, float[] output, int column, float[] gates, float[] pre, float[] state)
    {
        int h = _hidden;
        int stride = 4 * h;
        Span<float> hidden = state.AsSpan(0, h);
        Span<float> cellState = state.AsSpan(h, h);
        hidden.Clear();
        cellState.Clear();
        for (int done = 0; done < length; done += Block)
        {
            int count = Math.Min(Block, length - done);

            // Rows of this block in the order the direction reads them.
            int first = reverse ? length - done - count : done;
            for (int k = 0; k < count; k++)
            {
                int row = start + first + k;
                cell.InputProjection(input.AsSpan(row * inputWidth, inputWidth), gates.AsSpan(k * stride, stride));
            }

            for (int step = 0; step < count; step++)
            {
                int k = reverse ? count - 1 - step : step;
                int row = start + first + k;
                cell.Recurrent(hidden, gates.AsSpan(k * stride, stride), pre.AsSpan(0, stride));
                RowKernels.LstmStep(pre, cellState, hidden);
                hidden.CopyTo(output.AsSpan((row * 2 * h) + column, h));
            }
        }
    }

    // The weights of one layer and direction in the layout of RowKernels.Dense.
    private sealed class Cell
    {
        private readonly float[] _inputT; // [input, 4h]
        private readonly float[] _hiddenT; // [h, 4h]
        private readonly float[] _inputBias; // bias_ih + bias_hh

        public Cell(Tensor weightIh, Tensor weightHh, Tensor biasIh, Tensor biasHh, int hidden)
        {
            int rows = 4 * hidden;
            Input = weightIh.Shape[1];
            _inputT = Transpose(weightIh.Data<float>(), rows, Input);
            _hiddenT = Transpose(weightHh.Data<float>(), rows, hidden);
            ReadOnlySpan<float> bi = biasIh.Data<float>();
            ReadOnlySpan<float> bh = biasHh.Data<float>();
            _inputBias = new float[rows];
            for (int i = 0; i < rows; i++)
            {
                _inputBias[i] = bi[i] + bh[i];
            }
        }

        public int Input { get; }

        // gates = x W_ih^T + b_ih + b_hh
        public void InputProjection(ReadOnlySpan<float> x, Span<float> gates) => RowKernels.Dense(x, _inputT, gates.Length, _inputBias, gates);

        // pre = h W_hh^T + (input projection of this step)
        public void Recurrent(ReadOnlySpan<float> h, ReadOnlySpan<float> projection, Span<float> pre) => RowKernels.Dense(h, _hiddenT, pre.Length, projection, pre);

        private static float[] Transpose(ReadOnlySpan<float> w, int rows, int columns)
        {
            var t = new float[rows * columns];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    t[(c * rows) + r] = w[(r * columns) + c];
                }
            }

            return t;
        }
    }
}

/// <summary>An inference copy of a bias-free linear layer that maps a vector to many classes: the weights transposed for <see cref="RowKernels.Dense"/>.</summary>
internal sealed class BilinearInference
{
    private readonly float[] _weightsT;
    private readonly float[] _zero;

    public BilinearInference(Tensor weight, long stamp)
    {
        Stamp = stamp;
        int outputs = weight.Shape[0];
        int inputs = weight.Shape[1];
        Stride = (outputs + 3) & ~3;
        _weightsT = new float[inputs * Stride];
        _zero = new float[Stride];
        ReadOnlySpan<float> w = weight.Data<float>();
        for (int o = 0; o < outputs; o++)
        {
            for (int i = 0; i < inputs; i++)
            {
                _weightsT[(i * Stride) + o] = w[(o * inputs) + i];
            }
        }
    }

    public long Stamp { get; }

    public int Stride { get; }

    /// <summary>The products of one input vector with every output row, into <paramref name="products"/> (at least <see cref="Stride"/> long).</summary>
    public void Products(ReadOnlySpan<float> x, Span<float> products) => RowKernels.Dense(x, _weightsT, Stride, _zero, products);
}
