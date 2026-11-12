# Conversões e Math.Round

Às vezes um número precisa mudar de tipo. Escreva o tipo entre parênteses na frente dele:

```csharp
double horas = (double)7 / 2;   // 3.5: o 7 virou double antes da divisão
int inteiro = (int)3.9;         // 3: a parte decimal é cortada
```

Para arredondar de verdade, use `Math.Round`: `Math.Round(3.6)` dá `4`. Ele devolve um `double`, então, para guardar num `int`, converta: `(int)Math.Round(3.6)`.

O `Math` tem outras ajudas: `Math.Floor` arredonda para baixo, `Math.Ceiling` para cima, `Math.Abs` tira o sinal, e `Math.Max` e `Math.Min` dão o maior e o menor de dois números.
