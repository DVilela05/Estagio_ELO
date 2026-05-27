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
       Casos: Marcar Presença, Listar Marcações, Consultar Férias, Ajuda

  02_modelo_dominio.puml
     → Modelo de Domínio
       Classes: IncomingMessage, PendingConfirmation,
       PendingPeriodState, PendingFeriasState,
       Booking (ELO), ConsultaFerias (ELO)

  03_vista_logica_n1.puml
     → Vista Lógica Nível 1 (Diagrama de Contexto)
       Visão de alto nível: sistemas externos ↔ Gateway

  04_vista_logica_n2.puml
     → Vista Lógica Nível 2 (Diagrama de Containers)
       Layout vertical: plataformas em cima, sistema em baixo

  04_b_vista_logica_n3_componentes.puml
     → Vista Lógica Nível 3 (Componentes do Gateway)
       Layout horizontal: Api → Application → Core → Infrastructure
       Inclui: ListagemFériasHandler, TokenService

  05_vista_logica_n3_processamento.puml
     → Vista Lógica Nível 3 (Fluxo de Processamento)
       Detalhe do MessageProcessingService (anti-spam,
       confirmações, routing)

  06_vista_logica_n3_seguranca.puml
     → Vista Lógica Nível 3 (Pipeline de Segurança)
       Middleware global + filtros por plataforma

  07_sequencia_marcar_presenca.puml (original completo)
  07a_sequencia_marcar_presenca_parte1.puml
     → Parte 1: Receção e Confirmação
  07b_sequencia_marcar_presenca_parte2.puml
     → Parte 2: Execução e Resposta

  08_sequencia_listar_marcacoes.puml (original completo)
  08a_sequencia_listar_marcacoes_parte1.puml
     → Parte 1: Trigger e Pedido de Período
  08b_sequencia_listar_marcacoes_parte2.puml
     → Parte 2: Validação de Datas e Confirmação
  08c_sequencia_listar_marcacoes_parte3.puml
     → Parte 3: Execução e Resposta

  09_modelo_relacional.puml
     → Modelo Relacional (5 tabelas)
       Corrigido: Endereco = CdTerminal
       Adicionado: ngmobilewslog (Logs)

  10_sequencia_consultar_ferias.puml (original completo)
  10a_sequencia_consultar_ferias_parte1.puml
     → Parte 1: Trigger Inicial e Pedido de Ano
  10b_sequencia_consultar_ferias_parte2.puml
     → Parte 2: Extração de Ano e Confirmação
  10c_sequencia_consultar_ferias_parte3.puml
     → Parte 3: Execução e Resposta

  11_sequencia_processamento_erp.puml (NOVO)
     → Diagrama de Sequência: Processamento no ERP
       Fluxo geral do lado da empresa:
       decode Base64 → autenticar → resolver identidade →
       converter fuso horário → executar ação → log → resposta

══════════════════════════════════════════════════════════
