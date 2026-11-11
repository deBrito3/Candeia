# Noite 2 · A bateria do sistema

Quando falta energia, e no inverno falta muito, a casa inteira depende desta bateria. Se o painel diz que ela está vazia, o sistema desliga as luzes para economizar. E sem luz ele não tem medo de chegar.

O painel mostra 0% mesmo com a bateria cheia. Conserte as contas da classe `Battery`:

| Método | O que devolve | Exemplo |
| --- | --- | --- |
| `Percent(charge, capacity)` | A carga em porcentagem inteira, arredondada para baixo | `Percent(30, 120)` devolve `25` |
| `Hours(charge, load)` | Quantas horas a bateria aguenta: a carga dividida pelo consumo, com casas decimais | `Hours(100, 40)` devolve `2.5` |
| `Minutes(hours)` | As horas em minutos inteiros, arredondando para o minuto mais próximo | `Minutes(1.5)` devolve `90` |
| `LeftoverMinutes(minutes)` | Os minutos que sobram depois das horas inteiras | `LeftoverMinutes(125)` devolve `5` |

Cuidado com a divisão: entre dois `int`, ela joga fora as casas decimais.
