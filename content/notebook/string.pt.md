# string: textos

`string` guarda texto, sempre entre aspas duplas: `"CND-60"`, `"quarto"`, `""` (um texto vazio).

```csharp
string modelo = "CND-60";
string comodo = "quarto";
```

Sem as aspas, o C# acha que `CND-60` é uma conta: um nome `CND` menos `60`. Por isso o erro.

Textos se juntam com `+`: `"luz do " + comodo` dá `"luz do quarto"`. Um caractere só, como `'A'`, é um `char` e vai entre aspas simples.
