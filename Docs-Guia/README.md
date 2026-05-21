# 📖 Guia Simples do Projeto — Explicado como se fosses uma Criança

Este guia explica **cada ficheiro e cada pasta** do projeto, numa linguagem simples.
Imagina que este projeto é como um **robot que lê mensagens** no WhatsApp e no Teams, e responde automaticamente!

---

## 📁 Raiz do Projeto (`Estagio_ELO/`)

| Ficheiro / Pasta | O que faz? |
|---|---|
| `Estagio_ELO.sln` | É como a **capa do livro** — diz ao computador que isto é um projeto .NET e onde estão todas as partes. |
| `.gitignore` | Uma **lista de coisas para ignorar** — diz ao Git para não guardar ficheiros temporários (como lixo do computador). |
| `README.md` | O **manual de instruções** principal — explica o que o projeto faz e como usá-lo. |
| `HOW_TO_USE.md` | Um **guia passo-a-passo** de como configurar e usar cada funcionalidade. |
| `SECURITY.md` | Explica **todas as proteções de segurança** — como fechaduras numa porta para que ninguém entre sem permissão. |
| `TEST_REPORT.md` | O **relatório dos testes** — mostra se tudo está a funcionar bem (como um médico que verifica a saúde do projeto). |
| `RESUMO_COMPLETO.md` | Um **resumo de TUDO** — é como o índice detalhado de um livro. |
| `PROBLEMAS_REGISTADOS.md` | Um **diário de problemas** — todos os bugs que apareceram e como foram resolvidos. |

---

## 📁 `WebApplication1/` — O Código Principal

Esta é a pasta onde vive o "cérebro" do robot. Está organizada em 4 camadas, como uma **cebola** (Onion Architecture):

| Ficheiro / Pasta | O que faz? |
|---|---|
| `Program.cs` | O **botão de ligar** — é o ficheiro que arranca a aplicação e configura tudo (serviços, segurança, logging). |
| `WebApplication1.csproj` | A **lista de compras** — diz ao computador que pacotes (bibliotecas) precisa de instalar. |
| `appsettings.json` | As **definições gerais** — configurações públicas como versões de API e nomes de bot. |
| `appsettings.Development.json` | Definições para quando estamos a **programar em casa** (modo desenvolvimento). |
| `appsettings.Production.json` | Definições para quando o robot está a **trabalhar a sério** no servidor (modo produção — logs reduzidos). |
| `WebApplication1.http` | Ficheiro com **pedidos de teste** — como notas com perguntas de exemplo para enviar ao robot. |
| `Properties/launchSettings.json` | Diz ao robot **em que porta abrir** (tipo a porta 5197 do prédio). |

---

### 📁 `Api/` — A Porta de Entrada (Camada de Apresentação 🔵)

É como a **recepção** do edifício — recebe as mensagens e encaminha para quem sabe tratar delas.

#### 📁 `Api/Controllers/`

| Ficheiro | O que faz? |
|---|---|
| `WebhookController.cs` | O **recepcionista principal** — recebe mensagens do WhatsApp e do Teams e decide o que fazer com elas. |
| `HealthController.cs` | Um **medidor de saúde** — quando alguém pergunta "estás vivo?", ele responde "sim, estou saudável!" (`/health`). |

#### 📁 `Api/Middleware/`

Middlewares são como **guardas** que verificam todas as mensagens antes de chegarem ao recepcionista:

| Ficheiro | O que faz? |
|---|---|
| `CorrelationIdMiddleware.cs` | Dá um **número de rastreamento** a cada mensagem — como o número de encomenda nos correios, para podermos seguir tudo. |
| `ExceptionHandlingMiddleware.cs` | O **apanha-erros** — se alguma coisa correr mal, ele apanha o erro e devolve uma resposta bonita em vez de o robot rebentar. |
| `SecurityHeadersMiddleware.cs` | Adiciona **cabeçalhos de segurança** a todas as respostas — como colar autocolantes de proteção em todas as cartas que enviamos. |
| `ValidateTeamsJwtFilter.cs` | O **guarda do Teams** — verifica se a mensagem do Teams é mesmo do Teams e não de alguém a fingir (usa bilhetes de identidade JWT). |
| `ValidateWhatsAppSignatureFilter.cs` | O **guarda do WhatsApp** — verifica se a mensagem do WhatsApp é mesmo da Meta e não de alguém a fingir (usa assinaturas HMAC). |

---

### 📁 `Application/` — A Lógica de Orquestração (Camada de Aplicação 🟢)

| Ficheiro | O que faz? |
|---|---|
| `MessageProcessingService.cs` | O **maestro da orquestra** — coordena tudo: verifica spam, gere confirmações ("tens a certeza?"), e chama o comando certo. |
| `WebhookConcurrencyGuard.cs` | O **porteiro anti-spam** — faz deduplicação por `MessageId` (5 min) e lock por remetente (5s fallback). |

#### 📁 `Api/Middleware/` (filtros de webhook)

| Ficheiro | O que faz? |
|---|---|
| `WhatsAppConcurrencyGuardFilter.cs` | Filtra cedo mensagens duplicadas/spam e só deixa passar as aceites para o controller. |

---

### 📁 `Core/` — O Coração (Camada de Domínio 🟡)

É o **coração** do robot — contém as regras de negócio e não depende de nada externo.

#### 📁 `Core/Commands/`

| Ficheiro | O que faz? |
|---|---|
| `ICommandHandler.cs` | O **contrato** — uma interface que diz "qualquer comando tem de saber responder a CanHandle e ExecuteAsync". |
| `CommandRouter.cs` | O **carteiro** — recebe a mensagem e entrega ao comando certo (presença, ajuda, etc.). |
| `HelpCommandHandler.cs` | O comando **"ajuda"** — quando alguém escreve "ajuda" ou "help", ele responde com a lista de comandos. |
| `PresencaCommandHandler.cs` | O comando **"presença"** — quando alguém escreve "presente" ou "cá estou", regista a presença (aceita 15 formas diferentes!). |

#### 📁 `Core/Interfaces/`

| Ficheiro | O que faz? |
|---|---|
| `IMessagingService.cs` | O **contrato de mensagens** — diz que qualquer serviço de mensagens (WhatsApp, Teams) tem de saber enviar texto e marcar como lido. |
| `IBusinessApiClient.cs` | O **contrato com o servidor de negócio** — diz que qualquer cliente da API tem de saber registar presença, obter info e verificar disponibilidade. |

#### 📁 `Core/Models/`

| Ficheiro | O que faz? |
|---|---|
| `BusinessApiResult.cs` | O **resultado da API** — como um envelope de resposta que diz "correu bem", "deu timeout", "servidor indisponível", etc. |
| `IncomingMessage.cs` | O **modelo de mensagem** — guarda tudo sobre uma mensagem recebida: quem enviou, o texto, a plataforma, o UserId e UserName. |
| `MessagePlatform.cs` | Uma **etiqueta** — um enum que diz se a mensagem veio do WhatsApp (0) ou do Teams (1). |

#### 📁 `Core/Exceptions/`

| Ficheiro | O que faz? |
|---|---|
| `ApplicationExceptions.cs` | As **sirenes de alarme** — 4 tipos de erro especiais (comando inválido, webhook falhou, configuração errada, erro ao processar). |

---

### 📁 `Infrastructure/` — As Ligações ao Mundo Exterior (Camada de Infraestrutura 🔴)

É como os **cabos e fios** que ligam o robot ao WhatsApp, Teams e ao servidor de negócio.

#### 📁 `Infrastructure/Configuration/`

| Ficheiro | O que faz? |
|---|---|
| `BusinessApiSettings.cs` | As **definições do servidor de negócio** — BaseUrl, timeout de 30s, máximo de 3 tentativas. |
| `ConfigurationValidator.cs` | O **inspetor** — verifica se todas as passwords e tokens estão configurados antes de ligar o robot (para não arrancar sem proteção). |
| `WhatsAppSettings.cs` | As **definições do WhatsApp** — token de acesso, secret da app, versão da API, etc. |
| `TeamsSettings.cs` | As **definições do Teams** — BotId, ClientSecret, TenantId, etc. |

#### 📁 `Infrastructure/Messaging/`

| Ficheiro | O que faz? |
|---|---|
| `WhatsAppService.cs` | O **mensageiro do WhatsApp** — sabe enviar mensagens para o WhatsApp usando a API da Meta. |
| `TeamsService.cs` | O **mensageiro do Teams** — sabe enviar mensagens para o Teams usando o Bot Framework (com autenticação OAuth2). |
| `MessagingServiceFactory.cs` | A **fábrica de mensageiros** — cria o mensageiro certo dependendo da plataforma (WhatsApp ou Teams). |
| `TeamsActivity.cs` | Os **modelos do Teams** — classes que representam as mensagens do Bot Framework (Activity, ChannelAccount, etc.). |

#### 📁 `Infrastructure/ExternalApis/`

| Ficheiro | O que faz? |
|---|---|
| `LanguageDetector.cs` | O **detetor de línguas** — responsável por ler as mensagens ou o indicativo do WhatsApp para deduzir a língua falada (PT, EN, ES, FR). |

#### 📁 `Infrastructure/Logging/`

| Ficheiro | O que faz? |
|---|---|
| `ConsoleLogger.cs` | O **pintor de logs** — mostra mensagens bonitas e coloridas no terminal (caixas com emojis 📨📤, cores verde/azul/roxo). |

---

### 📁 `logs/` — Diário do Robot

| Ficheiro | O que faz? |
|---|---|
| `webhook-YYYYMMDD.txt` | Os **diários** — cada dia o robot escreve um ficheiro novo com tudo o que aconteceu (mensagens recebidas, erros, etc.). |

---

## 📁 `WebApplication1.Tests/` — Os Testes (Verificação de Saúde)

Esta pasta contém **testes automáticos** que verificam se tudo funciona (o total evolui ao longo do projeto). É como um **médico** que examina cada parte do robot.

| Ficheiro | O que faz? |
|---|---|
| `LanguageDetectorTests.cs` | Testa se o detetor de línguas consegue deduzir inglês do prefixo +44 e português de comandos portugueses. |
| `CommandRouterTests.cs` | Testa se o **carteiro** entrega as mensagens ao comando certo. |
| `HelpCommandHandlerTests.cs` | Testa se o comando **"ajuda"** responde bem a todas as formas de pedir ajuda. |
| `PresencaCommandHandlerTests.cs` | Testa se o comando **"presença"** reconhece as 15 formas de dizer "presente". |
| `TextNormalizationTests.cs` | Testa se a **limpeza de texto** funciona — remove emojis, pontuação, e converte para minúsculas. |
| `IncomingMessageTests.cs` | Testa se o **modelo de mensagem** guarda bem o UserId, UserName, Body, etc. |
| `MessagePlatformTests.cs` | Testa se as **etiquetas** WhatsApp e Teams funcionam como enum. |
| `MessagingServiceFactoryTests.cs` | Testa se a **fábrica** cria os mensageiros corretamente. |
| `WhatsAppSettingsTests.cs` | Testa se as **definições do WhatsApp** têm todas as propriedades esperadas. |
| `TeamsSettingsTests.cs` | Testa se as **definições do Teams** têm valores default corretos. |
| `TeamsActivityTests.cs` | Testa se os **modelos do Teams** (Activity, ChannelAccount) funcionam e deserializam JSON. |
| `TeamsConfigurationValidatorTests.cs` | Testa se o **inspetor do Teams** deteta configurações em falta. |
| `CustomExceptionsTests.cs` | Testa se as **sirenes de alarme** (exceções) funcionam com mensagens e InnerException. |
| `ConfigurationValidatorTests.cs` | Testa se o **inspetor do WhatsApp** deteta configurações em falta. |
| `ConfirmationEdgeCasesTests.cs` | Testa **casos estranhos** de confirmação (sim/não, tokens especiais, sem confirmação pendente). |
| `UserProcessingLockTests.cs` | Testa se o **lock por utilizador** funciona (1 msg de cada vez, unlock após resposta, multi-utilizador). |
| `ContextualConfirmationTests.cs` | Testa se as **confirmações mencionam o comando** ("Confirmas *presença*?"). |
| `ValidateTeamsJwtFilterTests.cs` | Testa se o **guarda do Teams** aceita/rejeita tokens JWT corretamente. |
| `ValidateWhatsAppSignatureFilterTests.cs` | Testa se o **guarda do WhatsApp** aceita/rejeita assinaturas HMAC corretamente. |
| `ExceptionHandlingMiddlewareTests.cs` | Testa se o **apanha-erros** devolve respostas diferentes em dev vs produção. |
| `TEST_COVERAGE_SUMMARY.md` | Um **relatório** que lista todos os testes e o que cada um cobre. |

### 📁 `Integration/` — Testes que Simulam o Mundo Real

| Ficheiro | O que faz? |
|---|---|
| `CustomWebApplicationFactory.cs` | A **fábrica de testes** — cria uma versão de teste da aplicação com mocks (simulações). |
| `WhatsAppIntegrationTests.cs` | Simula **14 cenários WhatsApp** reais — como se estivéssemos a enviar mensagens de verdade. |
| `TeamsIntegrationTests.cs` | Simula **6 cenários Teams** reais — como se o bot estivesse a receber mensagens do Teams. |
| `HealthIntegrationTests.cs` | Simula **4 cenários de health check** — verifica que o endpoint `/health` funciona. |
| `SecurityHeadersIntegrationTests.cs` | Simula **10 cenários de segurança** — verifica que todos os headers OWASP estão presentes. |

---

## 📁 `Architecture-UML/` — Os Desenhos do Projeto

Diagramas que mostram **como o projeto está organizado**, como plantas de um edifício:

| Ficheiro | O que faz? |
|---|---|
| `00_README.txt` | Instruções sobre como ver os diagramas. |
| `01_C4_Level1_Context.puml` | O **mapa geral** — mostra o sistema visto de longe (quem fala com quem: utilizadores, WhatsApp, Teams, servidor). |
| `logical-view.puml` | As **4 camadas da cebola** — mostra como o código está organizado (Core → Application → Infrastructure → Api). |
| `physical-view.puml` | O **mapa físico** — mostra onde cada peça vive (servidor, internet, utilizador). |
| `use-cases.puml` | Os **casos de uso** — mostra o que o utilizador pode fazer (marcar presença, pedir ajuda, etc.). |
| `sequence-whatsapp.puml` | O **caminho da mensagem WhatsApp** — passo a passo, desde que envias "presente" até receberes a resposta. |
| `sequence-teams.puml` | O **caminho da mensagem Teams** — passo a passo, desde que escreves no Teams até receberes a resposta. |
| `sequence-security.puml` | O **caminho da segurança** — mostra como os guardas (HMAC + JWT) verificam cada mensagem. |
| `state-confirmation.puml` | Os **estados de confirmação** — mostra os passos de "sim/não" (aguardar → confirmar → executar ou cancelar). |
| `activity-processing.puml` | O **fluxo de processamento** — o caminho completo que uma mensagem percorre dentro do robot. |
| `classes-commands.puml` | Os **desenhos das classes** — mostra como CommandRouter, HelpHandler e PresencaHandler se ligam. |
| `components-middleware.puml` | Os **componentes do pipeline** — mostra a ordem dos guardas e filtros (Exception → SecurityHeaders → Correlation → RateLimit → Controller). |

---

## 📁 Pastas Geradas Automaticamente (podem ser ignoradas)

| Pasta | O que faz? |
|---|---|
| `bin/` | Onde ficam os **ficheiros compilados** — o computador transforma o código em ficheiros que pode executar. |
| `obj/` | Ficheiros **temporários de compilação** — lixo técnico que o computador precisa enquanto constrói o projeto. |

---

## 🎯 Resumo em 3 Frases

1. **O que faz?** É um robot que recebe mensagens no WhatsApp e no Teams, percebe o que disseste ("presente", "ajuda") e responde automaticamente.
2. **Como está organizado?** Em 4 camadas como uma cebola — o coração (Core) não depende de nada, e as camadas externas tratam de WhatsApp, Teams e segurança.
3. **Como sabemos que funciona?** Tem uma suite de testes automáticos, 11 diagramas UML e proteções de segurança em várias camadas.

---

*Gerado automaticamente — Versão 8.x (anti-spam WhatsApp Web atualizado)*
