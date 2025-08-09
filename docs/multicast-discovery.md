# Multicast Discovery

## Overview

The multicast discovery system is the foundation of FAP's automatic peer detection mechanism. It enables nodes to discover each other on the LAN without any manual configuration, forming the basis for the overlord election and client connection processes.

## Multicast Configuration

### Network Settings
- **Multicast Address**: `239.1.1.1`
- **Multicast Port**: `12`
- **Protocol**: UDP
- **TTL**: Default (typically 1 for LAN)

### Implementation
```csharp
public class MulticastCommon
{
    protected readonly IPAddress broadcastAddress = IPAddress.Parse("239.1.1.1");
    protected readonly int broadcastPort = 12;
}
```

## Discovery Components

### 1. MulticastServerService
**Purpose**: Sends multicast announcements to the network

**Key Features**:
- **Periodic Announcements**: Sends announcements every 10 seconds
- **Connection Management**: Manages UDP socket lifecycle
- **Message Encoding**: Formats announcements for network transmission

**Implementation**:
```csharp
public class MulticastServerService : MulticastCommon
{
    private Socket broadcastSocket;
    
    private void ConnectBroadcast()
    {
        broadcastSocket = new Socket(AddressFamily.InterNetwork, 
                                   SocketType.Dgram, 
                                   ProtocolType.Udp);
        broadcastSocket.SetSocketOption(SocketOptionLevel.IP, 
                                      SocketOptionName.AddMembership,
                                      new MulticastOption(broadcastAddress, IPAddress.Any));
        broadcastSocket.SetSocketOption(SocketOptionLevel.IP, 
                                      SocketOptionName.ReuseAddress, 1);
        broadcastSocket.Connect(broadcastAddress, broadcastPort);
    }
    
    public void SendMessage(string msg)
    {
        lock (sync)
        {
            if (null == broadcastSocket)
                ConnectBroadcast();
            broadcastSocket.SendTo(Encoding.UTF8.GetBytes(msg), 
                                 broadcastSocket.RemoteEndPoint);
        }
    }
}
```

### 2. MulticastClientService
**Purpose**: Listens for multicast announcements from other peers

**Key Features**:
- **Continuous Listening**: Runs in background thread
- **Message Processing**: Handles incoming announcements
- **Event Notification**: Notifies subscribers of received messages

**Implementation**:
```csharp
public class MulticastClientService : MulticastCommon
{
    public delegate void MultiCastRX(string cmd);
    public event MultiCastRX OnMultiCastRX;
    
    private void ConnectListen()
    {
        listenSocket = new Socket(AddressFamily.InterNetwork, 
                                 SocketType.Dgram, 
                                 ProtocolType.Udp);
        listenSocket.SetSocketOption(SocketOptionLevel.IP, 
                                   SocketOptionName.AddMembership,
                                   new MulticastOption(broadcastAddress, IPAddress.Any));
        listenSocket.SetSocketOption(SocketOptionLevel.IP, 
                                   SocketOptionName.ReuseAddress, true);
        listenSocket.Bind(new IPEndPoint(IPAddress.Any, broadcastPort));
        
        ThreadPool.QueueUserWorkItem(Process);
    }
    
    private void Process(object o)
    {
        while (true)
        {
            int length = listenSocket.Receive(buffer);
            if (null != OnMultiCastRX)
                OnMultiCastRX(Encoding.UTF8.GetString(buffer, 0, length));
        }
    }
}
```

### 3. LANPeerFinderService
**Purpose**: Manages discovered peers and their lifecycle

**Key Features**:
- **Peer Tracking**: Maintains list of discovered peers
- **Lifecycle Management**: Handles peer addition/removal
- **State Synchronization**: Keeps peer information current

**Implementation**:
```csharp
public class LANPeerFinderService
{
    private readonly BackgroundSafeObservable<DetectedNode> announcedAddresses;
    
    private void mclient_OnMultiCastRX(string cmd)
    {
        if (cmd.StartsWith(HelloVerb.Preamble))
        {
            var verb = new HelloVerb();
            DetectedNode node = verb.ParseRequest(cmd);
            if (null != node)
            {
                DetectedNode search = announcedAddresses
                    .Where(s => s.Address == node.Address).FirstOrDefault();
                if (null == search)
                {
                    node.LastAnnounce = DateTime.Now;
                    announcedAddresses.Add(node);
                }
                else
                {
                    search.LastAnnounce = DateTime.Now;
                    search.OverlordID = node.OverlordID;
                    search.NetworkName = node.NetworkName;
                    search.NetworkID = node.NetworkID;
                    search.Priority = node.Priority;
                    search.CurrentUsers = node.CurrentUsers;
                    search.MaxUsers = node.MaxUsers;
                }
            }
        }
    }
}
```

## Announcement Protocol

### HelloVerb Message Format
**Purpose**: Announce overlord presence and capabilities

**Message Structure**:
```
FAPHELLO
[base64-encoded-address]
[base64-encoded-network-name]
[base64-encoded-overlord-id]
[base64-encoded-network-id]
[priority]
[current-users]
[max-users]
```

**Example Message**:
```
FAPHELLO
MTkyLjE2OC4xLjEwMDo4MDgx
TG9jYWw=
b3ZlcmxvcmQxMjM=
TG9jYWw=
50
5
50
```

### Message Encoding
```csharp
public string CreateRequest(string address, string name, string overlordid, 
                          string networkid, int priority, int userCount, int maxUsers)
{
    var sb = new StringBuilder();
    sb.Append(Preamble);  // "FAPHELLO"
    sb.Append("\n");
    sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(address)));
    sb.Append("\n");
    sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(name)));
    sb.Append("\n");
    sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(overlordid)));
    sb.Append("\n");
    sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(networkid)));
    sb.Append("\n");
    sb.Append(priority);
    sb.Append("\n");
    sb.Append(userCount);
    sb.Append("\n");
    sb.Append(maxUsers);
    return sb.ToString();
}
```

### Message Parsing
```csharp
public DetectedNode ParseRequest(string cmd)
{
    string[] lines = cmd.Split('\n');
    if (lines.Length < 8) return null;
    
    return new DetectedNode
    {
        Address = Encoding.UTF8.GetString(Convert.FromBase64String(lines[1])),
        NetworkName = Encoding.UTF8.GetString(Convert.FromBase64String(lines[2])),
        OverlordID = Encoding.UTF8.GetString(Convert.FromBase64String(lines[3])),
        NetworkID = Encoding.UTF8.GetString(Convert.FromBase64String(lines[4])),
        Priority = int.Parse(lines[5]),
        CurrentUsers = int.Parse(lines[6]),
        MaxUsers = int.Parse(lines[7])
    };
}
```

## WhoVerb Protocol

### Purpose
**WhoVerb** is used to trigger immediate announcements from all peers on the network.

### Message Format
```csharp
public static string CreateRequest()
{
    return "FAPWHO";
}
```

### Usage
```csharp
// Send WHO request to trigger immediate announcements
mserver.SendMessage(WhoVerb.CreateRequest());
```

## Discovery Lifecycle

### 1. Startup Phase
```
1. Start MulticastClientService
2. Begin listening for announcements
3. Send initial WHO request
4. Wait for peer responses
```

### 2. Announcement Phase
```
1. Overlords send periodic HELLO messages
2. Clients listen for announcements
3. Update peer lists with received information
4. Trigger connection attempts
```

### 3. Maintenance Phase
```
1. Continue periodic announcements
2. Monitor peer activity
3. Remove stale peers
4. Handle network changes
```

## Peer Management

### DetectedNode Structure
```csharp
public class DetectedNode
{
    public string Address { get; set; }           // IP:Port
    public string NetworkName { get; set; }       // Network identifier
    public string NetworkID { get; set; }         // Network ID
    public string OverlordID { get; set; }        // Overlord identifier
    public int Priority { get; set; }             // Overlord priority
    public int CurrentUsers { get; set; }         // Current client count
    public int MaxUsers { get; set; }             // Maximum capacity
    public DateTime LastAnnounce { get; set; }    // Last announcement time
}
```

### Peer Lifecycle
1. **Discovery**: Peer announces via multicast
2. **Registration**: Added to peer list
3. **Monitoring**: Track announcement frequency
4. **Cleanup**: Remove after timeout (60 seconds)

### Peer Validation
```csharp
// Check if peer is still active
bool isActive = (DateTime.Now - peer.LastAnnounce).TotalSeconds < 60;

// Remove stale peers
if (!isActive)
{
    announcedAddresses.Remove(peer);
}
```

## Network Topology Discovery

### Automatic Discovery
- **Zero Configuration**: No manual setup required
- **Dynamic Updates**: Network changes detected automatically
- **Fault Tolerance**: Continues operation with partial network

### Topology Building
```
1. Listen for multicast announcements
2. Build peer list from announcements
3. Attempt connections to discovered peers
4. Establish overlord-client relationships
5. Form network topology
```

## Performance Considerations

### Announcement Frequency
- **Default Interval**: 10 seconds between announcements
- **Immediate Response**: WHO requests trigger immediate announcements
- **Adaptive Timing**: Adjust based on network conditions

### Network Efficiency
- **UDP Multicast**: Efficient for LAN discovery
- **Minimal Payload**: Compact announcement messages
- **TTL Control**: Limit to local network segment

### Scalability
- **Peer Limits**: Practical limits based on network capacity
- **Message Filtering**: Ignore irrelevant announcements
- **Resource Management**: Efficient socket and memory usage

## Error Handling

### Network Issues
- **Multicast Failures**: Fall back to broadcast
- **Socket Errors**: Automatic reconnection
- **Message Corruption**: Validate message format

### Recovery Mechanisms
```csharp
// Handle multicast receive errors
try
{
    int length = listenSocket.Receive(buffer);
    if (null != OnMultiCastRX)
        OnMultiCastRX(Encoding.UTF8.GetString(buffer, 0, length));
}
catch (SocketException ex)
{
    // Log error and continue listening
    logger.Error("Multicast receive error", ex);
}
```

## Security Considerations

### LAN-Only Design
- **Network Isolation**: Designed for trusted LAN environments
- **No Authentication**: Relies on network-level security
- **Simple Protocol**: Minimal attack surface

### Message Validation
- **Format Checking**: Validate message structure
- **Content Validation**: Verify announcement data
- **Source Verification**: Check message origin

## Monitoring and Debugging

### Discovery Metrics
- **Active Peers**: Number of discovered peers
- **Announcement Rate**: Messages per second
- **Network Coverage**: Percentage of network discovered
- **Response Times**: Time to discover new peers

### Debugging Tools
Use structured logging to capture multicast events and discovery:
```csharp
logger.LogDebug("Received multicast: {Cmd}", cmd);
logger.LogInformation("Discovered peer: {OverlordId} at {Address}", peer.OverlordID, peer.Address);
logger.LogTrace("Sending announcement: {Announcement}", announcement);
```

## Configuration

### Multicast Settings
```csharp
// Multicast address and port
protected readonly IPAddress broadcastAddress = IPAddress.Parse("239.1.1.1");
protected readonly int broadcastPort = 12;

// Announcement interval
private const int ANNOUNCEMENT_INTERVAL = 10000; // 10 seconds

// Peer timeout
private const int PEER_TIMEOUT = 60000; // 60 seconds
```

### Network Tuning
- **Buffer Sizes**: Optimize for expected message sizes
- **Socket Options**: Configure for multicast efficiency
- **Thread Management**: Balance responsiveness and resource usage

## Best Practices

### Network Design
1. **Multicast Support**: Ensure network supports multicast
2. **Firewall Configuration**: Allow UDP port 12
3. **Network Segmentation**: Consider VLAN configuration
4. **Bandwidth Planning**: Account for announcement traffic

### Performance Optimization
1. **Announcement Timing**: Balance discovery speed vs. network load
2. **Peer Limits**: Set appropriate maximum peer counts
3. **Resource Management**: Monitor memory and CPU usage
4. **Error Recovery**: Implement robust error handling

### Troubleshooting
1. **Multicast Issues**: Check network multicast support
2. **Discovery Failures**: Verify firewall settings
3. **Performance Problems**: Monitor announcement frequency
4. **Peer Loss**: Check network connectivity and timeouts 