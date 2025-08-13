using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FAP.Domain;
using FAP.Domain.Entities;
using FAP.Domain.Net;
using FAP.Domain.Verbs;
using FAP.Domain.Verbs.Multicast;
using Fap.Foundation.Services;

namespace FAP.Testing;

public sealed class DefaultScenarioRunner : IScenarioRunner
{
	public async Task<ScenarioResult> RunAsync(ScenarioOptions options, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(options);
		if (string.IsNullOrWhiteSpace(options.ServerAddress)) throw new ArgumentException("ServerAddress is required", nameof(options));

		var total = 0;
		var ok = 0;
		var fail = 0;
		var sw = System.Diagnostics.Stopwatch.StartNew();

		var throttler = new SemaphoreSlim(options.Parallelism, options.Parallelism);
		var tasks = new List<Task>();
		var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		for (int c = 0; c < options.Clients; c++)
		{
			for (int i = 0; i < options.IterationsPerClient; i++)
			{
				await throttler.WaitAsync(cts.Token).ConfigureAwait(false);
				var t = Task.Run(async () =>
				{
					try
					{
						Interlocked.Increment(ref total);
						var client = new Client(new Node());
						bool stepOk = true;
						string target = options.ServerAddress;
						if (Uri.TryCreate(options.ServerAddress, UriKind.Absolute, out var uri))
						{
							target = uri.Host + ":" + (uri.IsDefaultPort ? 40 : uri.Port);
						}
						var serverNode = new Node { Location = target };
						var timeout = options.TimeoutSeconds * 1000;

						// Use INFO first to validate reachability
						// Prefer GET for INFO to avoid any POST body handling differences
						var okInfo = client.Execute(new FAP.Shared.Entities.NetworkRequest { Verb = "INFO", Data = string.Empty }, serverNode, timeout);
						stepOk &= okInfo;
						if (!okInfo) System.Console.WriteLine($"Scenario step failed: INFO -> {target}");

						if ((options.VerbMix & VerbMix.Hello) != 0)
						{
							var hello = new HelloVerb();
							var req = new FAP.Shared.Entities.NetworkRequest { Verb = "HELLO", Data = hello.CreateRequest(options.ServerAddress, Environment.MachineName, string.Empty, string.Empty, 0, 0, 0) };
							var okHello = client.Execute(req, serverNode, timeout);
							stepOk &= okHello;
							if (!okHello) System.Console.WriteLine($"Scenario step failed: HELLO -> {target}");
						}
						if ((options.VerbMix & VerbMix.Who) != 0)
						{
							var who = new WhoVerb();
							var req = new FAP.Shared.Entities.NetworkRequest { Verb = "WHO", Data = WhoVerb.CreateRequest() };
							var okWho = client.Execute(req, serverNode, timeout);
							stepOk &= okWho;
							if (!okWho) System.Console.WriteLine($"Scenario step failed: WHO -> {target}");
						}
						if ((options.VerbMix & VerbMix.Browse) != 0)
						{
							var browse = new BrowseVerb { Path = "/" };
							// The server expects BROWSE only on Fap.app path via proper handler; ensure Browse works
							var okBrowse = client.Execute(browse, serverNode, timeout);
							stepOk &= okBrowse;
							if (!okBrowse) System.Console.WriteLine($"Scenario step failed: BROWSE -> {target}");
						}
						if ((options.VerbMix & VerbMix.Chat) != 0)
						{
							var chat = new ChatVerb { Message = "ping" };
							var okChat = client.Execute(chat, serverNode, timeout);
							stepOk &= okChat;
							if (!okChat) System.Console.WriteLine($"Scenario step failed: CHAT -> {target}");
						}

						if (stepOk) Interlocked.Increment(ref ok); else Interlocked.Increment(ref fail);
					}
					catch
					{
						Interlocked.Increment(ref fail);
					}
					finally
					{
						throttler.Release();
					}
				}, cts.Token);
				tasks.Add(t);
			}
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
		sw.Stop();

		return new ScenarioResult
		{
			TotalRequests = total,
			SuccessfulRequests = ok,
			FailedRequests = fail,
			Elapsed = sw.Elapsed
		};
	}

	public async Task<(bool ok, string secret, string sourceId)> ConnectAsync(string serverAddress, TimeSpan timeout, CancellationToken cancellationToken)
	{
		string target = serverAddress;
		if (Uri.TryCreate(serverAddress, UriKind.Absolute, out var uri))
		{
			target = uri.Host + ":" + (uri.IsDefaultPort ? 40 : uri.Port);
		}
        var clientNode = new Node();
        clientNode.ID = IDService.CreateID();
		var serverNode = new Node { Location = target };
		var connect = new ConnectVerb
		{
			Address = target,
            ClientType = ClientType.Client,
            Secret = IDService.CreateID()
		};
		var client = new Client(clientNode);
		var ok = await Task.Run(() => client.Execute(connect, serverNode, (int)timeout.TotalMilliseconds), cancellationToken);
		if (!ok) return (false, string.Empty, string.Empty);
		return (true, connect.Secret, clientNode.ID);
	}
}

