# Noite 4 · A tranca do portão

O portão de ferro é a segunda defesa. Pus nele uma tranca elétrica com combinação, para ninguém entrar sem eu saber. E mandei o sistema nunca abrir de noite, nem com a combinação certa.

Alguém mexeu neste código. Hoje o portão abre com qualquer combinação, e a hora perigosa nunca começa. Conserte a classe `GateLock`:

| Método | O que devolve | Exemplo |
| --- | --- | --- |
| `IsDangerousHour(hour)` | Se é hora perigosa: das 18h até antes das 6h | `IsDangerousHour(22)` devolve `true` |
| `CanOpen(code, hour)` | Se o portão abre: a combinação `"3107"` e fora da hora perigosa | `CanOpen("3107", 10)` devolve `true` |
| `Status(code, hour)` | O que o painel do portão mostra | `Status("1234", 14)` devolve `"WRONG CODE"` |

O painel mostra `"DANGEROUS HOUR"` na hora perigosa, mesmo com a combinação certa. Fora dela, mostra `"WRONG CODE"` com a combinação errada e `"OPEN"` com a certa.

O código compila e roda. O defeito está na lógica, não na escrita.
