# Whole-number division and the remainder

Between two `int`s, division throws the decimals away. It does not round: it cuts.

```csharp
int a = 7 / 2;      // 3, not 3.5
int b = 30 / 120;   // 0
int c = 30 * 100 / 120;   // 25: multiplying first, nothing is lost
```

The `%` operator gives the remainder of whole-number division: `7 % 2` is `1`, because 7 = 3 × 2 + 1. It is handy for what is left over: `125 % 60` is `5`, the minutes left after the whole hours.

To keep the decimals, one side must be a `double`: `7 / 2.0` is `3.5`. See the page on conversions.
