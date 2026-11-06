# Plano: Bastion para Mac e Linux

O Bastion de hoje é um app WPF, que só existe no Windows, e quase todas as funções usam ferramentas do próprio Windows: registro, winget, Windows Defender, PowerShell, `pnputil`, `powercfg`, ponto de restauração. Por isso, levar o Bastion para Mac e Linux não é só "compilar de novo": é fazer um segundo app, que divide o que dá para dividir.

## Como fazer

1. **Interface em Avalonia.** É parecido com o WPF (XAML e C#), roda em Windows, Mac e Linux e aceita o mesmo tema escuro. O código das telas (`Pages/`) e das peças (`Core/Ui.cs`) é adaptado, não reescrito do zero.
2. **Separar o núcleo do sistema.** Criar uma camada `Plataforma` com uma interface por função (`ISensores`, `ILimpeza`, `IInstalador`, `IInicializacao`…) e uma implementação para cada sistema. O Windows continua usando o que já existe hoje.
3. **Mesmo repositório, dois apps no começo.** O app WPF segue como está para o Windows até o Avalonia ter as mesmas funções; depois, o Windows também passa para o Avalonia e fica um app só.
4. **Releases no padrão do launcher.** `Bastion-mac-arm64.dmg`, `Bastion-mac-x64.dmg` e `latest-mac.yml`, com o app assinado ad-hoc (`codesign --sign -`), como o Syvo. Para Linux, `AppImage` (o launcher ainda não instala apps no Linux).

## O que dá para levar

| Tela | Mac | Linux | Como |
|---|---|---|---|
| Início (CPU, RAM, rede ao vivo) | Sim | Sim | `host_statistics`/`vm_stat` no Mac, `/proc` no Linux |
| Temperatura e uso da GPU | Parcial | Sim (NVIDIA/AMD) | `powermetrics` precisa de admin no Mac; `nvidia-smi`/`sysfs` no Linux |
| Limpeza | Sim | Sim | Caches do usuário, Lixeira, caches de navegador e as maiores pastas |
| Organizador de arquivos | Sim | Sim | Mesmo código, com a pasta de Downloads de cada sistema |
| Inicialização | Sim | Sim | Itens de Login (Mac) e `~/.config/autostart` (Linux) |
| Instalar apps | Sim | Sim | Homebrew Cask no Mac, Flatpak/apt no Linux; ArtCraft já tem dmg e AppImage |
| Testes (CPU, RAM, disco) | Sim | Sim | Mesmo código de `Core/Stress.cs`, só troca a leitura sem cache do disco |
| Meu PC | Sim | Sim | `system_profiler` no Mac, `lshw`/`lsusb`/`/sys` no Linux |
| Periféricos (luzes) | Sim | Sim | O OpenRGB roda nos três sistemas e o cliente do Bastion já é independente do Windows |
| Teste de conexão | Sim | Sim | Mesmo código |
| Aparência e idiomas | Sim | Sim | Mesmo código |

## O que não faz sentido fora do Windows

- **Saúde do PC (telas azuis, Defender, WHEA)**, **Debloat**, **Drivers**, **Jogos (Modo de Jogo, Game Bar, plano de energia)** e **Formatar PC** são do Windows. No Mac/Linux, entram versões próprias e menores: por exemplo, a saúde do disco pelo `smartctl` e um guia de reinstalação do sistema.

## Ordem sugerida

1. Camada `Plataforma` e o núcleo do Windows passando por ela (sem mudar nada para quem usa).
2. Projeto Avalonia com Início, Testes, Limpeza, Organizador, Aparência e idiomas.
3. Releases de Mac no padrão do launcher e o cartão do Bastion deixando de aparecer como "indisponível" no Mac.
4. Linux em AppImage.
5. Restante das telas, conforme cada sistema permitir.
