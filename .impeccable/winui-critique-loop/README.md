# WinUI critique loop state

Runtime control for the agent skill `.cursor/skills/fap-winui-critique-loop`.

| File | Tracked? | Purpose |
|------|----------|---------|
| `state.schema.json` | yes | JSON Schema for `state.json` |
| `state.example.json` | yes | Template for a fresh queue |
| `state.json` | **no** (gitignored) | Live progress |

Start or resume from Agent chat: *Run the winui critique loop* / *Continue the winui critique loop from state*.

Heartbeat:

```text
/loop continue the winui critique loop from .impeccable/winui-critique-loop/state.json
```
