namespace RecursiveAI.OneNlp.Neural.Encoders;

/// <summary>An encoder whose input is the word forms of multiword tokens (<c>Token.Norm</c> of their pieces) rather than the surface pieces.</summary>
internal interface IWordFormEncoder
{
    /// <summary>Whether the encoder reads <c>Norm</c> where a multiword token's pieces have it, so heads that look at the form should too.</summary>
    bool ReadsWordForms { get; }
}
