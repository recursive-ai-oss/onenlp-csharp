namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>
/// The seam between encoders and heads: maps the tokens of a document to one vector each. Every head reads only this, so the fast
/// (hash-embedding CNN) and accurate (transformer) tiers, and encoders a user supplies, are interchangeable.
/// </summary>
/// <remarks>This is the first shape of the contract. It will change before the package's first stable release.</remarks>
public interface IEncoder
{
    /// <summary>The length of each token's vector.</summary>
    int Width { get; }

    /// <summary>Encodes a document's tokens.</summary>
    /// <param name="doc">The document.</param>
    /// <param name="destination">Receives <c>doc.Length * Width</c> values, one row per token in token order; it holds at least that many.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is too short.</exception>
    void Encode(Doc doc, Span<float> destination);
}
