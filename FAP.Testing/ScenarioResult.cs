namespace FAP.Testing;

public sealed class ScenarioResult
{
	public required int TotalRequests { get; init; }
	public required int SuccessfulRequests { get; init; }
	public required int FailedRequests { get; init; }
	public required TimeSpan Elapsed { get; init; }

	public double SuccessRate => TotalRequests == 0 ? 0 : (double)SuccessfulRequests / TotalRequests;
}

