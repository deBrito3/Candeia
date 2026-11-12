# double: números com casas decimais

`double` guarda números que podem ter casas decimais: `220.0`, `1.5`, `0.25`, `-3.7`.

```csharp
double volts = 220.0;
double horas = 2.5;
```

No C#, a casa decimal se escreve com ponto, nunca com vírgula: `220,0` não compila.

Um `double` também guarda números inteiros (`220` vira `220.0`), mas o contrário não vale: para pôr um `double` num `int`, é preciso converter, e a parte decimal se perde. Veja a página das conversões.
