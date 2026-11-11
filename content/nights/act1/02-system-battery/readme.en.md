# Night 2 · The system battery

When the power fails, and in the rainy season it fails a lot, the whole house depends on this battery. If the panel says it is empty, the system turns the lights off to save energy. And without light, he is not afraid to come closer.

The panel shows 0% even when the battery is full. Fix the calculations of the `Battery` class:

| Method | What it gives back | Example |
| --- | --- | --- |
| `Percent(charge, capacity)` | The charge as a whole percentage, rounded down | `Percent(30, 120)` gives `25` |
| `Hours(charge, load)` | How many hours the battery lasts: the charge divided by the load, with decimals | `Hours(100, 40)` gives `2.5` |
| `Minutes(hours)` | The hours in whole minutes, rounded to the nearest minute | `Minutes(1.5)` gives `90` |
| `LeftoverMinutes(minutes)` | The minutes left over after the whole hours | `LeftoverMinutes(125)` gives `5` |

Careful with division: between two `int` values, it throws the decimals away.
