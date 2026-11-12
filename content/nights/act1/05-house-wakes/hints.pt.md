## 1 · A ideia

Cada método usa uma coisa das noites anteriores:

- `TotalLoad` é uma soma de três números.
- `Usage` é uma divisão que precisa das casas decimais, como na bateria: entre dois `int`, 1 de 8 daria `0`, e não `12.5`.
- `CanTurnOn` é uma comparação: a carga de agora mais a nova não pode passar da capacidade. Chegar exatamente nela ainda cabe.
- `NextCircuit` é uma escada de perguntas, como no portão, na ordem do quadro. A primeira pergunta junta duas condições: estar chovendo **e** a bomba caber. Se chove mas a bomba não cabe, a escada desce para a geladeira.

`NextCircuit` pode usar o `CanTurnOn` que você acabou de escrever, uma vez com os watts da bomba e outra com os da geladeira.

## 2 · Os comandos

- Porcentagem com casas decimais: converta antes de dividir, `(double)parte * 100 / todo`.
- `<=` é "menor ou igual": é ele que diz que chegar exatamente na capacidade ainda cabe.
- `if`, `else if`, `else`, com `&&` para juntar duas condições numa pergunta só.

Um exemplo de mochila, que não é a resposta:

```csharp
static string Mochila(int peso, int limite, bool viagemLonga)
{
    if (viagemLonga && peso + 2 <= limite)
    {
        return "LEVA O CASACO";
    }
    else if (peso + 1 <= limite)
    {
        return "LEVA A GARRAFA";
    }
    else
    {
        return "NADA MAIS";
    }
}
```

## 3 · O esqueleto

Troque cada `___`:

```csharp
// TotalLoad
return lights ___ fridge ___ pump;

// Usage
return (double)load * ___ / capacity;

// CanTurnOn
return load + extra ___ capacity;

// NextCircuit
if (raining ___ CanTurnOn(load, ___, capacity))
{
    return "PUMP";
}
else if (CanTurnOn(load, ___, capacity))
{
    return "FRIDGE";
}
else
{
    return ___;
}
```
