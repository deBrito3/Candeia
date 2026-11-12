# Operations: the house's arithmetic

C# does the usual arithmetic: `+` adds, `-` subtracts, `*` multiplies and `/` divides.

```csharp
int total = 3 + 4 * 2;     // 11: multiplication comes first
int other = (3 + 4) * 2;   // 14: brackets rule
```

The order is the one from school: first `*`, `/` and `%`, then `+` and `-`. When in doubt, use brackets; they cost nothing and make the arithmetic clear to whoever reads it.

When an operation mixes `int` and `double`, the result is a `double`: `2 * 1.5` is `3.0`.
