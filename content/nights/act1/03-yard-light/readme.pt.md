# Noite 3 · A luz do quintal

O quintal é o caminho dele até a casa. Enquanto a luz de lá está acesa, ele fica parado no escuro do portão, esperando. Eu sei, porque já vi.

Faz anos que essa luz não acende. Conserte a classe `YardLight`:

| Método | O que devolve | Exemplo |
| --- | --- | --- |
| `IsDark(lux)` | Se está escuro lá fora: menos de 50 lux | `IsDark(10)` devolve `true` |
| `ShouldLight(lux, hour)` | Se a luz deve acender: só no escuro, e nunca das 5h às 17h | `ShouldLight(10, 22)` devolve `true` e `ShouldLight(10, 12)` devolve `false` |
| `Label(on)` | O texto do painel: `"ON"` com a luz acesa, `"OFF"` com ela apagada | `Label(true)` devolve `"ON"` |

De dia ele não vem, e a bateria é pouca. Por isso, das 5h às 17h a luz fica apagada, mesmo num dia escuro de chuva. A partir das 18h, ela volta a acender no escuro. A hora vai de 0 a 23.

`IsDark` ainda funciona e mostra como o `if` decide. Faça os outros dois do mesmo jeito.
