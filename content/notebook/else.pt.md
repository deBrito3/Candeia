# else e else if: os outros caminhos

Quando há mais de dois caminhos, cada `else if` faz uma pergunta nova, na ordem. A primeira verdadeira vence, e as de baixo nem são feitas.

```csharp
if (nota >= 9)
{
    return "ótimo";
}
else if (nota >= 6)
{
    return "bom";
}
else
{
    return "tente de novo";
}
```

A ordem importa: se `nota >= 6` viesse primeiro, uma nota 10 pararia ali e nunca chegaria a "ótimo". Comece pela condição mais exigente.

O `else` final pega tudo o que sobrou. Sem ele, um método que devolve um valor precisa de um `return` depois do último `if`, senão não compila.
