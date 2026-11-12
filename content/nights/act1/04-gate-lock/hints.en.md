## 1 · The idea

Today's two methods swapped "and" for "or", each one its own way:

- "From 6 pm until before 6 am" crosses midnight. No hour is 18 or more **and** less than 6 at once, so today's question is never true. A dangerous hour is one that is 18 or more, **or** one that is less than 6.
- The gate may only open when both things hold together: the right combination **and** not a dangerous hour. With "or", one of them is enough, and by day any combination opens it.

`Status` picks one of three answers, and the order of the questions decides. The dangerous hour comes first, because not even the right combination opens at night. Only then does the combination matter.

## 2 · The commands

- `&&` (and): both conditions must be true. `||` (or): one is enough. `!` (not) flips.
- Texts are compared with `==` (equal) and `!=` (different): `code != "3107"`.
- `if`, `else if`, `else`: the questions are asked in order, and the first true one wins.
- `Status` can call the `IsDangerousHour` you fix.

An example about a bakery, which is not the answer:

```csharp
static string Bakery(int hour, bool holiday)
{
    if (holiday)
    {
        return "CLOSED";
    }
    else if (hour >= 22 || hour < 4)
    {
        return "NIGHT SHIFT";
    }
    else
    {
        return "OPEN";
    }
}
```

## 3 · The skeleton

Replace each `___`:

```csharp
// IsDangerousHour
return hour >= 18 ___ hour < 6;

// CanOpen
return code == "3107" ___ !IsDangerousHour(hour);

// Status
if (IsDangerousHour(___))
{
    return "DANGEROUS HOUR";
}
else if (code ___ "3107")
{
    return "WRONG CODE";
}
else
{
    return ___;
}
```
