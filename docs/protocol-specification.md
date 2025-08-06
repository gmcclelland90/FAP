# FAP Protocol Specification

## Overview

The File Acceleration Protocol (FAP) is a custom application-layer protocol that "hitches a ride" on HTTP/1.1. It provides a structured way for clients and overlords to communicate for file sharing, messaging, and network coordination, while leveraging existing HTTP infrastructure and tools.

## Protocol Stack

```
┌─────────────────────────────────────┐
│           FAP Protocol              │  ← Application Layer
├─────────────────────────────────────┤
│           HTTP/1.1                  │  ← Transport Layer
├─────────────────────────────────────┤
│              TCP                    │  ← Network Layer
├─────────────────────────────────────┤
│            Ethernet                 │  ← Link Layer
└─────────────────────────────────────┘
```

## URL Structure

### HTTP Hitchhiking Design
FAP uses HTTP as its transport layer, with custom URL paths to distinguish protocol requests from standard web interface requests. This allows FAP to leverage existing HTTP infrastructure while maintaining its custom protocol functionality.

### Base URL Format
```
http://host:port/Fap.app/VERB?p=base64param
```

### Examples
```
http://192.168.1.100:30/Fap.app/CONNECT
http://192.168.1.100:30/Fap.app/BROWSE?p=L2ZvbGRlcg==
http://192.168.1.100:30/Fap.app/CHAT
```

## HTTP Headers

### Standard Headers
- **User-Agent**: `FAP Beat 7.5ish`
- **Content-Type**: `application/json` (for POST requests)
- **Content-Length**: Size of request body

### FAP-Specific Headers
- **FAP-AUTH**: Authentication key for secure communication
- **FAP-SOURCE**: Source node ID generating the message
- **FAP-OVERLORD**: Overlord ID which the client is connected to

### Header Examples
```http
GET /Fap.app/CONNECT HTTP/1.1
Host: 192.168.1.100:8080
User-Agent: FAP Beat 7.5ish
FAP-AUTH: abc123def456
FAP-SOURCE: node123
FAP-OVERLORD: overlord456
```

## NetworkRequest Structure

### Core Fields
```csharp
public class NetworkRequest
{
    public string Verb { set; get; }        // FAP verb (CONNECT, BROWSE, etc.)
    public string Data { set; get; }        // JSON payload for POST requests
    public string Param { set; get; }       // Base64 encoded parameter
    public string SourceID { set; get; }    // ID of node generating message
    public string OverlordID { set; get; }  // ID of overlord handling request
    public string AuthKey { set; get; }     // Authentication token
}
```

### Request Encoding
```csharp
public static string Encode(string url, string verb, string param)
{
    var sb = new StringBuilder();
    if (!url.StartsWith("http://"))
        sb.Append("http://");
    sb.Append(url);
    if (url.EndsWith("/"))
        sb.Append("Fap.app/");
    else
        sb.Append("/Fap.app/");
    sb.Append(verb);
    if (!string.IsNullOrEmpty(param))
    {
        sb.Append("?p=");
        sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(param))
            .Replace('+', '_'));
    }
    return sb.ToString();
}
```

## Protocol Verbs

### 1. CONNECT
**Purpose**: Establish connection between client and overlord

**Request Format**:
```http
POST /Fap.app/CONNECT HTTP/1.1
Content-Type: application/json
FAP-AUTH: [secret]
FAP-SOURCE: [client-id]

{
    "Address": "192.168.1.100:8080",
    "ClientType": "Client",
    "Secret": "abc123def456"
}
```

**Response**:
```http
HTTP/1.1 200 OK
FAP-AUTH: [secret]
FAP-SOURCE: [overlord-id]
FAP-OVERLORD: [overlord-id]
```

### 2. INFO
**Purpose**: Exchange node information

**Request Format**:
```http
GET /Fap.app/INFO HTTP/1.1
FAP-AUTH: [secret]
FAP-SOURCE: [node-id]
```

**Response**:
```json
{
    "ID": "node123",
    "Nickname": "MyComputer",
    "Host": "192.168.1.100",
    "Port": 8080,
    "Online": true,
    "Shares": [
        {
            "Name": "Documents",
            "Path": "C:\\Documents",
            "Size": 1073741824
        }
    ]
}
```

### 3. BROWSE
**Purpose**: Browse shared files and folders

**Request Format**:
```http
GET /Fap.app/BROWSE?p=L2ZvbGRlcg== HTTP/1.1
FAP-AUTH: [secret]
FAP-SOURCE: [client-id]
```

**Response**:
```json
{
    "Path": "/folder",
    "NoCache": false,
    "Results": [
        {
            "Name": "document.txt",
            "Size": 1024,
            "LastModified": "2023-01-01T12:00:00Z",
            "IsDirectory": false
        },
        {
            "Name": "subfolder",
            "Size": 0,
            "LastModified": "2023-01-01T12:00:00Z",
            "IsDirectory": true
        }
    ]
}
```

### 4. CHAT
**Purpose**: Send chat messages

**Request Format**:
```http
POST /Fap.app/CHAT HTTP/1.1
Content-Type: application/json
FAP-AUTH: [secret]
FAP-SOURCE: [sender-id]

{
    "Nickname": "User1",
    "Message": "Hello everyone!",
    "SourceID": "user123"
}
```

**Response**:
```http
HTTP/1.1 200 OK
```

### 5. UPDATE
**Purpose**: Notify about node status changes

**Request Format**:
```http
POST /Fap.app/UPDATE HTTP/1.1
Content-Type: application/json
FAP-AUTH: [secret]
FAP-SOURCE: [overlord-id]

{
    "Nodes": [
        {
            "ID": "node123",
            "Online": true,
            "Nickname": "MyComputer"
        }
    ]
}
```

**Response**:
```http
HTTP/1.1 200 OK
```

### 6. SEARCH
**Purpose**: Search for files across the network

**Request Format**:
```http
GET /Fap.app/SEARCH?p=c2VhcmNodGVybQ== HTTP/1.1
FAP-AUTH: [secret]
FAP-SOURCE: [client-id]
```

**Response**:
```json
{
    "SearchTerm": "searchterm",
    "Results": [
        {
            "Name": "document.txt",
            "Path": "/folder/document.txt",
            "Size": 1024,
            "NodeID": "node123",
            "NodeName": "MyComputer"
        }
    ]
}
```

### 7. COMPARE
**Purpose**: Compare system specifications

**Request Format**:
```http
GET /Fap.app/COMPARE HTTP/1.1
FAP-AUTH: [secret]
FAP-SOURCE: [client-id]
```

**Response**:
```json
{
    "Nodes": [
        {
            "ID": "node123",
            "Nickname": "MyComputer",
            "Specs": {
                "CPU": "Intel i7",
                "RAM": "16GB",
                "OS": "Windows 10"
            }
        }
    ]
}
```

### 8. GET
**Purpose**: Download files

**Request Format**:
```http
GET /Fap.app/GET?p=L2ZpbGUudHh0 HTTP/1.1
FAP-AUTH: [secret]
FAP-SOURCE: [client-id]
```

**Response**:
```http
HTTP/1.1 200 OK
Content-Type: application/octet-stream
Content-Length: 1024

[file content]
```

### 9. NOOP
**Purpose**: Keep-alive ping

**Request Format**:
```http
GET /Fap.app/NOOP HTTP/1.1
FAP-AUTH: [secret]
FAP-SOURCE: [client-id]
```

**Response**:
```http
HTTP/1.1 200 OK
```

## Authentication

### Secret Key Generation
```csharp
public static string CreateID()
{
    return Guid.NewGuid().ToString("N");
}
```

### Authentication Flow
1. **Client generates secret**: Unique ID for this connection
2. **Client sends CONNECT**: Includes secret in FAP-AUTH header
3. **Overlord validates**: Checks secret format and uniqueness
4. **Overlord responds**: Returns overlord secret in response
5. **Subsequent requests**: Use overlord secret for authentication

### Security Considerations
- **Secret Validation**: Only allow connections with valid secrets
- **Self-Connection Prevention**: Don't allow connections to self
- **Connection Limits**: Prevent multiple simultaneous connections
- **Secret Rotation**: Generate new secrets for reconnections

## Error Handling

### HTTP Status Codes
- **200 OK**: Request successful
- **400 Bad Request**: Malformed request
- **401 Unauthorized**: Authentication failed
- **404 Not Found**: Resource not found
- **500 Internal Server Error**: Server error

### Error Response Format
```json
{
    "Error": "Error message",
    "Code": "ERROR_CODE",
    "Details": "Additional error details"
}
```

## Message Flow

### Client to Overlord
```
1. Client creates NetworkRequest
2. Client encodes request as HTTP
3. Client sends HTTP request to overlord
4. Overlord processes request
5. Overlord sends HTTP response
6. Client processes response
```

### Overlord to Overlord
```
1. Overlord A creates message
2. Overlord A sends to Overlord B
3. Overlord B processes message
4. Overlord B forwards to clients
5. Overlord B sends response to Overlord A
```

### Multicast Messages
```
1. Overlord creates HelloVerb
2. Overlord encodes as UDP packet
3. Overlord sends to multicast address
4. All peers receive announcement
5. Peers update peer lists
```

## Performance Considerations

### Request Optimization
- **Connection Reuse**: Keep HTTP connections alive
- **Batch Operations**: Combine multiple operations
- **Compression**: Use gzip for large responses
- **Caching**: Cache frequently accessed data

### Network Efficiency
- **Keep-Alive**: Use HTTP keep-alive connections
- **Chunked Transfer**: Support large file transfers
- **Timeout Handling**: Implement appropriate timeouts
- **Retry Logic**: Exponential backoff for failures

## Protocol Versioning

### Version Information
- **Protocol Version**: `FAP/1.0`
- **Application Version**: `FAP Beat 7.5ish`
- **Backward Compatibility**: Maintained within major versions

### Version Negotiation
```http
GET /Fap.app/INFO HTTP/1.1
User-Agent: FAP Beat 7.5ish
```

## Implementation Notes

### Multiplexor
The `Multiplexor` class handles protocol encoding/decoding:

```csharp
public static NetworkRequest Decode(IRequest r)
{
    var req = new NetworkRequest();
    
    // Extract verb from URL path
    req.Verb = r.Uri.AbsolutePath.Substring("/Fap.app/".Length);
    
    // Extract parameter from query string
    IParameter param = r.Parameters.Where(p => p.Name == "p").FirstOrDefault();
    if (null != param)
    {
        req.Param = Encoding.UTF8.GetString(
            Convert.FromBase64String(param.Value.Replace('_', '+')));
    }
    
    // Extract data from POST body
    if (r.Method == "POST")
    {
        req.Data = GetPostString(r);
    }
    
    // Extract FAP headers
    var headers = r.Headers as HeaderCollection;
    if (null != headers)
    {
        foreach (IHeader h in headers)
        {
            var header = h as StringHeader;
            if (null != header)
            {
                switch (header.Name.ToUpper())
                {
                    case "FAP-AUTH":
                        req.AuthKey = header.Value;
                        break;
                    case "FAP-SOURCE":
                        req.SourceID = header.Value;
                        break;
                    case "FAP-OVERLORD":
                        req.OverlordID = header.Value;
                        break;
                }
            }
        }
    }
    
    return req;
}
```

### Verb Implementation
Each verb implements the `IVerb` interface:

```csharp
public interface IVerb
{
    NetworkRequest CreateRequest();
    NetworkRequest ProcessRequest(NetworkRequest r);
    bool ReceiveResponse(NetworkRequest r);
}
```

## Testing

### Protocol Testing
- **Unit Tests**: Test individual verb implementations
- **Integration Tests**: Test end-to-end communication
- **Load Tests**: Test performance under load
- **Error Tests**: Test error handling scenarios

### Debugging
- **Logging**: Comprehensive protocol logging
- **Tracing**: Detailed request/response tracing
- **Monitoring**: Real-time protocol metrics
- **Analysis**: Protocol analysis tools 