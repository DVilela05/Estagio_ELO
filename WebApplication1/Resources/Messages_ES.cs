using System.Collections.ObjectModel;

namespace WebApplication1.Resources
{
    /// <summary>
    /// Mensajes en Español (ES).
    /// </summary>
    internal static class Messages_ES
    {
        public static readonly ReadOnlyDictionary<string, string> Strings = new(new Dictionary<string, string>
        {
            // ═══════════════════════════════════════════════════════════════
            // TRIGGERS
            // ═══════════════════════════════════════════════════════════════

            ["Presence_Triggers"] =
                "presente|presencia|marcar presencia|marcar asistencia|" +
                "asistencia|estoy aquí|estoy aqui|llegué|llegue|he llegado",

            ["Listagem_Triggers"] =
                "ver asistencia|ver registros|registros de asistencia|" +
                "listar asistencia|listar registros|mis registros|mi asistencia|" +
                "listado de asistencia|listado asistencia",

            ["Ferias_Triggers"] =
                "vacaciones|ver vacaciones|listar vacaciones|" +
                "mis vacaciones|consultar vacaciones|días libres|dias libres",

            ["Help_Triggers"] = "ayuda|help|comandos",

            ["YesTokens"] = "sí|si|s|yes|y",
            ["NoTokens"] = "no|n",

            // ═══════════════════════════════════════════════════════════════
            // PRESENCIA
            // ═══════════════════════════════════════════════════════════════

            ["Presence_CommandName"] = "presencia",
            ["Presence_Description"] = "Marcar presencia (requiere PIN de ubicación; si el Web/PC no lo permite, usa el teléfono)",

            ["Presence_Success"] = "✅ ¡Presencia registrada con éxito!",
            ["Presence_SuccessStub"] = "✅ Mensaje de *presencia* recibido con éxito.",

            ["Presence_ErrorTimeout"] = "⏱️ El servidor tardó demasiado en responder. Tu presencia no fue registrada — inténtalo de nuevo en unos minutos.",
            ["Presence_ErrorUnavailable"] = "🔌 El servidor de registro está temporalmente no disponible. Inténtalo más tarde.",
            ["Presence_ErrorGeneric"] = "⚠️ Hubo un problema al registrar la presencia. El equipo técnico ha sido notificado. Inténtalo más tarde.",

            ["Presence_ErrorUnknownApi"] = "⚠️ Ocurrió un error interno (API desconocida). El equipo técnico ha sido notificado.",
            ["Presence_ErrorEmailMandatory"] = "⚠️ No se pudo identificar tu correo electrónico corporativo. Contacta con soporte para verificar la configuración de tu cuenta de Teams.",
            ["Presence_ErrorNumberMandatory"] = "⚠️ No se pudo leer tu número de teléfono para asociarlo a tu ficha de empleado.",
            ["Presence_ErrorTokenMandatory"] = "⚠️ Error de seguridad (token faltante). Inténtalo más tarde.",
            ["Presence_ErrorInvalidDatetime"] = "⚠️ La fecha del mensaje no fue comprendida por el servidor. Intenta enviar tu presencia nuevamente.",
            ["Presence_ErrorLoginError"] = "⚠️ Ocurrió un error al validar tus datos. Verifica con Recursos Humanos que tu {0} está correctamente asociado(a) a tu ficha. Escribe *ayuda* para más información.",
            ["Presence_ErrorDecodeError"] = "⚠️ Ocurrió un error en la transmisión de datos. Inténtalo de nuevo.",
            ["Presence_ErrorBusinessGeneric"] = "⚠️ No se pudo registrar la presencia. Error del servidor: {0}",
            ["Presence_ErrorUnexpectedResponse"] = "⚠️ Respuesta inesperada del servidor: {0}",

            ["Presence_IdType_Email"] = "correo electrónico",
            ["Presence_IdType_Phone"] = "nº de teléfono",

            // ═══════════════════════════════════════════════════════════════
            // LISTADO
            // ═══════════════════════════════════════════════════════════════

            ["Listagem_CommandName"] = "listado_asistencia",
            ["Listagem_Description"] = "Ver tus registros de asistencia de un período determinado (máximo 7 días)",

            ["Listagem_Expired"] = "⚠️ La solicitud anterior ha expirado. Por favor, escribe *ver asistencia* para empezar de nuevo.",
            ["Listagem_ConfirmSingle"] = "¿Confirmas que quieres ver los registros de asistencia para la fecha *{0}*? (sí/no)",
            ["Listagem_ConfirmRange"] = "¿Confirmas que quieres ver los registros de asistencia para el período del *{0} al {1}*? (sí/no)",
            ["Listagem_Cancelled"] = "❌ Solicitud de listado cancelada. Si necesitas ayuda, escribe *ayuda*.",
            ["Listagem_AskPeriod"] =
                "¿Qué período quieres consultar? Puedes indicar una fecha (ej: *10/10/2025*, *hoy*, *ayer*) o un período de hasta 7 días (ej: *01/10/2025 a 07/10/2025*).\n\n" +
                "💡 _Consejo: Puedes escribir 'cancelar' o 'salir' para anular la solicitud._",

            ["Listagem_EmptyInput"] = "⚠️ El texto enviado está vacío.",
            ["Listagem_NoDateFound"] = "⚠️ No pude encontrar ninguna fecha o palabra clave de período válida.",
            ["Listagem_PeriodTooLong"] = "⚠️ El período de análisis ({0} días) supera el máximo de 7 días.",
            ["Listagem_InvalidFormat"] = "⚠️ Formato inválido. Por favor, introduce solo las fechas.",
            
            // Errores de WebService
            ["Listagem_Error_InvalidStartDateTime"] = "⚠️ La fecha de inicio indicada es inválida.",
            ["Listagem_Error_InvalidDateTimePeriod"] = "⚠️ El período de fechas indicado es inválido.",
            ["Listagem_Error_InvalidNrDaysPeriod"] = "⚠️ El período indicado supera el límite permitido (máximo 7 días).",
            ["Listagem_Error_Null"] = "⚠️ Se ha producido un error al procesar las marcas. Por favor, inténtalo de nuevo más tarde.",
            ["Listagem_Error_NoTimeBookings"] = "ℹ️ No hay marcas registradas para el período indicado.",
            ["Listagem_ErrorGeneric"] = "❌ Ocurrió un error al consultar las marcas. Inténtalo de nuevo más tarde.",
            ["Listagem_DateRetryHelp"] =
                "\n\nPor favor, indica una fecha específica o un período (máximo 7 días).\n" +
                "Ejemplos:\n" +
                "• *10/10/2025*\n" +
                "• *01/10/2025 a 07/10/2025*\n" +
                "• *hoy*\n" +
                "• *ayer*\n\n" +
                "💡 _Consejo: Puedes escribir 'cancelar' o 'salir' para anular la solicitud._",

            ["Listagem_DateToday"] = "hoy",
            ["Listagem_DateYesterday"] = "ayer",

            ["Listagem_MockTitle"] = "📅 *Listado de registros de asistencia*",
            ["Listagem_MockUser"] = "👤 *Empleado:* {0}",
            ["Listagem_MockPeriod"] = "📆 *Período:* {0}",
            ["Listagem_MockSeparator"] = "──────────────────────────",
            ["Listagem_MockWeekend"] = "*• {0} ({1}):* 🛌 _Fin de semana_",
            ["Listagem_MockDayHeader"] = "*• {0} ({1}):*",
            ["Listagem_MockEntry"] = "   📥 08:30 - Entrada (Oficina ELO)",
            ["Listagem_MockLunchOut"] = "   📤 12:30 - Salida (Almuerzo)",
            ["Listagem_MockLunchIn"] = "   📥 13:30 - Entrada (Almuerzo)",
            ["Listagem_MockExit"] = "   📤 17:30 - Salida (Fin del turno)",
            ["Listagem_MockNote"] = "💡 _Nota: Estos datos son de demostración mientras la integración con el Web Service no está activa._",
            ["Listagem_DefaultUser"] = "Empleado",

            ["Day_Monday"] = "Lunes",
            ["Day_Tuesday"] = "Martes",
            ["Day_Wednesday"] = "Miércoles",
            ["Day_Thursday"] = "Jueves",
            ["Day_Friday"] = "Viernes",
            ["Day_Saturday"] = "Sábado",
            ["Day_Sunday"] = "Domingo",

            // ═══════════════════════════════════════════════════════════════
            // LISTADO DE VACACIONES
            // ═══════════════════════════════════════════════════════════════

            ["Ferias_CommandName"] = "listado_vacaciones",
            ["Ferias_Description"] = "Consultar tus vacaciones para un año determinado",

            ["Ferias_Expired"] = "⚠️ La solicitud anterior ha expirado. Por favor, escribe *vacaciones* para empezar de nuevo.",
            ["Ferias_ConfirmYear"] = "¿Confirmas que quieres consultar tus vacaciones para el año *{0}*? (sí/no)",
            ["Ferias_Cancelled"] = "❌ Solicitud de consulta de vacaciones cancelada. Si necesitas ayuda, escribe *ayuda*.",
            ["Ferias_AskYear"] =
                "¿Para qué año quieres consultar las vacaciones? Indica el año (ej: *2025*, *2026*).\n\n" +
                "💡 _Consejo: Puedes escribir 'cancelar' o 'salir' para anular la solicitud._",

            ["Ferias_EmptyInput"] = "⚠️ El texto enviado está vacío.",
            ["Ferias_InvalidYear"] = "⚠️ No pude identificar un año válido.",
            ["Ferias_YearRetryHelp"] =
                "\n\nPor favor, indica el año deseado (ej: *2025*, *2026*).\n\n" +
                "💡 _Consejo: Puedes escribir 'cancelar' o 'salir' para anular la solicitud._",

            ["Ferias_ErrorGeneric"] = "❌ Ocurrió un error al consultar las vacaciones. Inténtalo de nuevo más tarde.",

            ["Ferias_Placeholder"] = "🏖️ La funcionalidad de consulta de vacaciones para el año *{0}* estará disponible pronto. ¡Permanece atento!",

            // ═══════════════════════════════════════════════════════════════
            // AYUDA
            // ═══════════════════════════════════════════════════════════════

            ["Help_CommandName"] = "ayuda",
            ["Help_Description"] = "Muestra la lista de comandos disponibles",
            ["Help_Title"] = "🤖 *Comandos disponibles:*",
            ["Help_CommandEntry"] = "▸ *{0}* — {1}",
            ["Help_TriggerLabel"] = "   _Escribe:_ {0}",
            ["Help_FooterSeparator"] = "───────────────────",
            ["Help_NotePresence"] = "📍 _Nota de presencia: el registro requiere PIN de ubicación._",
            ["Help_NoteMobile"] = "📱 _Si el Web/PC no permite enviar PIN, marca la presencia en el teléfono._",
            ["Help_Hint"] = "💡 _Escribe cualquiera de los comandos anteriores para empezar._",

            // ═══════════════════════════════════════════════════════════════
            // CONFIRMACIONES
            // ═══════════════════════════════════════════════════════════════

            ["Confirmation_Prompt_1"] = "He recibido una solicitud para registrar asistencia. ¿Puedo continuar? (sí/no)",

            ["Confirmation_YesNoHelp_1"] = "❓ Pregunté sobre *{0}* — responde con SÍ o NO (s/n).",
            ["Confirmation_YesNoHelp_2"] = "📋 Sigo esperando: ¿realmente quieres *{0}*? SÍ o NO (s/n).",
            ["Confirmation_YesNoHelp_3"] = "🗣️ Es sobre *{0}* — responde solo SÍ o NO (s/n).",
            ["Confirmation_YesNoHelp_4"] = "💬 ¿Quieres *{0}* o no? Responde SÍ o NO (s/n).",
            ["Confirmation_YesNoHelp_5"] = "✋ Para continuar con *{0}*, confirma con SÍ o NO (s/n).",

            ["Confirmation_RemainingAttempts"] = "\n\n⏳ Intentos restantes: *{0}*",

            ["Confirmation_NoPending_1"] = "⚠️ No tengo ninguna solicitud pendiente de confirmar.\n\nSi enviaste un comando antes, puede haber expirado o el sistema fue reiniciado.\n\nEscribe *ayuda* para ver los comandos disponibles.",
            ["Confirmation_NoPending_2"] = "⚠️ Hmm, no encuentro ninguna confirmación pendiente.\n\nLa solicitud puede haber expirado o se perdió durante un reinicio.\n\nIntenta enviar el comando de nuevo o escribe *ayuda*.",
            ["Confirmation_NoPending_3"] = "⚠️ No hay nada esperando confirmación en este momento.\n\nSi enviaste algo antes, puede haber pasado mucho tiempo o el sistema se reinició.\n\nEscribe *ayuda* para ver los comandos.",
            ["Confirmation_NoPending_4"] = "⚠️ ¿Confirmar qué? No tengo ninguna solicitud pendiente.\n\nSi habías enviado algo, puede haber expirado.\n\nEscribe *ayuda* para ver tus opciones.",
            ["Confirmation_NoPending_5"] = "⚠️ Nada pendiente de confirmación.\n\nPuede haber expirado o se perdió durante un reinicio.\n\nUsa *ayuda* para ver todos los comandos.",
            ["Confirmation_NoPending_6"] = "⚠️ Ups, no tengo registro de ninguna solicitud pendiente tuya.\n\nSi enviaste algo antes, puede haber pasado mucho tiempo.\n\nEscribe *ayuda* para orientarte.",

            ["Confirmation_InvalidFinal_1"] = "⚠️ La respuesta esperada era *sí* o *no* para *{0}*.\n\nCancelo esta solicitud por ahora. Si necesitas ayuda, escribe *ayuda*.",
            ["Confirmation_InvalidFinal_2"] = "⚠️ Esa no era la respuesta esperada para *{0}* — necesitaba *sí* o *no*.\n\nSolicitud cancelada. Escribe *ayuda* para ver las opciones.",
            ["Confirmation_InvalidFinal_3"] = "⚠️ Para *{0}* solo esperaba *sí* o *no*.\n\nDespués de 3 intentos inválidos, cancelé la solicitud. Escribe *ayuda* para continuar.",
            ["Confirmation_InvalidFinal_4"] = "⚠️ No pude confirmar *{0}* porque la respuesta no fue *sí*/*no*.\n\nConfirmación cancelada. Escribe *ayuda* si lo necesitas.",
            ["Confirmation_InvalidFinal_5"] = "⚠️ La confirmación de *{0}* fue cancelada: la respuesta no fue ni *sí* ni *no*.\n\nUsa *ayuda* para continuar.",

            ["Confirmation_Cancelled"] = "❌ Vale, *{0}* cancelado. Si necesitas ayuda, escribe ayuda.",

            // ═══════════════════════════════════════════════════════════════
            // UBICACIÓN
            // ═══════════════════════════════════════════════════════════════

            ["Location_Request_1"] = "📍 Perfecto — para completar la *presencia*, envía ahora el PIN de ubicación desde la app.\nTienes *{0} segundos* para enviar tu ubicación actual.",
            ["Location_Request_2"] = "📍 Solo falta la ubicación para cerrar la *presencia*. Envía el PIN de la app.\nLa ubicación debe ser *actual* y tienes *{0} segundos*.",
            ["Location_Request_3"] = "📍 Confirmado. Para terminar la *presencia*, comparte tu ubicación actual desde la app.\nVentana: *{0} segundos*.",
            ["Location_Request_4"] = "📍 Casi listo: envía el PIN de ubicación de tu app para completar la *presencia*.\nTiempo máximo: *{0} segundos*.",
            ["Location_Request_5"] = "📍 Último paso para registrar la *presencia*: envía el PIN de tu ubicación actual.\nSolo la ubicación *actual* es aceptada.",

            ["Location_Help_1"] = "📍 Aún necesito el PIN de ubicación actual para completar la *presencia*.\nEnvía tu ubicación desde la app dentro de *{0} segundos*.",
            ["Location_Help_2"] = "🗺️ Para terminar la *presencia*, envía tu ubicación actual desde la app.\nLa ventana es corta: *{0} segundos*.",
            ["Location_Help_3"] = "📌 Para validar la *presencia* necesito el PIN de ubicación actual.\nNo sirve una ubicación antigua o modificada.",
            ["Location_Help_4"] = "📍 Falta la ubicación actual para validar la *presencia*.\nDebes enviar el PIN desde la app rápidamente.",
            ["Location_Help_5"] = "🧭 Para completar la *presencia* necesito tu PIN de ubicación *ahora*.\nSi la ventana expira, tendrás que empezar de nuevo.",

            ["Location_Final_1"] = "⚠️ No recibí el PIN de ubicación a tiempo. Solicitud de *presencia* cancelada.\nPara intentar de nuevo: escribe *presente* y luego envía el PIN vía *📎 Ubicación*.",
            ["Location_Final_2"] = "⚠️ Sin PIN de ubicación dentro del tiempo límite no puedo completar la *presencia*. Solicitud cancelada.",
            ["Location_Final_3"] = "⚠️ La *presencia* fue cancelada porque la ubicación no llegó a tiempo.\nCuando quieras, empieza de nuevo con *presente*.",
            ["Location_Final_4"] = "⚠️ La ventana de envío del PIN ha expirado. *Presencia* cancelada.\nLa próxima vez, envía la ubicación dentro del tiempo límite.",
            ["Location_Final_5"] = "⚠️ Solicitud de *presencia* cancelada: el PIN de ubicación no fue recibido dentro de la ventana permitida.",

            ["Location_Received"] = "📍 PIN de ubicación recibido.",

            // ═══════════════════════════════════════════════════════════════
            // COMANDO DESCONOCIDO
            // ═══════════════════════════════════════════════════════════════

            ["Unknown_1"] = "🤔 Hmm, no entendí lo que querías decir.\n\nEscribe *ayuda* para ver lo que puedo hacer.",
            ["Unknown_2"] = "❓ Ese mensaje no corresponde a ningún comando.\n\nPrueba escribiendo *ayuda*.",
            ["Unknown_3"] = "🙈 No reconozco ese mensaje.\n\nEnvía *menú* para ver las opciones disponibles.",
            ["Unknown_4"] = "⚠️ Comando no encontrado.\n\nEscribe *?* para ver la lista de comandos.",
            ["Unknown_5"] = "🤷 ¡Todavía no sé hacer eso!\n\nEscribe *ayuda* para ver lo que está disponible.",
            ["Unknown_6"] = "📭 Mensaje no soportado.\n\nEscribe *ayuda* para ver los comandos que acepto.",

            ["MultiLang_Hint"] = "Escribe *ayuda*",
            // ═══════════════════════════════════════════════════════════════
            // ERRORES DE SEGURIDAD Y VALIDACIÓN
            // ═══════════════════════════════════════════════════════════════

            ["Token_SecurityError"] = "Acceso denegado: Falló la validación de seguridad (Token inválido o caducado).",
            ["Error_NoPermission"] = "Acceso denegado: No tienes permisos para usar esta función.",
            ["Error_UserNotFound"] = "No se pudo encontrar tu registro de empleado en el sistema.",
            ["Error_MultipleUsers"] = "Hay varios empleados asociados a este contacto. Por favor, contacta con RRHH.",
        });
    }
}
