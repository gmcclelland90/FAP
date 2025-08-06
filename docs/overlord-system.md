# Overlord System

## Overview

The Overlord System is the core coordination mechanism of the File Acceleration Protocol (FAP). It implements an automatic server election and management system that ensures optimal network performance and fault tolerance.

## What is an Overlord?

An **Overlord** is a special type of node that acts as a coordinator for the FAP network. Overlords:

- **Coordinate Clients**: Manage connections from multiple client nodes
- **Route Messages**: Forward messages between clients and other overlords
- **Maintain Topology**: Keep track of network structure and peer status
- **Provide Redundancy**: Multiple overlords ensure network resilience

## Overlord Types

### 1. Dedicated Overlords
- **Purpose**: High-capacity servers designed for heavy workloads
- **Capacity**: 100 concurrent clients
- **Priority**: Always start when needed
- **Port**: 40 (default overlord port)
- **Use Case**: Large networks, high-traffic environments

### 2. Regular Overlords
- **Purpose**: Standard nodes that become overlords when needed
- **Capacity**: 40-50 concurrent clients (based on priority)
- **Priority**: Elected based on system capabilities
- **Port**: 40 (default overlord port)
- **Use Case**: Normal network operation

## Overlord Election Process

### Election Criteria

The system uses several factors to determine overlord eligibility:

```csharp
// Priority levels from FAP.Domain.Enums.OverlordPriority
public enum OverlordPriority
{
    Low,      // 40 clients max, port 40
    Normal,   // 50 clients max, port 40
    High      // 100 clients max, port 40
}
```

### Election Algorithm

1. **Discovery Phase**
   ```
   - Listen for existing overlords via multicast
   - Count available overlords and their capacity
   - Calculate total network capacity
   ```

2. **Assessment Phase**
   ```
   - Determine if additional overlords are needed
   - Check if existing overlords have sufficient free slots
   - Calculate optimal overlord count
   ```

3. **Election Phase**
   ```
   - Higher priority nodes start overlords first
   - Random delays prevent election conflicts
   - Priority-based startup delays:
     * High: 2-3 seconds
     * Normal: 3-5 seconds
     * Low: 5-8 seconds
   ```

### Election Logic

```csharp
private bool IsNewServerNeeded()
{
    // Count active overlords
    List<DetectedNode> localOverlords = 
        peerFinder.Peers.Where(p => 
            (DateTime.Now - p.LastAnnounce).TotalSeconds < 60).ToList();

    int overlords = localOverlords.Count;
    int serversWithFreeSlots = 0;
    int totalUsers = 0;
    int totalSlots = 0;

    // Calculate capacity
    foreach (DetectedNode overlord in localOverlords)
    {
        totalUsers += overlord.CurrentUsers;
        totalSlots += overlord.MaxUsers;
        if (overlord.MaxUsers - overlord.CurrentUsers > 5)
            serversWithFreeSlots++;
    }

    // Determine if new overlord needed
    return overlords == 0 || 
           (serversWithFreeSlots == 0 && totalUsers > totalSlots * 0.8);
}
```

## Overlord Management

### OverlordManagerService

The `OverlordManagerService` is responsible for managing overlord lifecycle:

```csharp
public class OverlordManagerService
{
    public void StartAndStopIfNeeded()
    {
        // Check if new overlord needed
        if (IsNewServerNeeded())
        {
            // Launch with priority-based delay
            ThreadPool.QueueUserWorkItem(LaunchOverlordWithDelay);
        }
    }
}
```

### Startup Sequence

1. **Initial Assessment**
   ```
   - Check for existing overlords
   - Calculate network capacity
   - Determine if local overlord needed
   ```

2. **Delayed Launch**
   ```
   - Apply priority-based delay
   - Re-check network state
   - Launch if still needed
   ```

3. **Service Initialization**
   ```
   - Start HTTP server
   - Initialize FAPServerHandler
   - Begin multicast announcements
   ```

## Overlord Communication

### Inter-Overlord Communication

Overlords communicate with each other to:

- **Share Client Lists**: Exchange information about connected clients
- **Route Messages**: Forward messages between overlords
- **Maintain Topology**: Keep network structure synchronized

```csharp
// Overlord-to-overlord connection
var uplink = new Uplink(model.LocalNode, new Node
{
    ID = peer.OverlordID,
    Location = peer.Address,
    NodeType = ClientType.Overlord,
    Secret = verb.Secret
});
```

### Client-Overlord Communication

Clients connect to overlords using the `CONNECT` verb:

```csharp
var verb = new ConnectVerb();
verb.ClientType = ClientType.Client;
verb.Address = model.LocalNode.Location;
verb.Secret = IDService.CreateID();
```

## Overlord Capacity Management

### Capacity Limits

| Priority | Max Clients | Use Case |
|----------|-------------|----------|
| Low | 40 | Light workloads |
| Normal | 50 | Standard operation |
| High | 100 | Heavy workloads |
| Dedicated | 100 | Server environments |

### Load Balancing

The system automatically distributes clients across available overlords:

1. **Client Connection**
   ```
   - Client discovers overlords via multicast
   - Attempts connection to available overlords
   - Connects to first successful overlord
   ```

2. **Overlord Selection**
   ```
   - Prioritize overlords with free capacity
   - Avoid overlords that have been attempted recently
   - Fall back to retry previously failed overlords
   ```

## Overlord State Management

### Connection Tracking

Overlords maintain several collections:

```csharp
// Connected client nodes
private readonly BackgroundSafeObservable<ClientStream> connectedClientNodes;

// External overlord servers
private readonly BackgroundSafeObservable<Uplink> extOverlordServers;

// External nodes (from other overlords)
private readonly BackgroundSafeObservable<Node> externalNodes;
```

### Message Routing

Overlords route messages to appropriate destinations:

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
```

## Fault Tolerance

### Automatic Recovery

1. **Overlord Failure**
   ```
   - Clients detect connection loss
   - Automatically reconnect to available overlords
   - New overlords start if needed
   ```

2. **Client Failure**
   ```
   - Overlords detect client disconnection
   - Remove client from active lists
   - Notify other clients of status change
   ```

3. **Network Partition**
   ```
   - Isolated segments form separate overlord networks
   - Automatic reconnection when network restored
   - Graceful merging of overlord networks
   ```

## Performance Optimization

### Announcement Frequency

Overlords announce themselves every 10 seconds:

```csharp
private void processAnnounce(object o)
{
    while (run)
    {
        // Create announcement with current stats
        multicastServer.SendMessage(verb.CreateRequest(
            serverNode.Location, 
            network.NetworkName, 
            serverNode.ID,
            network.NetworkID, 
            serverNode.Strength,
            connectedClientNodes.Count, 
            maxClients));
        
        // Wait 10 seconds before next announcement
        announcerSync.WaitOne(10000);
    }
}
```

### Connection Management

- **Connection Limits**: Prevent resource exhaustion
- **Timeout Handling**: Automatic cleanup of stale connections
- **Retry Logic**: Exponential backoff for failed connections
- **Load Distribution**: Even distribution across overlords

## Monitoring and Diagnostics

### Overlord Metrics

Key metrics tracked by overlords:

- **Connected Clients**: Number of active client connections
- **Message Throughput**: Messages processed per second
- **Network Latency**: Response times to client requests
- **Resource Usage**: CPU and memory utilization

### Health Checks

```csharp
// Regular health check
if (DateTime.Now - lastHealthCheck > TimeSpan.FromMinutes(5))
{
    // Perform health check
    CheckOverlordHealth();
    lastHealthCheck = DateTime.Now;
}
```

## Configuration

### Overlord Settings

```csharp
// Priority configuration
model.OverlordPriority = OverlordPriority.Normal;

// Capacity settings
switch (model.OverlordPriority)
{
    case OverlordPriority.High:
        maxClients = 100;
        break;
    case OverlordPriority.Normal:
        maxClients = 50;
        break;
    case OverlordPriority.Low:
        maxClients = 40;
        break;
}
```

### Network Settings

- **Announcement Interval**: 10 seconds
- **Health Check Interval**: 5 minutes
- **Connection Timeout**: 30 seconds
- **Retry Attempts**: 3 attempts with exponential backoff

## Best Practices

### Overlord Deployment

1. **Capacity Planning**
   - Estimate expected client count
   - Deploy overlords with appropriate priority
   - Monitor capacity utilization

2. **Network Design**
   - Distribute overlords across network segments
   - Ensure multicast connectivity
   - Plan for fault tolerance

3. **Monitoring**
   - Track overlord performance metrics
   - Monitor client distribution
   - Alert on capacity issues

### Troubleshooting

Common overlord issues and solutions:

1. **Overlord Not Starting**
   - Check multicast connectivity
   - Verify port availability
   - Review priority settings

2. **Client Connection Failures**
   - Verify overlord capacity
   - Check network connectivity
   - Review authentication settings

3. **Message Routing Issues**
   - Verify overlord interconnections
   - Check client registration
   - Review message format 