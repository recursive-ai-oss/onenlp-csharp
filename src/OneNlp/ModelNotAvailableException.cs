namespace RecursiveAI.OneNlp;

/// <summary>
/// The error for a verb or pipeline that needs a trained model no referenced package provides. It derives from
/// <see cref="InvalidOperationException"/>, so code that caught that type keeps working; a host can read which language and packages
/// are missing instead of parsing the message.
/// </summary>
public sealed class ModelNotAvailableException : InvalidOperationException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is missing and which package to add.</param>
    /// <param name="language">The language the model was needed for; <see cref="Language.Unknown"/> when it is not tied to one (open-type entities, embeddings).</param>
    /// <param name="tier">The tier that was asked for.</param>
    /// <param name="packageIds">The NuGet package ids, any one of which provides the model.</param>
    public ModelNotAvailableException(string message, Language language, Tier tier, IReadOnlyList<string> packageIds)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(packageIds);
        Language = language;
        Tier = tier;
        PackageIds = packageIds;
    }

    /// <summary>The language the model was needed for; <see cref="Language.Unknown"/> when it is not tied to one.</summary>
    public Language Language { get; }

    /// <summary>The tier that was asked for.</summary>
    public Tier Tier { get; }

    /// <summary>The NuGet package ids, any one of which provides the model. Every id names a package that exists.</summary>
    public IReadOnlyList<string> PackageIds { get; }
}
