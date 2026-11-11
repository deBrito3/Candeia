# Candeia

Jogo de terror e programação para Windows. Você volta ao casarão da infância, no Recife, e sobrevive às noites escrevendo C# que compila e roda de verdade, enquanto o Papa-Figo se aproxima. Arte vetorial desenhada pelo próprio jogo e sons sintetizados em código, sem assets externos.

**Estado:** entrega 0 de 11. O repositório tem a estrutura, uma janela vazia do sistema CANDEIA e o executor de testes. Ainda não há jogo.

## Requisitos

- Windows 10 ou 11, x64.
- SDK do .NET 10. A versão mínima está em `global.json`. Para instalar exatamente essa versão sem mexer no SDK da máquina, rode `./scripts/setup-sdk.ps1`.

## Comandos

```powershell
./scripts/test.ps1                                # compila tudo e roda as suítes de teste
./scripts/run.ps1                                 # abre o jogo
./scripts/run.ps1 --shot test-artifacts/boot.png  # salva a tela inicial numa imagem
```

## Estrutura

| Pasta | Conteúdo |
| --- | --- |
| `src/Candeia.Core/` | Regras, desafios e execução do código do jogador. Não depende do Windows |
| `src/Candeia.Windows/` | O executável do jogo (Windows Forms) |
| `tests/Candeia.Tests/` | Suítes de teste por linha de comando, sem framework de testes |
| `scripts/` | SDK isolado, testes e execução |
| `licenses/` | Licenças de componentes de terceiros distribuídos com o jogo |

Todo o código, inclusive nomes e comentários, é escrito em inglês. A interface, o caderno e as explicações ao jogador ficam em português e inglês.
