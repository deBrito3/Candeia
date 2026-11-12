# Operações: as contas da casa

O C# faz as contas de sempre: `+` soma, `-` subtrai, `*` multiplica e `/` divide.

```csharp
int total = 3 + 4 * 2;     // 11: a multiplicação vem antes
int outro = (3 + 4) * 2;   // 14: os parênteses mandam
```

A ordem é a da escola: primeiro `*`, `/` e `%`, depois `+` e `-`. Na dúvida, use parênteses; eles não custam nada e deixam a conta clara para quem ler.

Quando a conta mistura `int` e `double`, o resultado é `double`: `2 * 1.5` dá `3.0`.
