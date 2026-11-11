## 1

Em C#, `7 / 2` dá `3`: a divisão entre dois inteiros descarta a parte decimal. Por isso `charge / capacity` dá `0` sempre que a carga é menor que a capacidade. A ordem das contas muda o resultado.

## 2

Em `Percent`, multiplique antes de dividir. Em `Hours`, transforme um dos lados em `double` antes de dividir, com `(double)`. Para arredondar, use `Math.Round(...)` e converta o resultado para `int` com `(int)`. O resto de uma divisão é o operador `%`.

## 3

Complete as lacunas:

```csharp
int percent = charge * ___ / capacity;

double hours = (double)___ / load;

int minutes = (int)Math.Round(hours * ___);

int leftover = minutes % ___;
```
