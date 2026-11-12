## 1 · A ideia

Os dois métodos de hoje trocaram o "e" pelo "ou", cada um de um jeito:

- "Das 18h até antes das 6h" atravessa a meia-noite. Nenhuma hora é ao mesmo tempo 18 ou mais **e** menor que 6, então a pergunta de hoje nunca é verdadeira. A hora perigosa é a que é 18 ou mais, **ou** a que é menor que 6.
- O portão só pode abrir quando as duas coisas valem juntas: a combinação certa **e** não ser hora perigosa. Com "ou", basta uma delas, e de dia qualquer combinação abre.

`Status` escolhe entre três respostas, e a ordem das perguntas decide. A hora perigosa vem primeiro, porque nem a combinação certa abre de noite. Só depois se olha a combinação.

## 2 · Os comandos

- `&&` (e): as duas condições precisam ser verdadeiras. `||` (ou): basta uma. `!` (não) inverte.
- Textos se comparam com `==` (igual) e `!=` (diferente): `code != "3107"`.
- `if`, `else if`, `else`: as perguntas são feitas na ordem, e a primeira verdadeira vence.
- `Status` pode chamar o `IsDangerousHour` que você conserta.

Um exemplo de padaria, que não é a resposta:

```csharp
static string Padaria(int hora, bool feriado)
{
    if (feriado)
    {
        return "FECHADA";
    }
    else if (hora >= 22 || hora < 4)
    {
        return "TURNO DA NOITE";
    }
    else
    {
        return "ABERTA";
    }
}
```

## 3 · O esqueleto

Troque cada `___`:

```csharp
// IsDangerousHour
return hour >= 18 ___ hour < 6;

// CanOpen
return code == "3107" ___ !IsDangerousHour(hour);

// Status
if (IsDangerousHour(___))
{
    return "DANGEROUS HOUR";
}
else if (code ___ "3107")
{
    return "WRONG CODE";
}
else
{
    return ___;
}
```
