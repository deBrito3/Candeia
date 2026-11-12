# Night 4 · The gate lock

The iron gate is the second defence. I put an electric lock with a combination on it, so nobody would come in without my knowing. And I told the system never to open at night, not even with the right combination.

Someone has been in this code. Today the gate opens with any combination, and the dangerous hours never begin. Fix the `GateLock` class:

| Method | What it returns | Example |
| --- | --- | --- |
| `IsDangerousHour(hour)` | Whether it is a dangerous hour: from 6 pm until before 6 am | `IsDangerousHour(22)` returns `true` |
| `CanOpen(code, hour)` | Whether the gate opens: the combination `"3107"` and outside the dangerous hours | `CanOpen("3107", 10)` returns `true` |
| `Status(code, hour)` | What the panel by the gate shows | `Status("1234", 14)` returns `"WRONG CODE"` |

The panel shows `"DANGEROUS HOUR"` in the dangerous hours, even with the right combination. Outside them, it shows `"WRONG CODE"` for a wrong combination and `"OPEN"` for the right one.

The code compiles and runs. The defect is in the logic, not in the writing.
