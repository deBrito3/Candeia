# else and else if: the other paths

When there are more than two paths, each `else if` asks a new question, in order. The first true one wins, and the ones below are not even asked.

```csharp
if (grade >= 9)
{
    return "great";
}
else if (grade >= 6)
{
    return "good";
}
else
{
    return "try again";
}
```

Order matters: if `grade >= 6` came first, a 10 would stop there and never reach "great". Start with the most demanding condition.

The final `else` catches everything left. Without it, a method that returns a value needs a `return` after the last `if`, or it does not compile.
