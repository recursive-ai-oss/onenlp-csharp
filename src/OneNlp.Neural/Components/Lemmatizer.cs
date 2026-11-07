namespace RecursiveAI.OneNlp.Neural.Components;

/// <summary>
/// A trained component that sets <see cref="Token.Lemma"/>, in spaCy's term for the role. The one implementation is the
/// <see cref="EditTreeLemmatizer"/>; ask for this type with <c>Pipeline.Add&lt;Lemmatizer&gt;()</c> to take whichever lemmatizer a model package carries.
/// </summary>
public abstract class Lemmatizer : TrainableHead
{
    /// <summary>Creates the base.</summary>
    /// <param name="name">The component name, unique in its pipeline.</param>
    /// <param name="encoder">The encoder the head reads.</param>
    protected Lemmatizer(string name, Encoders.IEncoder encoder)
        : base(name, encoder)
    {
    }
}

/// <summary>
/// A trained component that sets heads and dependency relations (<see cref="Token.Head"/>, <see cref="Token.Dep"/>), in spaCy's term
/// for the role. The one implementation is the biaffine <see cref="Parser"/>; ask for this type with <c>Pipeline.Add&lt;DependencyParser&gt;()</c>.
/// </summary>
public abstract class DependencyParser : TrainableHead
{
    /// <summary>Creates the base.</summary>
    /// <param name="name">The component name, unique in its pipeline.</param>
    /// <param name="encoder">The encoder the head reads.</param>
    protected DependencyParser(string name, Encoders.IEncoder encoder)
        : base(name, encoder)
    {
    }
}
