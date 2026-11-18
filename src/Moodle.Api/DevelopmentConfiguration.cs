namespace Moodle.Api;

/// <summary>Explicit opt-in for synthetic dependencies; production cannot enable them.</summary>
public static class DevelopmentConfiguration
{
    public static bool Validate(IHostEnvironment environment, IConfiguration configuration)
    {
        var fakes = configuration.GetValue<bool>("Development:UseFakeIdentity");
        var seed = configuration.GetValue<bool>("Database:SeedAsg");
        var sandbox = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        if ((fakes || seed) && !sandbox)
            throw new InvalidOperationException("Synthetic identity and seed require Development or Testing.");
        if (seed && (!fakes || !configuration.GetValue<bool>("Database:Initialize")))
            throw new InvalidOperationException("ASG seed requires explicit fake identity and database initialization.");
        return fakes;
    }
}
