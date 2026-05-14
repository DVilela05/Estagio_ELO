Architecture UML package (PlantUML) — versão simplificada

Objetivo:
- Diagramas pequenos, diretos e fáceis de explicar no relatório/apresentação.
- Cada diagrama responde a uma pergunta específica.

Como usar:
1) Abrir qualquer ficheiro .puml
2) Usar preview/export da extensão PlantUML no VS Code
3) Exportar para PNG/SVG para o relatório

Diagramas incluídos:
- 01_overview_layers.puml
	Visão geral em camadas (API, Application, Core, Infrastructure)

- 02_request_flow_whatsapp.puml
	Fluxo WhatsApp ponta-a-ponta (webhook, filtros, processamento e resposta)

- 03_request_flow_teams.puml
	Fluxo Teams ponta-a-ponta (JWT, processamento e reply)

- 04_presence_state_machine.puml
	Máquina de estados da conversa para marcação de presença com PIN

- 05_security_pipeline.puml
	Pipeline de segurança HTTP (middlewares, filtros e controlos anti-spam)
