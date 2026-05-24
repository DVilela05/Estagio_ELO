╔══════════════════════════════════════════════════════════╗
║       DIAGRAMAS UML — GATEWAY WEBHOOK ASSIDUIDADE       ║
╚══════════════════════════════════════════════════════════╝

  Para visualizar os ficheiros .puml:
  • VS Code: Instalar extensão "PlantUML" (jebbs) → Alt+D
  • Online:  http://www.plantuml.com/plantuml/

══════════════════════════════════════════════════════════

  01_caso_de_uso.puml
     → Diagrama de Casos de Uso
       Atores: Colaborador, WhatsApp API, Teams API, ELO WCF
       Casos: Marcar Presença, Listar Marcações, Ajuda

  02_modelo_dominio.puml
     → Modelo de Domínio
       Classes: IncomingMessage, PendingConfirmation,
       PendingPeriodState, BusinessApiResult, Booking (ELO)

  03_vista_logica_n1.puml
     → Vista Lógica Nível 1 (Diagrama de Contexto)
       Visão de alto nível: sistemas externos ↔ Gateway

  04_vista_logica_n2.puml
     → Vista Lógica Nível 2 (Diagrama de Containers)
       Camadas internas: Api, Application, Core, Infrastructure

  05_vista_logica_n3_processamento.puml
     → Vista Lógica Nível 3 (Fluxo de Processamento)
       Detalhe do MessageProcessingService (anti-spam,
       confirmações, routing)

  06_vista_logica_n3_seguranca.puml
     → Vista Lógica Nível 3 (Pipeline de Segurança)
       Middleware global + filtros por plataforma

  07_sequencia_marcar_presenca.puml
     → Diagrama de Sequência: Marcar Presença
       Fluxo completo: mensagem → confirmação → WCF (metaim1)
       → saveBooking → resposta ao utilizador

  08_sequencia_listar_marcacoes.puml
     → Diagrama de Sequência: Listar Marcações
       Fluxo multi-mensagem: trigger → pedir período →
       validar datas → confirmar → WCF (metalm1) → resposta

══════════════════════════════════════════════════════════
