## 1 · The idea

Each method of the `RoomLight` class is a question the system asks the lamp, and the answer comes out through `return`. The word written before the method's name says what kind of answer the system expects:

- `int Watts()` expects a whole number, like `60`;
- `double Voltage()` expects a number that may have decimals, like `127.5`;
- `bool StartsOn()` expects a yes or no: `true` or `false`;
- `string Model()` expects a text, like `"ABC-1"`.

Right now, `Voltage()` gives back `0`, `StartsOn()` gives back `false` and `Model()` gives back an empty text. The right answers are on the lamp's box, described in the README.

The README also asks for each answer to be kept in a variable before it is given back, the way `Watts()` already does. That is why giving the value back directly, like `return true;`, lights the lamp but does not win the night.

## 2 · The commands

A variable is a named box that keeps a value. It is created in a single line: the type, the name, the `=` sign, the value and a `;` at the end. Then `return` gives back what it keeps.

An example with other values, not the lamp's:

```csharp
int doors = 3;           // whole number
double weight = 1.5;     // decimals: use a point, never a comma
bool open = true;        // only true or false, no quotes
string colour = "blue";  // text always goes in double quotes
return weight;           // gives back the value kept in weight
```

Common mistakes:

- `220,0` with a comma does not compile: in C#, decimals use a point;
- `"true"` in quotes becomes text, not a `bool`;
- text without quotes, like `CND-60`, is read by C# as names and arithmetic, not as text;
- a missing `;` at the end of the line.

## 3 · The skeleton

Replace each `___` with the value from the lamp's box:

```csharp
public static double Voltage()
{
    double volts = ___;    // the socket's voltage, with a decimal
    return volts;
}

public static bool StartsOn()
{
    bool on = ___;         // does the lamp start lit?
    return on;
}

public static string Model()
{
    string model = ___;    // the model, in double quotes
    return model;
}
```
