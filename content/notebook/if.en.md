# if: the house decides

`if` runs a block only when the condition in the parentheses is `true`. The `else` right after it runs when it is `false`.

```csharp
if (temperature > 30)
{
    return "hot";
}
else
{
    return "cool";
}
```

The condition is always a `bool`: a comparison such as `lux < 50`, a `bool` variable, or a method that returns a `bool`.

A `return` inside the `if` ends the method right there. So after `if (...) return true;`, the next line only runs when the condition was false.

Mind the semicolon: `if (lux < 50);` ends the `if` on the spot, and the block below always runs.
