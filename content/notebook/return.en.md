# return: the answer the house receives

Each method is a question the house asks. `return` is the answer it receives.

```csharp
public static int Watts()
{
    int watts = 60;
    return watts;
}
```

The word before the name (`int`) says the type of the answer. `return` must hand over a value of that type, and the method ends there: whatever comes after `return` does not run.

`public static` means the house system can call the method without creating anything first. The `class` around it gathers the methods of one piece of the house. A `void` method gives nothing back: it only does something.
