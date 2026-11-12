# Noite 5 · A casa acorda

O temporal derrubou a energia, como derruba todo ano. Quando a casa apaga de uma vez, ele não espera: vem direto. Hoje ele chega à janela. Não fique olhando para ela.

As luzes aguentam na bateria que você consertou. O resto da casa espera no quadro de energia, um circuito de cada vez, sem passar da carga que a entrada aguenta. Conserte a classe `FuseBox`:

| Método | O que devolve | Exemplo |
| --- | --- | --- |
| `TotalLoad(lights, fridge, pump)` | A carga total, em watts | `TotalLoad(100, 300, 800)` devolve `1200` |
| `Usage(load, capacity)` | Quanto da capacidade a carga usa, em porcentagem com casas decimais | `Usage(500, 2000)` devolve `25.0` |
| `CanTurnOn(load, extra, capacity)` | Se cabem mais `extra` watts sem passar da capacidade | `CanTurnOn(1000, 800, 2000)` devolve `true` |
| `NextCircuit(load, capacity, raining)` | O próximo circuito a religar | `NextCircuit(500, 2000, true)` devolve `"PUMP"` |

A ordem do quadro: com chuva, primeiro a bomba d'água (`"PUMP"`, 800 W), se couber. Senão, a geladeira (`"FRIDGE"`, 300 W), se couber. Senão, `"WAIT"`. Chegar exatamente na capacidade ainda cabe.

Esta é a noite do cerco: mais tempo, e tudo o que você aprendeu até aqui.
