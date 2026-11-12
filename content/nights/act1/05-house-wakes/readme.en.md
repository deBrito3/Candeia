# Night 5 · The house wakes

The storm took the power, as it does every year. When the house goes dark all at once, he does not wait: he comes straight in. Tonight he reaches the window. Do not keep looking at it.

The lights hold on the battery you fixed. The rest of the house waits at the fuse box, one circuit at a time, never past the load the mains can carry. Fix the `FuseBox` class:

| Method | What it returns | Example |
| --- | --- | --- |
| `TotalLoad(lights, fridge, pump)` | The whole load, in watts | `TotalLoad(100, 300, 800)` returns `1200` |
| `Usage(load, capacity)` | How much of the capacity the load uses, as a percentage with decimals | `Usage(500, 2000)` returns `25.0` |
| `CanTurnOn(load, extra, capacity)` | Whether `extra` more watts fit without going past the capacity | `CanTurnOn(1000, 800, 2000)` returns `true` |
| `NextCircuit(load, capacity, raining)` | The next circuit to turn back on | `NextCircuit(500, 2000, true)` returns `"PUMP"` |

The fuse box's order: with rain, the water pump (`"PUMP"`, 800 W) first, if it fits. Otherwise the fridge (`"FRIDGE"`, 300 W), if it fits. Otherwise `"WAIT"`. Reaching the capacity exactly still fits.

This is the siege night: more time, and everything you have learned so far.
