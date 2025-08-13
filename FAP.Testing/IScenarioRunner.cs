using System.Threading;
using System.Threading.Tasks;

namespace FAP.Testing;

public interface IScenarioRunner
{
	Task<ScenarioResult> RunAsync(ScenarioOptions options, CancellationToken cancellationToken);

	Task<(bool ok, string secret, string sourceId)> ConnectAsync(string serverAddress, TimeSpan timeout, CancellationToken cancellationToken);
}

