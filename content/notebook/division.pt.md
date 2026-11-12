# Divisão inteira e o resto

Entre dois `int`, a divisão joga fora as casas decimais. Não arredonda: corta.

```csharp
int a = 7 / 2;      // 3, e não 3.5
int b = 30 / 120;   // 0
int c = 30 * 100 / 120;   // 25: multiplicando antes, nada se perde
```

O operador `%` dá o resto da divisão inteira: `7 % 2` é `1`, porque 7 = 3 × 2 + 1. É útil para o que sobra: `125 % 60` é `5`, os minutos que sobram depois das horas inteiras.

Para ter as casas decimais, um dos lados precisa ser `double`: `7 / 2.0` dá `3.5`. Veja a página das conversões.
