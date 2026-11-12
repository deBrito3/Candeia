## 1 · A ideia

O painel mostra 0% por causa de um detalhe do C#: a divisão entre dois números inteiros (`int`) joga fora tudo o que vem depois da vírgula. Não arredonda, só corta.

Veja `Percent(30, 120)` no código de hoje, `charge / capacity * 100`:

- primeiro `30 / 120`, que daria 0,25, mas entre inteiros dá `0`;
- depois `0 * 100`, que continua `0`.

Mudando a ordem das contas, nada se perde: `30 * 100 / 120` faz primeiro `3000`, e `3000 / 120` dá `25`.

Os outros três métodos usam a mesma ideia de jeitos diferentes:

- `Hours` precisa das casas decimais, então a divisão não pode ser só entre inteiros;
- `Minutes` transforma horas em minutos e arredonda para o minuto mais próximo;
- `LeftoverMinutes` quer o que sobra depois de tirar as horas inteiras: o resto de uma divisão.

## 2 · Os comandos

- Para não perder as casas decimais, transforme um dos números em `double` antes de dividir: `(double)7 / 2` dá `3.5`. Esse `(double)` na frente do número se chama conversão.
- `Math.Round(x)` arredonda para o inteiro mais próximo, mas o resultado ainda é um `double`. Para guardar num `int`, converta: `(int)Math.Round(x)`.
- Uma hora tem 60 minutos: minutos = horas × 60.
- O operador `%` dá o resto da divisão: `17 % 5` dá `2`, porque 17 = 3 × 5 + 2.

Um exemplo com ovos, que não é a resposta:

```csharp
int ovos = 14;
int caixas = ovos / 6;            // 2 caixas cheias (14 / 6 entre inteiros)
int soltos = ovos % 6;            // 2 ovos sobrando
double porPessoa = (double)ovos / 4;   // 3.5, com a casa decimal
int arredondado = (int)Math.Round(porPessoa);  // 4
```

## 3 · O esqueleto

Troque cada `___`:

```csharp
// multiplique antes de dividir
int percent = charge * ___ / capacity;

// converta um dos lados para double antes de dividir
double hours = (double)___ / load;

// horas vezes os minutos de uma hora, arredondado
int minutes = (int)Math.Round(hours * ___);

// o resto da divisão pelos minutos de uma hora
int leftover = minutes % ___;
```

Lembre de devolver cada variável com `return`.
