## 1 · A ideia

`ShouldLight` faz duas perguntas, uma depois da outra:

1. É de noite? Isso vale antes das 5h (a madrugada) e a partir das 18h. No resto do dia, a resposta já é `false`, por mais escuro que esteja.
2. Se é de noite, a luz acende só no escuro. E `IsDark` já sabe responder isso.

Hoje dá para fazer a primeira pergunta com dois `if` separados: um para a madrugada e outro para a noite. Se nenhum dos dois respondeu, é dia, e o método devolve `false`. Na próxima noite, um operador junta as duas perguntas numa só.

`Label` tem só dois caminhos: com a luz acesa, um texto; senão, o outro. É o caso do `if` com `else`.

## 2 · Os comandos

- `if (condição) { ... } else { ... }`: o primeiro bloco roda quando a condição é `true`, e o do `else` quando ela é `false`.
- "Antes das 5h" é `hour < 5`. "A partir das 18h" é `hour >= 18`.
- Um `bool` já é uma condição: `if (on)` quer dizer "se `on` for `true`".
- Um método pode chamar outro da mesma classe: `IsDark(lux)` devolve um `bool`.
- Um `return` encerra o método. O que vem depois dele só roda se nenhum `return` antes rodou.

Um exemplo de roupa, que não é a resposta:

```csharp
static string Roupa(int graus, bool chovendo)
{
    if (chovendo)
    {
        return "capa de chuva";
    }
    if (graus < 18)
    {
        return "casaco";
    }
    return "camiseta";
}
```

## 3 · O esqueleto

Troque cada `___`:

```csharp
public static bool ShouldLight(int lux, int hour)
{
    if (hour < ___)
    {
        return IsDark(lux);   // madrugada: só o escuro decide
    }
    if (hour >= ___)
    {
        return ___;           // noite: como na madrugada
    }
    return ___;               // de dia, nunca acende
}

public static string Label(bool on)
{
    if (___)
    {
        return "ON";
    }
    else
    {
        return ___;
    }
}
```
