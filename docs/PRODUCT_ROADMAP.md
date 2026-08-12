# RAM FURY Monitor — product roadmap

## Objetivo

Evoluir o protótipo validado de controle da memória Kingston FURY para uma aplicação desktop nativa, configurável, instalável e extensível, mantendo o requisito de não usar controladores RGB externos.

## Fases

### Fase 1 — fundação segura

- serviço Windows estável;
- reconexão por sessão após cada escrita no FURY CTRL;
- configuração persistente versionada;
- logs e restauração do estado original;
- instalação/desinstalação idempotente.

### Fase 2 — UI profissional

- painel WinForms escuro e moderno;
- editor HSV/RGB/HEX;
- paletas salvas pelo usuário;
- slider contínuo de brilho;
- preview da cor e status do serviço;
- aplicar alterações sem reiniciar a tray.

### Fase 3 — efeitos e scripts

- catálogo de efeitos built-in;
- modelo de script declarativo, inicialmente JSON;
- passos, intervalos, transições e fontes de dados;
- sandbox de validação antes de aplicar;
- importação/exportação de scripts;
- só depois avaliar uma linguagem embutida, evitando executar código arbitrário por padrão.

### Fase 4 — distribuição

- bundle versionado;
- instalador com detecção do FURY CTRL e permissões administrativas;
- atualização sem deixar processos bloqueando arquivos;
- desinstalação completa;
- artefatos de release no GitHub.

## Decisões de segurança

- sem OpenRGB ou controlador RGB externo;
- o serviço oficial FURY CTRL continua sendo pré-requisito;
- scripts não executarão comandos do sistema por padrão;
- segredos/protocolos de teste e capturas não devem entrar em releases públicas;
- cada fase será compilada, executada e verificada antes do próximo commit.
