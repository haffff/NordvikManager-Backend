using System.Collections.Generic;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Opt-in for steps that must see some arguments raw. ActionProcessingService normally
    /// substitutes %var% tokens in every argument before Execute; arguments named here are
    /// left untouched so the step can resolve them itself (e.g. once per collection item).
    /// </summary>
    public interface IDeferredArgumentsStep
    {
        IReadOnlyCollection<string> DeferredArguments { get; }
    }
}
