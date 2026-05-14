# Webhook RH Validation - TODO

Este documento resume o que falta implementar no WebServerEstagioELO para validar o utilizador (RH) e gravar a marcacao, seguindo a logica do nGMobile.

## Contexto
- O webhook recebe o numero de telefone.
- Temos de encontrar o utilizador associado ao recurso humano com esse telefone.
- Depois validar permissoes e gravar a marcacao.
- A logica real deve vir dos assemblies Business/Core da empresa.

## Ficheiros base (nGMobile)
- Tools/DatabaseTool.cs
  - Le WebConfig.ini na pasta bin
  - Obtem Provider, Connection, Cdemp, Language
- Business/Authenticator.cs
  - Valida utilizador por CdUser/Email (nao usa telefone direto)
- Business/AttendanceExpert.cs
  - Faz o fluxo completo de marcacoes (SaveLocationBooking / pedidos)

## O que ja foi adiantado no WebServerEstagioELO
- WebConfig.ini colocado no projeto e copiado para bin
- Utils/IniConfigLoader.cs (le o INI)
- AttendanceController com placeholders comentados

## Fluxo esperado (a implementar quando Business/Core chegar)
1) Ler config do INI (Provider, Connection, Timeout, Cdemp)
2) Abrir ligacao a DB (ADO.NET simples ou servico do Core)
3) Encontrar utilizador pelo telefone
   - Confirmar tabela/coluna (ex.: RcsHumanos.Telemovel)
4) (Comentado por agora) Verificar permissoes do utilizador
5) Inicializar contexto de utilizador (equivalente a users.Init)
6) Construir Booking e gravar marcacao
   - Equivalente a AttendanceExpert.saveBooking
   - Lida com marcacoes diretas vs pedidos (zonas, ausencias, etc.)

## Pontos em falta (precisam do Business/Core)
- Metodo de lookup por telefone
- Metodo de permissao (ex.: HasAttendanceAccess)
- Inicializacao do contexto (user.Init)
- Metodo de gravacao (equivalente a SaveBooking)

## Notas
- Tudo esta comentado para nao quebrar o fluxo atual.
- Quando os assemblies chegarem, trocar placeholders por chamadas reais.
