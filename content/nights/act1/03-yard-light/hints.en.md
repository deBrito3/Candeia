## 1 · The idea

`ShouldLight` asks two questions, one after the other:

1. Is it night? That holds before 5 am (the small hours) and from 6 pm on. The rest of the day, the answer is already `false`, however dark it is.
2. If it is night, the light comes on only in the dark. And `IsDark` already knows how to answer that.

Today you can ask the first question with two separate `if`s: one for the small hours and one for the evening. If neither answered, it is day, and the method returns `false`. Next night, an operator joins both questions into one.

`Label` has only two paths: with the light on, one text; otherwise, the other. That is the case for an `if` with an `else`.

## 2 · The commands

- `if (condition) { ... } else { ... }`: the first block runs when the condition is `true`, the `else` block when it is `false`.
- "Before 5 am" is `hour < 5`. "From 6 pm on" is `hour >= 18`.
- A `bool` is already a condition: `if (on)` means "if `on` is `true`".
- A method can call another one of the same class: `IsDark(lux)` returns a `bool`.
- A `return` ends the method. What comes after it only runs if no `return` before it ran.

An example about clothes, which is not the answer:

```csharp
static string Clothes(int degrees, bool raining)
{
    if (raining)
    {
        return "raincoat";
    }
    if (degrees < 18)
    {
        return "jacket";
    }
    return "t-shirt";
}
```

## 3 · The skeleton

Replace each `___`:

```csharp
public static bool ShouldLight(int lux, int hour)
{
    if (hour < ___)
    {
        return IsDark(lux);   // small hours: only the dark decides
    }
    if (hour >= ___)
    {
        return ___;           // evening: as in the small hours
    }
    return ___;               // by day, never on
}

public static string Label(bool on)
{
    if (___)
    {
        return "ON";
    }
    else
    {
        return ___;
    }
}
```
