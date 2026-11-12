# Conversions and Math.Round

Sometimes a number must change type. Write the type in brackets in front of it:

```csharp
double hours = (double)7 / 2;   // 3.5: the 7 became a double before the division
int whole = (int)3.9;           // 3: the decimal part is cut off
```

To really round, use `Math.Round`: `Math.Round(3.6)` is `4`. It gives back a `double`, so to keep it in an `int`, cast it: `(int)Math.Round(3.6)`.

`Math` has other helpers: `Math.Floor` rounds down, `Math.Ceiling` rounds up, `Math.Abs` drops the sign, and `Math.Max` and `Math.Min` give the larger and the smaller of two numbers.
