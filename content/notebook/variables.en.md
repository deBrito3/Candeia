# Variables: named boxes

A variable is a named box where you keep a value to use later. Every box has a type, and the type says what fits in it.

```csharp
int doors = 3;
```

Read it left to right: the type (`int`), the name (`doors`), the `=` sign and the value (`3`). The `=` does not ask whether something is equal: it puts the value in the box. The `;` ends the sentence.

Afterwards, just use the name: `doors + 1` is 4. Pick names that say what the box keeps; whoever reads it in ten years will thank you. That is what I tried to do in this system.

With `var`, C# works the type out from the value: `var doors = 3;` is an `int` too.
