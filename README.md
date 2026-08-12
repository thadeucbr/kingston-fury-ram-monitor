# RAM LED monitor

## O que ele faz

- Monitora o uso de RAM do Windows;
- Divide o uso em 12 níveis verticais;
- Acende os quatro sticks simultaneamente, de baixo para cima;
- Usa até 12 LEDs por stick;
- Muda a cor conforme o uso:
  - 0–50%: verde → amarelo;
  - 50–100%: amarelo → vermelho;
- Controla Kingston FURY diretamente, sem OpenRGB ou outro controlador RGB.

Com 24% de RAM, por exemplo:

```text
3 LEDs acesos em cada stick
```

## Estrutura do projeto

```text
src/
  RamFuryMonitor.cs          motor do efeito e bridge FURY
  MonitorConfig.cs           configuração persistente
  RamFuryWindowsService.cs   host do serviço Windows
  RamFuryTray.cs             painel da bandeja e editor visual
scripts/
  build.cmd                  compilação local
  install-service.cmd        instalação idempotente
  uninstall-service.cmd      remoção do serviço e startup
build/                       artefatos locais ignorados pelo Git
docs/                        documentação de produto
```

O FURY CTRL precisa continuar instalado porque fornece o serviço, DLLs e drivers SMBus. A interface do FURY CTRL não precisa ficar aberta.

## Tray app

A tray app permite:

- ativar/desativar o efeito;
- editor de cor HSV/RGB/HEX;
- preview da cor;
- paletas built-in traffic, verde, azul, roxo e Old Gold (`#8C5000`);
- paletas personalizadas salvas em `config.json`;
- slider contínuo de brilho, incluindo brilho original;
- abrir a pasta de logs;
- fechar apenas o painel, sem parar o serviço.

As configurações ficam em:

```text
C:\ProgramData\RamFuryMonitor\config.json
```

Exemplo:

```json
{"enabled":true,"palette":"traffic"}
```

## Instalação definitiva

Execute como administrador:

```text
scripts\install-service.cmd
```

O instalador:

- copia os binários para `C:\ProgramData\RamFuryMonitor`;
- cria o serviço `RamFuryMonitor` com inicialização automática;
- configura reinício após falhas;
- registra a tray app para iniciar com o usuário;
- inicia o serviço.

Log:

```text
C:\ProgramData\RamFuryMonitor\monitor.log
```

Remoção:

```text
scripts\uninstall-service.cmd
```

Também deve ser executado como administrador.

## Execução manual

Dry-run, sem escrever LEDs:

```text
RamFuryMonitor.exe
```

Live:

```text
RamFuryMonitor.exe --live
```

Serviço:

```text
RamFuryMonitor.exe --service
```

## Compilação

Os binários são compilados somente com o .NET Framework já instalado:

```text
scripts\build.cmd
```

Os fontes ficam em `src\` e os artefatos locais em `build\`; binários compilados não entram no Git.

## Identidade visual

Assets oficiais:

```text
assets/branding/ram-fury.ico
assets/branding/ram-fury-icon.png
assets/branding/ram-fury-installer-banner.png
```

O `.ico` contém múltiplas resoluções para tray e executáveis. O banner fica reservado para a interface do instalador profissional.

## Instalador gráfico

Para gerar o pacote completo:

```text
scripts\package.cmd
```

Saída:

```text
dist\RamFurySetup\RamFurySetup.exe
dist\RamFurySetup.zip
```

O instalador gráfico:

- exige elevação administrativa via manifest do Windows;
- mostra o banner e o status do bundle;
- verifica se o endpoint local do FURY CTRL está acessível;
- instala ou atualiza o serviço;
- instala a tray e o ícone oficial;
- registra a inicialização automática;
- preserva configurações e logs;
- remove serviço e startup sem apagar dados do usuário.

## Pré-requisitos

- Windows 10/11;
- .NET Framework 4.x já disponível no Windows;
- Kingston FURY CTRL instalado;
- serviço oficial do FURY CTRL ativo;
- acesso administrativo apenas durante a instalação.

A aplicação conversa somente com o serviço local oficial da Kingston. Não instala OpenRGB, drivers RGB alternativos ou serviços de terceiros.
