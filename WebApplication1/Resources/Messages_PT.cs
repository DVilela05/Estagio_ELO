using System.Collections.ObjectModel;

namespace WebApplication1.Resources
{
    /// <summary>
    /// Mensagens em Português (PT) — língua por defeito / fallback.
    /// Contém TODAS as strings que o bot usa para comunicar com o utilizador.
    /// 
    /// Convenções de chaves:
    ///   - Categoria_Nome            → string simples
    ///   - Categoria_Nome_1, _2, _3  → variantes para rotação aleatória
    ///   - Categoria_Triggers        → triggers separados por "|"
    ///   - {0}, {1}, {2}             → placeholders para string.Format
    /// </summary>
    internal static class Messages_PT
    {
        public static readonly ReadOnlyDictionary<string, string> Strings = new(new Dictionary<string, string>
        {
            // ═══════════════════════════════════════════════════════════════
            // TRIGGERS — palavras-chave que ativam cada comando (separadas por "|")
            // ═══════════════════════════════════════════════════════════════

            ["Presence_Triggers"] =
                "presente|presença|presenca|marcar presença|marcar presenca|" +
                "marcação|marcaçao|marcacao|marcação presença|marcação presenca|" +
                "marcacao presença|marcacao presenca|cá estou|ca estou|estou cá|estou ca|cheguei",

            ["Listagem_Triggers"] =
                "ver marcações|ver marcacoes|marcações|marcacoes|listagem|" +
                "listagem de marcações|listagem de marcacoes|listar marcações|listar marcacoes|" +
                "minhas marcações|minhas marcacoes|ver assiduidade|assiduidade|" +
                "listagem assiduidade|listagem de assiduidade",

            ["Help_Triggers"] = "ajuda|help",

            // Tokens de confirmação (sim/não)
            ["YesTokens"] = "sim|s|yes|y",
            ["NoTokens"] = "nao|não|n|no",

            // ═══════════════════════════════════════════════════════════════
            // PRESENÇA — respostas do comando de marcação
            // ═══════════════════════════════════════════════════════════════

            ["Presence_CommandName"] = "presença",
            ["Presence_Description"] = "Marcar presença (requer PIN de localização; se o Web/PC não permitir, faz no telemóvel)",

            ["Presence_Success"] = "✅ Presença registada com sucesso!",
            ["Presence_SuccessStub"] = "✅ Mensagem de *presença* recebida com sucesso.",

            ["Presence_ErrorTimeout"] = "⏱️ O servidor demorou a responder. A tua presença não foi registada — tenta novamente em alguns minutos.",
            ["Presence_ErrorUnavailable"] = "🔌 O servidor de registo está temporariamente indisponível. Tenta novamente mais tarde.",
            ["Presence_ErrorGeneric"] = "⚠️ Houve um problema ao registar a presença. A equipa técnica foi notificada. Tenta novamente mais tarde.",

            ["Presence_ErrorUnknownApi"] = "⚠️ Ocorreu um erro interno (API desconhecida). A equipa técnica foi notificada.",
            ["Presence_ErrorEmailMandatory"] = "⚠️ Não foi possível identificar o teu e-mail corporativo. Contacta o suporte para garantir que a tua conta do Teams está bem configurada.",
            ["Presence_ErrorNumberMandatory"] = "⚠️ Não foi possível ler o teu número de telemóvel para associar à tua ficha de colaborador.",
            ["Presence_ErrorTokenMandatory"] = "⚠️ Erro de segurança (Token em falta). Tenta novamente mais tarde.",
            ["Presence_ErrorInvalidDatetime"] = "⚠️ A data da mensagem não foi compreendida pelo servidor. Tenta enviar a presença novamente.",
            ["Presence_ErrorLoginError"] = "⚠️ Ocorreu um erro ao validar os teus dados. Confirma com os Recursos Humanos se o teu {0} está corretamente associado à tua ficha. Escreve *help* para mais informações.",
            ["Presence_ErrorDecodeError"] = "⚠️ Ocorreu um erro na transmissão dos dados. Tenta novamente.",
            ["Presence_ErrorBusinessGeneric"] = "⚠️ Não foi possível registar a presença. Erro do servidor: {0}",
            ["Presence_ErrorUnexpectedResponse"] = "⚠️ Resposta inesperada do servidor: {0}",

            ["Presence_IdType_Email"] = "e-mail",
            ["Presence_IdType_Phone"] = "nº de telefone",

            // ═══════════════════════════════════════════════════════════════
            // LISTAGEM DE MARCAÇÕES — fluxo completo
            // ═══════════════════════════════════════════════════════════════

            ["Listagem_CommandName"] = "listagem_marcações",
            ["Listagem_Description"] = "Ver as tuas marcações de assiduidade num determinado período (máximo 7 dias)",

            ["Listagem_Expired"] = "⚠️ O pedido anterior expirou. Por favor, escreva *ver marcações* para recomeçar.",
            ["Listagem_ConfirmSingle"] = "Confirmas que pretendes ver a listagem de marcações para a data de *{0}*? (sim/não)",
            ["Listagem_ConfirmRange"] = "Confirmas que pretendes ver a listagem de marcações para o período de *{0} a {1}*? (sim/não)",
            ["Listagem_Cancelled"] = "❌ Pedido de listagem cancelado. Se precisares de ajuda, escreve *ajuda*.",
            ["Listagem_AskPeriod"] =
                "Qual é o período de análise? Podes indicar uma data (ex: *10/10/2025*, *hoje*, *ontem*) ou um período de análise até 7 dias (ex: *01/10/2025 a 07/10/2025*).\n\n" +
                "💡 _Dica: Podes escrever uma frase natural, ex: 'mostra-me as marcações de ontem a hoje' ou 'quero ver o dia 12/10/2025'._",

            // Erros de extração de período
            ["Listagem_EmptyInput"] = "⚠️ O texto enviado está vazio.",
            ["Listagem_NoDateFound"] = "⚠️ Não consegui encontrar nenhuma data ou palavra-chave de período válida.",
            ["Listagem_PeriodTooLong"] = "⚠️ O período de análise ({0} dias) excede o limite máximo de 7 dias.",
            ["Listagem_DateRetryHelp"] =
                "\n\nPor favor, indica uma data específica ou um período de análise (máximo 7 dias).\n" +
                "Exemplos:\n" +
                "• *10/10/2025*\n" +
                "• *01/10/2025 a 07/10/2025*\n" +
                "• *hoje*\n" +
                "• *ontem*\n\n" +
                "💡 _Dica: Podes escrever uma frase natural, ex: 'quero ver o dia 10/10/2025' ou 'mostra de ontem a hoje'._",

            // Atalhos de datas
            ["Listagem_DateToday"] = "hoje",
            ["Listagem_DateYesterday"] = "ontem",

            // Mock de marcações
            ["Listagem_MockTitle"] = "📅 *Listagem de Marcações de Assiduidade*",
            ["Listagem_MockUser"] = "👤 *Colaborador:* {0}",
            ["Listagem_MockPeriod"] = "📆 *Período:* {0}",
            ["Listagem_MockSeparator"] = "──────────────────────────",
            ["Listagem_MockWeekend"] = "*• {0} ({1}):* 🛌 _Fim de semana_",
            ["Listagem_MockDayHeader"] = "*• {0} ({1}):*",
            ["Listagem_MockEntry"] = "   📥 08:30 - Entrada (Escritório ELO)",
            ["Listagem_MockLunchOut"] = "   📤 12:30 - Saída (Almoço)",
            ["Listagem_MockLunchIn"] = "   📥 13:30 - Entrada (Almoço)",
            ["Listagem_MockExit"] = "   📤 17:30 - Saída (Fim do Turno)",
            ["Listagem_MockNote"] = "💡 _Nota: Estes dados são de demonstração enquanto a integração com o Web Service não é ativada._",
            ["Listagem_DefaultUser"] = "Colaborador",

            // Dias da semana
            ["Day_Monday"] = "Segunda-feira",
            ["Day_Tuesday"] = "Terça-feira",
            ["Day_Wednesday"] = "Quarta-feira",
            ["Day_Thursday"] = "Quinta-feira",
            ["Day_Friday"] = "Sexta-feira",
            ["Day_Saturday"] = "Sábado",
            ["Day_Sunday"] = "Domingo",

            // ═══════════════════════════════════════════════════════════════
            // AJUDA — menu de comandos
            // ═══════════════════════════════════════════════════════════════

            ["Help_CommandName"] = "ajuda",
            ["Help_Description"] = "Mostra a lista de comandos disponíveis",
            ["Help_Title"] = "🤖 *Comandos disponíveis:*",
            ["Help_CommandEntry"] = "▸ *{0}* — {1}",
            ["Help_TriggerLabel"] = "   _Escreve:_ {0}",
            ["Help_FooterSeparator"] = "───────────────────",
            ["Help_NotePresence"] = "📍 _Nota de presença: a marcação exige PIN de localização._",
            ["Help_NoteMobile"] = "📱 _Se no Web/PC não conseguires enviar PIN, marca a presença no telemóvel._",
            ["Help_Hint"] = "💡 _Escreve qualquer um dos comandos acima para começar._",

            // ═══════════════════════════════════════════════════════════════
            // CONFIRMAÇÕES — prompts de sim/não
            // ═══════════════════════════════════════════════════════════════

            ["Confirmation_Prompt_1"] = "Recebi o pedido para realizar uma marcação de assiduidade. Posso avançar? (sim/não)",

            ["Confirmation_YesNoHelp_1"] = "❓ Perguntei sobre *{0}* — responde com SIM ou NÃO (s/n, yes/no).",
            ["Confirmation_YesNoHelp_2"] = "📋 Ainda estou à espera: queres mesmo *{0}*? SIM ou NÃO (s/n, yes/no).",
            ["Confirmation_YesNoHelp_3"] = "🗣️ É sobre *{0}* — responde apenas SIM ou NÃO (s/n, yes/no).",
            ["Confirmation_YesNoHelp_4"] = "💬 Queres *{0}* ou não? Responde SIM ou NÃO (s/n, yes/no).",
            ["Confirmation_YesNoHelp_5"] = "✋ Para avançar com *{0}*, confirma com SIM ou NÃO (s/n, yes/no).",

            ["Confirmation_RemainingAttempts"] = "\n\n⏳ Tentativas restantes: *{0}*",

            ["Confirmation_NoPending_1"] = "⚠️ Não tenho nenhum pedido pendente para confirmar.\n\nSe enviaste um comando antes, pode ter expirado ou o sistema foi reiniciado.\n\nEscreve *ajuda* para ver os comandos disponíveis.",
            ["Confirmation_NoPending_2"] = "⚠️ Hmm, não encontro nenhuma confirmação pendente.\n\nTalvez o pedido tenha expirado ou perdeu-se durante um reinício.\n\nTenta enviar o comando novamente ou escreve *ajuda*.",
            ["Confirmation_NoPending_3"] = "⚠️ Não tenho nada a aguardar confirmação neste momento.\n\nSe enviaste algo antes, pode ter sido há muito tempo ou o sistema foi reiniciado.\n\nPara recomeçar, escreve *ajuda* e vê os comandos disponíveis.",
            ["Confirmation_NoPending_4"] = "⚠️ Confirmar o quê? Não tenho nenhum pedido à espera.\n\nSe tinhas enviado algo, pode ter expirado entretanto.\n\nEscreve *ajuda* para veres o que podes fazer.",
            ["Confirmation_NoPending_5"] = "⚠️ Não há nada pendente de confirmação.\n\nPode ter expirado ou perdeu-se quando o sistema foi reiniciado.\n\nUsa *ajuda* para veres todos os comandos e recomeçar.",
            ["Confirmation_NoPending_6"] = "⚠️ Ups, não tenho registo de nenhum pedido teu à espera.\n\nSe enviaste antes, pode ter sido há muito tempo ou houve um reinício.\n\nEscreve *ajuda* para saberes o que fazer.",

            ["Confirmation_InvalidFinal_1"] = "⚠️ A resposta esperada era *sim* ou *não* para *{0}*.\n\nVou cancelar este pedido por agora. Se precisares, escreve *ajuda*.",
            ["Confirmation_InvalidFinal_2"] = "⚠️ Não era a resposta esperada para *{0}* — precisava de *sim* ou *não*.\n\nPedido cancelado. Podes escrever *ajuda* para ver as opções.",
            ["Confirmation_InvalidFinal_3"] = "⚠️ Para *{0}* eu só esperava *sim* ou *não*.\n\nComo já houve 3 tentativas inválidas, cancelei o pedido. Escreve *ajuda* para continuar.",
            ["Confirmation_InvalidFinal_4"] = "⚠️ Não consegui confirmar *{0}* porque a resposta não foi *sim*/*não*.\n\nCancelei esta confirmação. Se quiseres, escreve *ajuda*.",
            ["Confirmation_InvalidFinal_5"] = "⚠️ A confirmação de *{0}* foi cancelada: a resposta não era *sim* nem *não*.\n\nUsa *ajuda* para retomar.",

            ["Confirmation_Cancelled"] = "❌ Ok, *{0}* cancelado. Se precisares de ajuda, escreve ajuda.",

            // ═══════════════════════════════════════════════════════════════
            // LOCALIZAÇÃO — prompts de PIN
            // ═══════════════════════════════════════════════════════════════

            ["Location_Request_1"] = "📍 Perfeito — para concluir a *presença*, envia agora o PIN de localização da própria app.\nTens *{0} segundos* para enviar a localização atual.",
            ["Location_Request_2"] = "📍 Falta só a localização para fechar a *presença*. Envia o PIN da app.\nA localização tem de ser a *atual* e tens *{0} segundos*.",
            ["Location_Request_3"] = "📍 Confirmado. Para terminar a *presença*, partilha a tua localização atual pela app.\nJanela: *{0} segundos*.",
            ["Location_Request_4"] = "📍 Estamos quase: envia o PIN de localização da tua app para concluir a *presença*.\nTempo máximo: *{0} segundos*.",
            ["Location_Request_5"] = "📍 Último passo para registar a *presença*: envia o PIN da tua localização atual.\nApenas a localização *agora* é aceite.",

            ["Location_Help_1"] = "📍 Ainda preciso do PIN de localização atual para concluir a *presença*.\nEnvia a localização da própria app dentro de *{0} segundos*.",
            ["Location_Help_2"] = "🗺️ Para terminar a *presença*, envia a tua localização atual pela app.\nA janela é curta: *{0} segundos*.",
            ["Location_Help_3"] = "📌 Para validar a *presença* preciso do PIN da localização atual.\nNão serve localização antiga ou alterada.",
            ["Location_Help_4"] = "📍 Falta a localização atual para validar a *presença*.\nTens de enviar o pin da própria app rapidamente.",
            ["Location_Help_5"] = "🧭 Para fechar a *presença* preciso do PIN da tua localização *agora*.\nSe a janela expirar, terás de recomeçar o pedido.",

            ["Location_Final_1"] = "⚠️ Não recebi o PIN de localização a tempo. Cancelei este pedido de *presença*.\nPara tentar de novo: *presente* e depois envia o PIN via *📎 Localização*.",
            ["Location_Final_2"] = "⚠️ Sem PIN de localização dentro do tempo limite não consigo concluir a *presença*. Pedido cancelado.",
            ["Location_Final_3"] = "⚠️ A *presença* foi cancelada porque a localização não chegou a tempo.\nQuando quiseres, recomeça com *presente*.",
            ["Location_Final_4"] = "⚠️ A janela de envio do PIN expirou. Cancelei a *presença*.\nNo próximo pedido, envia a localização dentro do tempo limite.",
            ["Location_Final_5"] = "⚠️ Pedido de *presença* cancelado: faltou o PIN de localização dentro da janela permitida.",

            ["Location_Received"] = "📍 PIN de localização recebido.",

            // ═══════════════════════════════════════════════════════════════
            // COMANDO DESCONHECIDO — mensagens de fallback
            // ═══════════════════════════════════════════════════════════════

            ["Unknown_1"] = "🤔 Hmm, não percebi o que querias dizer.\n\nEscreve *ajuda* para ver o que posso fazer.",
            ["Unknown_2"] = "❓ Essa mensagem não corresponde a nenhum comando.\n\nExperimenta escrever *ajuda*.",
            ["Unknown_3"] = "🙈 Não reconheço essa mensagem.\n\nEnvia *menu* para ver as opções disponíveis.",
            ["Unknown_4"] = "⚠️ Comando não encontrado.\n\nEscreve *?* para ver a lista de comandos.",
            ["Unknown_5"] = "🤷 Ainda não sei fazer isso!\n\nEscreve *ajuda* para ver o que está disponível.",
            ["Unknown_6"] = "📭 Mensagem não suportada.\n\nDigita *help* para ver os comandos que aceito.",

            // ═══════════════════════════════════════════════════════════════
            // MULTI-LÍNGUA — mensagem curta quando não se deteta a língua
            // ═══════════════════════════════════════════════════════════════

            ["MultiLang_Hint"] = "Escreve *ajuda*",
        });
    }
}
