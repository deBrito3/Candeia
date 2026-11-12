## 1 · The idea

The panel shows 0% because of a C# detail: dividing two whole numbers (`int`) throws away everything after the decimal point. It does not round, it just cuts.

Look at `Percent(30, 120)` in today's code, `charge / capacity * 100`:

- first `30 / 120`, which would be 0.25, but between whole numbers is `0`;
- then `0 * 100`, which is still `0`.

Changing the order of the operations loses nothing: `30 * 100 / 120` first makes `3000`, and `3000 / 120` is `25`.

The other three methods use the same idea in different ways:

- `Hours` needs the decimals, so the division cannot be between whole numbers only;
- `Minutes` turns hours into minutes and rounds to the nearest minute;
- `LeftoverMinutes` wants what is left after taking out the whole hours: the remainder of a division.

## 2 · The commands

- To keep the decimals, turn one of the numbers into a `double` before dividing: `(double)7 / 2` is `3.5`. That `(double)` in front of the number is called a cast.
- `Math.Round(x)` rounds to the nearest whole number, but the result is still a `double`. To keep it in an `int`, cast it: `(int)Math.Round(x)`.
- An hour has 60 minutes: minutes = hours × 60.
- The `%` operator gives the remainder of a division: `17 % 5` is `2`, because 17 = 3 × 5 + 2.

An example with eggs, which is not the answer:

```csharp
int eggs = 14;
int boxes = eggs / 6;             // 2 full boxes (14 / 6 between whole numbers)
int loose = eggs % 6;             // 2 eggs left over
double perPerson = (double)eggs / 4;   // 3.5, with the decimal
int rounded = (int)Math.Round(perPerson);  // 4
```

## 3 · The skeleton

Replace each `___`:

```csharp
// multiply before dividing
int percent = charge * ___ / capacity;

// cast one side to double before dividing
double hours = (double)___ / load;

// hours times the minutes in an hour, rounded
int minutes = (int)Math.Round(hours * ___);

// the remainder of dividing by the minutes in an hour
int leftover = minutes % ___;
```

Remember to give each variable back with `return`.
