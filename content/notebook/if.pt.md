# if: a casa decide

O `if` roda um bloco só quando a condição entre parênteses é `true`. O `else`, logo depois, roda quando ela é `false`.

```csharp
if (temperatura > 30)
{
    return "quente";
}
else
{
    return "fresco";
}
```

A condição é sempre um `bool`: uma comparação como `lux < 50`, uma variável `bool` ou um método que devolve `bool`.

Um `return` dentro do `if` já encerra o método. Por isso, depois de `if (...) return true;`, a linha de baixo só roda quando a condição foi falsa.

Cuidado com o ponto e vírgula: `if (lux < 50);` termina o `if` ali mesmo, e o bloco de baixo roda sempre.
