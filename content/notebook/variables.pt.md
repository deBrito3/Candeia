# Variáveis: caixas com nome

Uma variável é uma caixa com nome onde você guarda um valor para usar depois. Toda caixa tem um tipo, e o tipo diz o que cabe nela.

```csharp
int portas = 3;
```

Leia da esquerda para a direita: o tipo (`int`), o nome (`portas`), o sinal `=` e o valor (`3`). O `=` não pergunta se é igual: ele guarda o valor na caixa. O `;` fecha a frase.

Depois, é só usar o nome: `portas + 1` vale 4. Escolha nomes que digam o que a caixa guarda; daqui a dez anos, quem for ler agradece. Foi o que eu tentei fazer neste sistema.

Com `var`, o C# descobre o tipo pelo valor: `var portas = 3;` também é um `int`.
