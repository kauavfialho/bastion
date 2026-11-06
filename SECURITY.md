# Política de segurança

## Versões com suporte

Só a versão mais recente recebe correções de segurança. Antes de relatar um problema, confira se ele também acontece nela.

## Como relatar uma falha

Não abra uma issue pública, porque isso expõe o problema antes da correção.

1. Abra a aba **Security** deste repositório.
2. Clique em **Report a vulnerability**.
3. Explique o que acontece, como repetir o problema, qual versão do Windows você usa e qual é o impacto.

Não envie dados pessoais, senhas nem arquivos do seu computador. Se precisar mostrar algo, use prints sem informações pessoais.

O projeto é mantido por uma pessoa, em tempo livre. Não há prazo garantido de resposta, mas todo relato será lido. Depois da correção, a falha pode ser divulgada, com crédito a quem relatou, se a pessoa quiser.

## O que entra aqui

- Uma função do Bastion que apaga ou muda algo sem pedir confirmação.
- Download ou instalação de programa sem a conferência do arquivo (SHA-256 ou SHA-512).
- Um jeito de outro programa usar o Bastion para ganhar permissão de administrador.

## Como o Bastion protege o seu computador

- Não há conta, login ou servidor. Nada sobre o seu computador sai dele.
- O app abre sem permissão de administrador. Funções que precisam dela pedem antes, uma a uma.
- Tudo que apaga ou muda o sistema pede confirmação. Quando dá, o Bastion usa a Lixeira, pontos de restauração ou o mesmo controle do Gerenciador de Tarefas, para que a mudança possa ser desfeita.
- Programas são instalados pelo winget, pelo Ninite ou pelos sites oficiais. O ArtCraft Launcher é conferido pelo SHA-256 publicado pelo próprio projeto.
- A internet é usada só para instalar programas, testar a conexão e abrir páginas de suporte.

## O que o Bastion não protege

- **Os instaladores não têm certificado pago.** Por isso, o Windows mostra um aviso na primeira vez. Baixe só pelas Releases oficiais deste repositório.
- **Ajustes de BIOS, XMP e overclock são por sua conta.** O Bastion só mostra o caminho; quem muda é você, dentro da BIOS.
