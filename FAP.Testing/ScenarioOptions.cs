namespace FAP.Testing;

public sealed class ScenarioOptions
{
	public required string ServerAddress { get; init; }
	public int Clients { get; init; } = 5;
	public int Parallelism { get; init; } = 3;
	public int IterationsPerClient { get; init; } = 2;
	public int TimeoutSeconds { get; init; } = 10;
	public VerbMix VerbMix { get; init; } = VerbMix.Hello | VerbMix.Who | VerbMix.Browse | VerbMix.Chat;
	public double FailureThresholdPercent { get; init; } = 0.0; // 0% failures by default
}

[System.Flags]
public enum VerbMix
{
	None = 0,
	Hello = 1 << 0,
	Who = 1 << 1,
	Browse = 1 << 2,
	Chat = 1 << 3,
}

