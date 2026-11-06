# Como contribuir com o Bastion

App WPF em C# (.NET 8, `net8.0-windows`), com interface em português, inglês e espanhol. Mais de uma pessoa trabalha neste repositório, cada uma com o próprio assistente de IA.

## Estrutura

- `App.xaml`: tema (cores, estilos de botão, cartão, abas, interruptor, caixa de seleção). Use os estilos daqui.
- `MainWindow.xaml(.cs)`: barra lateral, seletor de idioma, navegação entre páginas e notificações (`ShowToast`).
- `Pages/`: uma classe por tela. Todas herdam `PageBase` e montam a interface em código com `Ui.*`.
  Para criar uma tela nova, crie a classe em `Pages/` e registre com `Add(...)` em `MainWindow.xaml.cs`.
- `Core/`: lógica reutilizável.
  - `Ui`: peças de interface (`Card`, `Btn`, `BtnAsync`, `KV`, `Switch`, `Check`, `Stat`, `Info`, `Confirm`, `NeedsAdmin`).
  - `I18n`: traduções. Todo texto da tela passa por `L("texto em português")`.
  - `Ps`: roda scripts do Windows PowerShell 5.1 e devolve JSON (`ObjAsync`, `ListAsync`).
  - `Winget`, `ArtCraft`: instalação de apps. `HwMonitor`: sensores ao vivo. `Organizer`/`TrayHost`: organizador.
  - `OpenRgb`, `VendorApps`: luzes e apps de periféricos. `BiosGuide`: ajustes e guia da BIOS.
  - `Admin`: detecção de administrador e reabrir elevado.
- `instalador/`: script do instalador do Windows (NSIS) e geração do `latest.yml`.

## Regras

- O texto original fica em português do Brasil, curto e explicando o porquê para leigos. Todo texto novo passa por `L(...)` e ganha tradução em inglês e espanhol em `Core/I18n.cs`.
- O app abre sem admin (`asInvoker`). Controle que precisa de admin usa `Ui.NeedsAdmin(...)`, que oferece reabrir como administrador.
- Ações que apagam ou mudam o sistema sempre pedem `Ui.Confirm` antes e devem ser reversíveis quando possível (Lixeira, ponto de restauração, `StartupApproved` em vez de apagar entradas).
- Não adicione pacotes NuGet sem combinar antes; hoje o projeto não usa nenhum.
- Antes de commitar: `dotnet build` sem erros.
- Não publique anotações locais (`CLAUDE.md`), senhas, chaves ou dados pessoais.

## Fluxo de trabalho (Git)

- Commits em português, com o nome de quem fez, sem coautoria ou atribuição a uma IA.
- Não commite direto na `main`: uma branch por mudança, Pull Request, o outro revisa e faz o merge.
- `git pull` na `main` antes de criar a branch; commits pequenos.
- Para evitar conflitos, combinem quem mexe em qual tela (`Pages/*.cs`). `App.xaml`, `MainWindow.xaml.cs` e `Core/I18n.cs` são compartilhados: mudanças neles em PRs separados e rápidos.
- Não commite `bin/`, `obj/` ou `dist/` (já estão no `.gitignore`). Versões para usar vão em Releases do GitHub.
