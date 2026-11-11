## 1

In C#, `7 / 2` is `3`: dividing two whole numbers drops the decimal part. That is why `charge / capacity` is `0` whenever the charge is smaller than the capacity. The order of the calculations changes the result.

## 2

In `Percent`, multiply before you divide. In `Hours`, turn one side into a `double` before dividing, with `(double)`. To round, use `Math.Round(...)` and turn the result into an `int` with `(int)`. The remainder of a division is the `%` operator.

## 3

Fill in the blanks:

```csharp
int percent = charge * ___ / capacity;

double hours = (double)___ / load;

int minutes = (int)Math.Round(hours * ___);

int leftover = minutes % ___;
```
