# string: text

`string` keeps text, always in double quotes: `"CND-60"`, `"bedroom"`, `""` (an empty text).

```csharp
string model = "CND-60";
string room = "bedroom";
```

Without the quotes, C# thinks `CND-60` is arithmetic: a name `CND` minus `60`. Hence the error.

Texts join with `+`: `"light of the " + room` gives `"light of the bedroom"`. A single character, like `'A'`, is a `char` and goes in single quotes.
