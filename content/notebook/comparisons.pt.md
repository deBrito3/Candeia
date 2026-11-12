# Comparações: maior, menor, igual

Uma comparação responde sim ou não: o resultado é um `bool`.

| Operador | Pergunta | Verdadeiro, por exemplo |
| --- | --- | --- |
| `==` | é igual? | `5 == 5` |
| `!=` | é diferente? | `5 != 3` |
| `<` | é menor? | `3 < 5` |
| `>` | é maior? | `5 > 3` |
| `<=` | é menor ou igual? | `5 <= 5` |
| `>=` | é maior ou igual? | `6 >= 5` |

`=` guarda um valor e `==` compara. `if (hora = 4)` não compila em C#: o certo é `if (hora == 4)`.

Atenção às bordas. "A partir das 22h" é `hora >= 22`, e "antes das 4h" é `hora < 4`. Um teste com a hora exata da borda mostra qual dos dois você escreveu.

Textos também se comparam com `==`: `senha == "abc"` só é verdadeiro com as mesmas letras, maiúsculas e minúsculas iguais.
