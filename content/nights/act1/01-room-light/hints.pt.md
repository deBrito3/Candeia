## 1 · A ideia

Cada método da classe `RoomLight` é uma pergunta que o sistema faz à lâmpada, e a resposta sai pelo `return`. A palavra escrita antes do nome do método diz que tipo de resposta o sistema espera:

- `int Watts()` espera um número inteiro, como `60`;
- `double Voltage()` espera um número que pode ter casas decimais, como `127.5`;
- `bool StartsOn()` espera um sim ou não: `true` (verdadeiro) ou `false` (falso);
- `string Model()` espera um texto, como `"ABC-1"`.

Hoje, `Voltage()` devolve `0`, `StartsOn()` devolve `false` e `Model()` devolve um texto vazio. As respostas certas estão na caixa da lâmpada, descrita no LEIA-ME.

O LEIA-ME também pede que cada resposta seja guardada numa variável antes de ser devolvida, do jeito que `Watts()` já faz. Por isso devolver o valor direto, como `return true;`, acende a lâmpada, mas não vence a noite.

## 2 · Os comandos

Uma variável é uma caixa com nome que guarda um valor. Ela é criada numa linha só: o tipo, o nome, o sinal `=`, o valor e um `;` no fim. Depois, `return` devolve o que está guardado nela.

Um exemplo com outros valores, que não são os da lâmpada:

```csharp
int portas = 3;          // número inteiro
double peso = 1.5;       // casas decimais: use ponto, nunca vírgula
bool aberta = true;      // só true ou false, sem aspas
string cor = "azul";     // texto sempre entre aspas duplas
return peso;             // devolve o valor guardado em peso
```

Erros comuns:

- `220,0` com vírgula não compila: em C#, a casa decimal usa ponto;
- `"true"` com aspas vira texto, e não um `bool`;
- texto sem aspas, como `CND-60`, o C# lê como nomes e contas, e não como texto;
- faltar o `;` no fim da linha.

## 3 · O esqueleto

Troque cada `___` pelo valor da caixa da lâmpada:

```csharp
public static double Voltage()
{
    double volts = ___;    // a tensão da tomada, com casa decimal
    return volts;
}

public static bool StartsOn()
{
    bool on = ___;         // a lâmpada começa acesa?
    return on;
}

public static string Model()
{
    string model = ___;    // o modelo, entre aspas duplas
    return model;
}
```
