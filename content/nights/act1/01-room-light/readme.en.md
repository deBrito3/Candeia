# Night 1 · The bedroom light

If you are reading this, the bedroom light went out again. It was the first thing I connected to the system, and it is the first thing the system forgets.

CANDEIA asks the lamp four questions. Each question is a method of the `RoomLight` class, and each answer must have the right type:

| Method | Question | Type of the answer |
| --- | --- | --- |
| `Watts()` | How many watts does the lamp use? | `int`, a whole number |
| `Voltage()` | What is the voltage of the socket? | `double`, a number that may have decimals |
| `StartsOn()` | Does the lamp start lit? | `bool`, true (`true`) or false (`false`) |
| `Model()` | What is the lamp's model? | `string`, a text in quotes |

The lamp's box is on the desk: **60 watts, 220 volts, starts lit, model CND-60**.

`Watts()` still works. Write the other three the same way: store the answer in a variable of the right type and give that variable back with `return`.

Every right answer lights a lamp. Don't leave the house in the dark.
