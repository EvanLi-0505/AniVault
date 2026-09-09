using AniVault.Services;

namespace AniVault.Tests;

/// <summary>In-memory <see cref="IBootstrapConfigService"/> for tests — no file access.</summary>
public sealed class FakeBootstrapConfigService : IBootstrapConfigService
{
    private BootstrapConfig _config = new();

    public string ConfigFilePath => "(in-memory)";

    public BootstrapConfig Load() => new() { DataDirectory = _config.DataDirectory };

    public void Save(BootstrapConfig config) => _config = new BootstrapConfig { DataDirectory = config.DataDirectory };
}
