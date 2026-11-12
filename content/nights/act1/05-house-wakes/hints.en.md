## 1 · The idea

Each method uses something from the nights before:

- `TotalLoad` is a sum of three numbers.
- `Usage` is a division that needs the decimals, as with the battery: between two `int`s, 1 of 8 would be `0`, not `12.5`.
- `CanTurnOn` is a comparison: the current load plus the new one cannot go past the capacity. Reaching it exactly still fits.
- `NextCircuit` is a ladder of questions, as at the gate, in the fuse box's order. The first question joins two conditions: it is raining **and** the pump fits. If it rains but the pump does not fit, the ladder goes down to the fridge.

`NextCircuit` can use the `CanTurnOn` you have just written, once with the pump's watts and once with the fridge's.

## 2 · The commands

- A percentage with decimals: convert before dividing, `(double)part * 100 / whole`.
- `<=` is "less or equal": it is what says that reaching the capacity exactly still fits.
- `if`, `else if`, `else`, with `&&` to join two conditions into one question.

An example about a backpack, which is not the answer:

```csharp
static string Backpack(int weight, int limit, bool longTrip)
{
    if (longTrip && weight + 2 <= limit)
    {
        return "TAKE THE COAT";
    }
    else if (weight + 1 <= limit)
    {
        return "TAKE THE BOTTLE";
    }
    else
    {
        return "NOTHING MORE";
    }
}
```

## 3 · The skeleton

Replace each `___`:

```csharp
// TotalLoad
return lights ___ fridge ___ pump;

// Usage
return (double)load * ___ / capacity;

// CanTurnOn
return load + extra ___ capacity;

// NextCircuit
if (raining ___ CanTurnOn(load, ___, capacity))
{
    return "PUMP";
}
else if (CanTurnOn(load, ___, capacity))
{
    return "FRIDGE";
}
else
{
    return ___;
}
```
