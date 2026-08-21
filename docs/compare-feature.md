# Compare Feature

The Compare feature collects system specifications from peers to provide a quick, comparable "spec sheet" and simple score for each node. It is built on the `COMPARE` protocol verb and a `CompareNode` model.

## What It Does
- Queries each peer for system specs via `GET /Fap.app/COMPARE`
- Displays per-node details (CPU, RAM, GPU, disks, displays, network) and a computed score
- Handles peers that deny comparison or fail to respond
- Caches specs on each peer for 5 minutes to avoid expensive hardware probes

## How It Works

### Protocol Flow
1. Local client enumerates known peers from `Model.Network.Nodes`
2. For each peer, it executes `CompareVerb` using the modern HTTP client (`FAP.Domain.Net.ModernHttpClient`)
3. The remote handler invokes `CompareVerb.ProcessRequest` which gathers and returns specs
4. The local client parses the response via `CompareVerb.ReceiveResponse` and updates the UI

### Key Types
- `FAP.Domain/Verbs/CompareVerb`
  - Request: `Verb = "COMPARE"`
  - Response JSON: `{ Allowed: bool, Node: CompareNode }`
  - Respects `Model.DisableComparision` (denies when true)
  - Caches response for 5 minutes using a static cache to limit hardware queries
- `FAP.Domain/Entities/CompareNode`
  - Extends `Node` with typed properties for specs and a computed `Score`
  - Backed by a flexible key/value store with `COMP-*` keys
- `FAP.Application/Controllers/CompareController`
  - Orchestrates parallel requests to all peers
  - Updates `CompareViewModel` with status and results

### Data Collected
Collected via `HardwareInfoService` (with fallbacks to `SystemInfo`) in `CompareVerb.ProcessRequest` and mapped into `CompareNode`:
- CPU: `CPUSpeed`, `CPUType`, `CPUCores`, `CPUThreads`, `CPUBits`
- Motherboard/BIOS: `MoboBrand`, `MoboModel`, `BIOSVersion`
- Memory: `RAMSize`
- GPU: `GPUModel`, `GPUCount`, `GPUTotalMemory`
- Display: `DisplayPrimaryWidth/Height`, `DisplayTotalWidth/Height`
- Storage: `HDDSize`, `HDDFree`, `HDDCount`
- Network: `NICSpeed`
- Audio: `SoundCard`

### Score Calculation
`CompareNode.GetSystemScore()` computes a simple aggregate score from the collected fields:
- Linear combination prioritizing CPU speed × threads, CPU bits, RAM size, GPU count/memory, total display area, and disk size/free

### Caching Behavior
- Each peer caches its `COMPARE` response for 5 minutes: `Environment.TickCount - cacheTime > 1000 * 300`
- Reduces WMI/hardware probes frequency and improves responsiveness

### Deny/Disable
- Setting `Model.DisableComparision = true` causes peers to return `Allowed = false`
- The UI shows `Status = "Denied"` for that peer

## HTTP Details
- Method: `GET /Fap.app/COMPARE`
- Hosting: ASP.NET Core Kestrel; requests are decoded centrally and dispatched to handlers
- Headers: typical FAP headers as applicable (`FAP-AUTH`, `FAP-SOURCE`)
- Request body: none
- Response: JSON serialized `CompareVerb` with `Allowed` and `Node`

Example response (abridged):
```json
{
  "Allowed": true,
  "Node": {
    "Nickname": "PeerPC",
    "CPUType": "Intel(R) Core(TM) i7",
    "CPUSpeed": 3400000000,
    "CPUThreads": 16,
    "RAMSize": 34359738368,
    "GPUModel": "NVIDIA RTX",
    "GPUTotalMemory": 8589934592,
    "HDDSize": 1000000000000,
    "HDDFree": 500000000000,
    "Score": 123456
  }
}
```

## UI Flow
- Command "Start" triggers requests to all peers in parallel
- Status updates reflect outstanding requests
- Results are appended to an observable collection for display and sorting
- When all responses are in, the Run button is re-enabled

## Configuration
- Disable comparisons: `Model.DisableComparision` (exposed in settings UI)
- Cache duration: fixed at 5 minutes in `CompareVerb`

## Error Handling
- If a peer fails to respond: status `"Error"`
- If denied by peer policy: status `"Denied"`

## Relevant Code
- Handlers: `FAP.Domain/Handlers/FAPClientHandler.HandleCompare`, `FAP.Domain/Handlers/FAPServerHandler.HandleCompare`
- Verb: `FAP.Domain/Verbs/CompareVerb`
- Model: `FAP.Domain/Entities/CompareNode`, `FAP.Domain/Entities/Model.DisableComparision`
- Controller: `FAP.Application/Controllers/CompareController`


