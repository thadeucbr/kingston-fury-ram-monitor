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

## Componentes

```text
RamFuryMonitor.exe  → serviço Windows e motor do efeito
RamFuryTray.exe     → painel na bandeja do Windows
FuryControllerService.exe → serviço oficial Kingston
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
install-service.cmd
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
uninstall-service.cmd
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
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:RamFuryMonitor.exe /reference:System.Web.Extensions.dll /reference:System.Net.Http.dll /reference:System.ServiceProcess.dll RamFuryMonitor.cs
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:RamFuryTray.exe /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll RamFuryTray.cs
```

## Protocolo validado

```text
Endpoint: ws://127.0.0.1:55599/
Origin: ksws-dramledctrl://5E7EFB96-6632-40D5-882F-51CE1E62CA3F
RijndaelManaged: BlockSize 256, CBC, PKCS7
Rfc2898DeriveBytes: 1000 iterações, chave de 32 bytes
Wire format: Base64(salt de 32 + IV de 32 + ciphertext)
```
