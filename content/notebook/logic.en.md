# And, or, not: && || !

To join questions into one:

- `a && b` (and): true only when both are true.
- `a || b` (or): true when at least one is true.
- `!a` (not): flips the answer. `!true` is `false`.

```csharp
bool shopOpens = weekday && !holiday;
bool bakeryOpen = hour >= 22 || hour < 4;
bool workHours = hour >= 8 && hour < 17;
```

A range that crosses midnight, such as 10 pm to 4 am, uses `||`: no hour is greater than 22 and less than 4 at once. A range inside the day, such as 8 am to 5 pm, uses `&&`.

Swapping one for the other is a common defect, and the code still compiles: the `&&` becomes a question that is never true, or the `||` one that almost always is.
