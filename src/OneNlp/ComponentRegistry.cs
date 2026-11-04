using System.Collections.Concurrent;

namespace RecursiveAI.OneNlp;

/// <summary>Creates a component from named settings. Registered with <see cref="ComponentRegistry"/>.</summary>
/// <param name="settings">String settings from the caller; empty when there are none. A factory documents the keys it reads.</param>
/// <returns>A new component.</returns>
public delegate IPipelineComponent ComponentFactory(IReadOnlyDictionary<string, string> settings);

/// <summary>
/// The table of component factories by name, so a pipeline can be built from names (<c>Add("norm")</c>) the way spaCy's
/// <c>add_pipe</c> does. It is filled by explicit calls, never by scanning assemblies, so it works under NativeAOT and trimming.
/// </summary>
/// <remarks>
/// Components of this package are registered when the registry is first used. A package that adds components registers them from
/// a module initializer (<c>[ModuleInitializer]</c>), which runs when the package's assembly is first used, or from a public
/// <c>Register</c> method the application calls at startup. All members are thread-safe.
/// </remarks>
public static class ComponentRegistry
{
    private static readonly IReadOnlyDictionary<string, string> NoSettings = new Dictionary<string, string>();
    private static readonly ConcurrentDictionary<string, ComponentFactory> Factories = CreateWithBuiltIns();

    /// <summary>The registered names, in no particular order.</summary>
    public static IReadOnlyCollection<string> Names => [.. Factories.Keys];

    /// <summary>Registers a factory under a name.</summary>
    /// <param name="name">The component name, unique across the application; case-sensitive.</param>
    /// <param name="factory">The factory.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">A factory is already registered under the name.</exception>
    public static void Register(string name, ComponentFactory factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(factory);
        if (!Factories.TryAdd(name, factory))
        {
            throw new InvalidOperationException($"A component named '{name}' is already registered.");
        }
    }

    /// <summary>Creates a registered component.</summary>
    /// <param name="name">The registered name.</param>
    /// <param name="settings">Settings for the factory, or <see langword="null"/> for none.</param>
    /// <exception cref="InvalidOperationException">No component has this name. The message lists the registered names and says that components from other packages register when that package is loaded.</exception>
    public static IPipelineComponent Create(string name, IReadOnlyDictionary<string, string>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!Factories.TryGetValue(name, out ComponentFactory? factory))
        {
            throw new InvalidOperationException(
                $"No pipeline component named '{name}' is registered (registered: {string.Join(", ", Factories.Keys.Order(StringComparer.Ordinal))}). "
                + "Components that live in another package, such as RecursiveAI.OneNlp.Neural, are registered once that package is loaded.");
        }

        return factory(settings ?? NoSettings);
    }

    private static ConcurrentDictionary<string, ComponentFactory> CreateWithBuiltIns()
    {
        var table = new ConcurrentDictionary<string, ComponentFactory>(StringComparer.Ordinal)
        {
            [NormComponent.ComponentName] = _ => new NormComponent(),
            // Setting "language" (a language code, default none) picks the segmentation data.
            [Segmentation.SentencizerComponent.ComponentName] = settings => Segmentation.SentencizerComponent.For(
                settings.TryGetValue("language", out string? code) ? Language.FromCode(code) : Language.Unknown),
        };
        return table;
    }
}
