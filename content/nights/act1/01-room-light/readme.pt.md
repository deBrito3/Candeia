# Noite 1 · A luz do quarto

Se você está lendo isto, a luz do quarto apagou de novo. Foi a primeira coisa que eu liguei ao sistema, e é a primeira que ele esquece.

O CANDEIA faz quatro perguntas à lâmpada. Cada pergunta é um método da classe `RoomLight`, e cada resposta precisa ser do tipo certo:

| Método | Pergunta | Tipo da resposta |
| --- | --- | --- |
| `Watts()` | Quantos watts a lâmpada usa? | `int`, um número inteiro |
| `Voltage()` | Qual é a tensão da tomada? | `double`, um número que pode ter casas decimais |
| `StartsOn()` | A lâmpada começa acesa? | `bool`, verdadeiro (`true`) ou falso (`false`) |
| `Model()` | Qual é o modelo da lâmpada? | `string`, um texto entre aspas |

A caixa da lâmpada ficou em cima da mesa: **60 watts, 220 volts, começa acesa, modelo CND-60**.

`Watts()` ainda funciona. Faça os outros três do mesmo jeito: guarde a resposta numa variável do tipo certo e devolva essa variável com `return`.

Cada resposta certa acende uma lâmpada. Não deixe a casa no escuro.
