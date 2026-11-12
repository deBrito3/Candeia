# double: numbers with decimals

`double` keeps numbers that may have decimals: `220.0`, `1.5`, `0.25`, `-3.7`.

```csharp
double volts = 220.0;
double hours = 2.5;
```

In C#, decimals are written with a point, never a comma: `220,0` does not compile.

A `double` holds whole numbers too (`220` becomes `220.0`), but not the other way round: to put a `double` in an `int` you must convert it, and the decimal part is lost. See the page on conversions.
