# Connection Management

## Overview

Connection management in FAP handles the establishment, maintenance, and termination of connections between clients and overlords. It provides automatic connection discovery, retry logic, and fault tolerance to ensure reliable network communication.

## Connection States

### ConnectionState Enum
```csharp
public enum ConnectionState
{
    Disconnected,    // Not connected to any overlord
    Connecting,      // Attempting to establish connection
    Connected        // Successfully connected to overlord
}
```

### State Transitions
```
Disconnected → Connecting → Connected
     ↑                              ↓
     └─── Connection Lost ←─────────┘
```

## Connection Controller

### Purpose
The `ConnectionController` manages the automatic connection lifecycle for clients.

### Key Responsibilities
- **Peer Discovery**: Monitor available overlords
- **Connection Attempts**: Try to connect to discovered overlords
- **State Management**: Track connection status
- **Retry Logic**: Handle connection failures gracefully

### Implementation
```csharp
public class ConnectionController
{
    private readonly Model model;
    private readonly MulticastServerService mserver;
    private readonly LANPeerFinderService peerFinder;
    private readonly BackgroundSafeObservable<LanPeer> attemptedPeers;
    private bool run = true;
    
    public void Start()
    {
        peerFinder.Start();
        ThreadPool.QueueUserWorkItem(ProcessLanConnection);
    }
}
```

## Connection Process

### 1. Discovery Phase
```csharp
private void ProcessLanConnection(object o)
{
    // Send WHO request to trigger immediate announcements
    mserver.SendMessage(WhoVerb.CreateRequest());
    
    while (run)
    {
        if (model.Network.State != ConnectionState.Connected)
        {
            // Build prioritized server list
            var availableNodes = new List<DetectedNode>();
            List<DetectedNode> detectedPeers = peerFinder.Peers.ToList();
            
            // Prioritize servers we haven't tried
            foreach (DetectedNode peer in detectedPeers)
            {
                if (attemptedPeers.Where(s => s.Node == peer).Count() == 0)
                    availableNodes.Add(peer);
            }
            
            // Try connections
            while (model.Network.State != ConnectionState.Connected && 
                   availableNodes.Count > 0)
            {
                DetectedNode node = availableNodes[0];
                availableNodes.RemoveAt(0);
                if (!Connect(model.Network, node))
                    peerFinder.RemovePeer(node);
            }
        }
    }
}
```

### 2. Connection Attempt
```csharp
private bool Connect(Domain.Entities.Network net, DetectedNode n)
{
    try
    {
        // logger.LogInformation("Client connecting to {Address}", n.Address);
        net.State = ConnectionState.Connecting;

        var verb = new ConnectVerb();
        verb.ClientType = ClientType.Client;
        verb.Address = model.LocalNode.Location;
        verb.Secret = IDService.CreateID();
        
        // Modern HTTP client for verbs
        var client = new ModernHttpClient(model.LocalNode);
        
        net.Overlord = new Node();
        net.Overlord.Location = n.Address;
        net.Overlord.Secret = verb.Secret;
        
        if (client.Execute(verb, n.Address))
        {
            net.State = ConnectionState.Connected;
            net.Overlord.ID = verb.OverlordID;
            // logger.LogInformation("Client connected");
            return true;
        }
        else
        {
            net.Overlord = new Node();
        }
    }
    catch
    {
        net.State = ConnectionState.Disconnected;
    }
    return false;
}
```

### 3. Connection Validation
```csharp
// Validate connection parameters
if (string.IsNullOrEmpty(iv.Secret))
{
    // Don't allow connections with no secret
    return false;
}

// Don't allow connections to ourselves
if (iv.Address == serverNode.Location)
    return false;

// Only allow one connect attempt at once
lock (sync)
{
    if (connectingIDs.Contains(address))
        return false;
    connectingIDs.Add(address);
}
```

## Connection Types

### 1. Client to Overlord
**Purpose**: Standard client connection to overlord server

**Process**:
1. Client discovers overlord via multicast
2. Client sends CONNECT verb to overlord
3. Overlord validates client credentials
4. Overlord accepts connection and returns auth key
5. Client establishes persistent connection

**Authentication**:
```csharp
var verb = new ConnectVerb();
verb.ClientType = ClientType.Client;
verb.Address = model.LocalNode.Location;
verb.Secret = IDService.CreateID();  // Generate unique secret
```

### 2. Overlord to Overlord
**Purpose**: Inter-overlord communication for message routing

**Process**:
1. Overlord A discovers Overlord B via multicast
2. Overlord A connects to Overlord B as client
3. Both overlords establish bidirectional communication
4. Messages can be routed between overlords

**Implementation**:
```csharp
var uplink = new Uplink(model.LocalNode, new Node
{
    ID = peer.OverlordID,
    Location = peer.Address,
    NodeType = ClientType.Overlord,
    Secret = verb.Secret
});
extOverlordServers.Add(uplink);
```

## Connection Lifecycle

### 1. Initialization
```
1. Start peer discovery
2. Send WHO request
3. Listen for overlord announcements
4. Build available overlord list
```

### 2. Connection Attempt
```
1. Select best available overlord
2. Generate connection secret
3. Send CONNECT request
4. Validate response
5. Establish persistent connection
```

### 3. Maintenance
```
1. Monitor connection health
2. Send periodic keep-alive messages
3. Handle connection timeouts
4. Reconnect on failure
```

### 4. Termination
```
1. Send disconnect notification
2. Clean up connection resources
3. Remove from overlord client list
4. Update connection state
```

## Retry Logic

### Exponential Backoff
```csharp
private void LaunchOverlordWithDelay(object o)
{
    try
    {
        var r = new Random();
        int delay = 0;
        switch (model.OverlordPriority)
        {
            case OverlordPriority.High:
                delay = r.Next(2000, 3000);
                break;
            case OverlordPriority.Normal:
                delay = r.Next(3000, 5000);
                break;
            case OverlordPriority.Low:
                delay = r.Next(5000, 8000);
                break;
        }
        Thread.Sleep(delay);
        
        if (IsNewServerNeeded())
            Start();
    }
    finally
    {
        serverLaunching = false;
    }
}
```

### Connection Prioritization
```csharp
// Build prioritized server list
var availableNodes = new List<DetectedNode>();

// Prioritize servers we haven't tried
foreach (DetectedNode peer in detectedPeers)
{
    if (attemptedPeers.Where(s => s.Node == peer).Count() == 0)
        availableNodes.Add(peer);
}

// Add previously attempted servers (retry)
foreach (LanPeer peer in attemptedPeers.OrderByDescending(x => x.LastConnectionTime))
{
    availableNodes.Add(peer.Node);
}
```

## Connection Monitoring

### Health Checks
```csharp
// Monitor connection health
if (DateTime.Now - lastHealthCheck > TimeSpan.FromMinutes(5))
{
    // Send NOOP to verify connection
    var verb = new NoopVerb();
    var client = new ModernHttpClient(model.LocalNode);
    if (!client.Execute(verb, model.Network.Overlord))
    {
        // Connection lost, trigger reconnection
        model.Network.State = ConnectionState.Disconnected;
    }
    lastHealthCheck = DateTime.Now;
}
```

### Connection Metrics
- **Connection Duration**: How long connection has been active
- **Message Throughput**: Messages per second
- **Response Times**: Round-trip time for requests
- **Error Rates**: Failed requests per time period

## Fault Tolerance

### Automatic Reconnection
```csharp
public void Disconnect()
{
    // Notify log off
    if (model.Network.State == ConnectionState.Connected)
    {
        var c = new Client(model.LocalNode);
        var verb = new UpdateVerb();
        verb.Nodes.Add(new Node { ID = model.LocalNode.ID, Online = false });
        c.Execute(verb, model.Network.Overlord, 3000);

        // Remove peer so we don't reconnect immediately
        DetectedNode peer = peerFinder.Peers
            .Where(p => p.Address == model.Network.Overlord.Location)
            .FirstOrDefault();
        if (null != peer)
            peerFinder.RemovePeer(peer);
        model.Network.State = ConnectionState.Disconnected;
    }
}
```

### Network Partition Handling
- **Isolated Segments**: Form separate overlord networks
- **Automatic Recovery**: Reconnect when network restored
- **Graceful Degradation**: Continue operation with reduced capacity

## Message Routing

### Client Message Flow
```csharp
private void SendMessageAsync(object o)
{
    try
    {
        if (model.Network.State == ConnectionState.Connected)
        {
            var client = new Client(model.LocalNode);
            if (!client.Execute((NetworkRequest)o, model.Network.Overlord))
            {
                if (model.Network.State == ConnectionState.Connected)
                    model.Network.State = ConnectionState.Disconnected;
            }
        }
        else
        {
            logger.LogWarning("Could not send message as you are not connected");
        }
    }
    catch (Exception e)
    {
        logger.LogError(e, "Failed to send chat message");
    }
}
```

### Overlord Message Routing
```csharp
// Route to standard clients
private void SendToStandardClients(NetworkRequest r)
{
    foreach (ClientStream peer in connectedClientNodes
        .Where(c => c.Node.NodeType == ClientType.Client))
        peer.AddMessage(r);
}

// Route to overlord clients
private void SendToOverlordClients(NetworkRequest r)
{
    foreach (ClientStream peer in connectedClientNodes
        .Where(c => c.Node.NodeType == ClientType.Overlord))
        peer.AddMessage(r);
}

// Route to external overlords
private void SendToOverlordServers(NetworkRequest r)
{
    foreach (Uplink peer in extOverlordServers)
        peer.AddMessage(r);
}
```

## Performance Optimization

### Connection Pooling
- **HTTP Keep-Alive**: Reuse connections for multiple requests
- **Connection Limits**: Prevent resource exhaustion
- **Timeout Management**: Efficient timeout handling

### Load Balancing
- **Overlord Selection**: Choose overlord with available capacity
- **Connection Distribution**: Spread clients across overlords
- **Capacity Monitoring**: Track overlord utilization

## Security Considerations

### Authentication
- **Secret Validation**: Verify connection secrets
- **Node Verification**: Validate peer identity
- **Connection Limits**: Prevent resource exhaustion

### Network Security
- **LAN-Only**: Designed for trusted networks
- **No Encryption**: Relies on network-level security
- **Simple Protocol**: Minimal attack surface

## Configuration

### Connection Settings
```csharp
// Connection timeouts
public static int UPLINK_TIMEOUT = 60000;        // 1 minute
public static int DOWNLOAD_RETRY_TIME = 120000;  // 2 minutes

// Connection limits/timeouts are configured via ASP.NET Core Kestrel (appsettings.json)
```

### Retry Configuration
- **Initial Delay**: 2-8 seconds (priority-based)
- **Max Retries**: 3 attempts per overlord
- **Backoff Strategy**: Exponential backoff
- **Timeout Values**: 30-second default timeouts

## Monitoring and Debugging

### Connection Logging
```csharp
// Log connection attempts (using Microsoft.Extensions.Logging.ILogger)
logger.LogInformation("Client connecting to {Address}", n.Address);

// Log successful connections
logger.LogInformation("Client connected");

// Log connection failures
logger.LogWarning("Connection failed to {Address}", n.Address);
```

### Connection Diagnostics
- **Connection State**: Monitor current connection status
- **Peer Discovery**: Track discovered overlords
- **Connection Attempts**: Log connection attempts and results
- **Performance Metrics**: Monitor connection performance

## Best Practices

### Connection Management
1. **Graceful Handling**: Handle connection failures gracefully
2. **Retry Logic**: Implement appropriate retry strategies
3. **Resource Cleanup**: Properly clean up connection resources
4. **Monitoring**: Monitor connection health and performance

### Network Design
1. **Overlord Distribution**: Distribute overlords across network
2. **Capacity Planning**: Ensure sufficient overlord capacity
3. **Network Monitoring**: Monitor network connectivity
4. **Fault Tolerance**: Plan for network failures

### Troubleshooting
1. **Connection Failures**: Check network connectivity and overlord availability
2. **Performance Issues**: Monitor connection performance and capacity
3. **Authentication Problems**: Verify secret generation and validation
4. **Routing Issues**: Check message routing and overlord interconnections 