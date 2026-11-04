namespace RecursiveAI.OneNlp;

/// <summary>
/// A component that can say which settings recreate it. A saved pipeline writes <see cref="Settings"/> next to the component's name
/// and rebuilds the component with <see cref="ComponentRegistry.Create"/> from them.
/// </summary>
public interface IConfigurableComponent : IPipelineComponent
{
    /// <summary>The string settings that the component's registered factory reads to build an equal component.</summary>
    IReadOnlyDictionary<string, string> Settings { get; }
}
