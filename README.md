# Candeia

Jogo de terror e programação para Windows. Você volta ao casarão da infância, no Recife, e sobrevive às noites escrevendo C# que compila e roda de verdade, enquanto o Papa-Figo se aproxima. Arte vetorial desenhada pelo próprio jogo e sons sintetizados em código, sem assets externos.

**Estado:** entrega 4 de 11. O jogo verifica, compila e executa C# de verdade, num processo isolado; tem as noites 1 e 2 do Ato 1; e já tem as regras da noite (relógio elástico, penalidades, velas, marcas e o trajeto do Papa-Figo), conferidas em noites simuladas. Ainda não há tela de jogo.

## Requisitos

- Windows 10 ou 11, x64.
- SDK do .NET 10. A versão mínima está em `global.json`. Para instalar exatamente essa versão sem mexer no SDK da máquina, rode `./scripts/setup-sdk.ps1`.

## Comandos

```powershell
./scripts/test.ps1                                # compila tudo e roda as suítes de teste
./scripts/run.ps1                                 # abre o jogo
./scripts/run.ps1 --shot test-artifacts/boot.png  # salva a tela inicial numa imagem
./scripts/check.ps1                               # verifica as amostras de samples/csharp como uma noite faria
./scripts/check.ps1 meu-arquivo.cs --en           # verifica um arquivo, com as mensagens em inglês
./scripts/security-test.ps1                       # ataca o executor isolado e mostra o que cada camada bloqueou
./scripts/content-test.ps1                        # confere cada noite com o código inicial e a solução de referência
./scripts/simulate.ps1                            # joga noites roteirizadas pelas regras e mostra a linha do tempo
```

Para medir os tempos de compilação (a primeira compilação e a média das seguintes):

```powershell
./scripts/dotnet.ps1 run --project tests/Candeia.Tests -- --bench
```

## C# do jogador

O código do jogador passa por três filtros antes de ser compilado, e os erros chegam juntos, em ordem de linha:

1. **Restrições de linguagem:** `unsafe`, ponteiros, atributos, `extern`, `await`, `lock`, `typeof`, destrutores e nomes começados por `__` são recusados.
2. **Erros do compilador:** os mais comuns ganham explicação própria e, quando a intenção é clara, uma correção pronta. Os outros usam a mensagem oficial do compilador em português.
3. **Lista de APIs permitidas:** tipos simples, textos, `Math`, coleções, `StringBuilder` e `Console.Write`/`WriteLine`. Arquivos, rede, processos, reflexão, ambiente, threads, sorteio e relógio são recusados. O LINQ só abre no Ato 3.

Os códigos `CND` são do próprio jogo. Os códigos `CS` são do compilador C#.

## Execução isolada

O código aprovado é compilado com contadores de passos e marcadores, e roda noutro processo: o próprio executável, aberto com `--runner`. Esse processo:

- roda com **baixa integridade**: não consegue gravar nas pastas do usuário nem abrir o processo do jogo;
- fica num **Job Object**: morre junto com o jogo, não abre outros programas, tem memória (256 MB) e tempo de processador limitados, e não acessa área de transferência nem configurações da área de trabalho;
- herda só os próprios canais de comunicação e um ambiente mínimo;
- para sozinho ao passar do limite de passos (1 milhão por teste) ou quando a pilha está quase cheia, e o jogo o encerra se passar de 3 segundos.

A baixa integridade não impede ler arquivos, acessar a rede nem encerrar outros programas do usuário. Contra isso, a barreira é a lista de APIs permitidas. A suíte de ataques mostra cada caso.

## Noites

Cada noite é uma pasta em `content/nights/<ato>/<noite>/`:

| Arquivo | Conteúdo |
| --- | --- |
| `night.json` | A classe e os métodos que o sistema da casa chama, os testes (cada um uma lâmpada, visível ou oculto), os conceitos exigidos e os sustos roteirizados |
| `starter.cs` | O código que o jogador encontra no computador antigo |
| `readme.pt.md`, `readme.en.md` | O LEIA-ME deixado por quem escreveu o sistema |
| `hints.pt.md`, `hints.en.md` | As três camadas de dica (`## 1` a ideia, `## 2` os comandos, `## 3` um esqueleto com lacunas) |

## Regras da noite

Os valores ficam em `content/rules.json`: tempo por ato, parte do tempo devolvida pelas lâmpadas, custos da regra "a casa faz barulho", velas, respiro, dificuldades e as faixas das paradas (rua, portão, quintal, janela, porta, ao seu lado). Ajustar o equilíbrio do jogo é mudar esse arquivo.

- `NightClock` é o tempo elástico: corre só enquanto o jogador programa, para enquanto as lâmpadas acendem e no respiro, perde uma vela a cada vez que zera e termina em vitória ou captura, com as marcas Ileso, Sem doce e Distância.
- `PenaltyRules` calcula o que cada execução devolve ou custa.
- `CreatureDirector` decide em que parada ele está, se pode ser visto naquele ato, avisa com um som antes de cada chegada e dispara os sustos roteirizados das noites.

A solução de referência de cada noite fica em `tests/Candeia.Tests/Solutions/<id>.cs` e não vai no jogo. Os conceitos exigidos podem ser uma construção que precisa rodar (`if`, `for`, `foreach`, `while`, `do`, `switch`, `conditional`), uma variável de um tipo (`declare:double`) ou um operador (`operator:%`).

## Estrutura

| Pasta | Conteúdo |
| --- | --- |
| `src/Candeia.Core/` | Regras, desafios e execução do código do jogador. Não depende do Windows |
| `src/Candeia.Windows/` | O executável do jogo (Windows Forms) |
| `tests/Candeia.Tests/` | Suítes de teste por linha de comando, sem framework de testes |
| `content/` | As noites e o restante do conteúdo do jogo, em arquivos de dados |
| `samples/csharp/` | Programas de exemplo, certos e errados, para conferir o verificador de C# |
| `scripts/` | SDK isolado, testes, verificação de C# e execução |
| `licenses/` | Licenças de componentes de terceiros distribuídos com o jogo |

Todo o código, inclusive nomes e comentários, é escrito em inglês. A interface, o caderno e as explicações ao jogador ficam em português e inglês.
