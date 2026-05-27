using System.Collections.ObjectModel;

namespace WebApplication1.Resources
{
    /// <summary>
    /// Mensagens em Inglês (EN).
    /// </summary>
    internal static class Messages_EN
    {
        public static readonly ReadOnlyDictionary<string, string> Strings = new(new Dictionary<string, string>
        {
            // ═══════════════════════════════════════════════════════════════
            // TRIGGERS
            // ═══════════════════════════════════════════════════════════════

            ["Presence_Triggers"] =
                "present|attendance|mark attendance|check in|" +
                "i'm here|im here|here|arrived",

            ["Listagem_Triggers"] =
                "view attendance|view records|attendance records|records|" +
                "list attendance|list records|my attendance|my records|" +
                "attendance list|attendance listing",

            ["Ferias_Triggers"] =
                "vacations|vacation|holidays|view vacations|view holidays|" +
                "list vacations|list holidays|my vacations|my holidays",

            ["Help_Triggers"] = "help|commands|menu",

            ["YesTokens"] = "yes|y",
            ["NoTokens"] = "no|n",

            // ═══════════════════════════════════════════════════════════════
            // PRESENÇA
            // ═══════════════════════════════════════════════════════════════

            ["Presence_CommandName"] = "attendance",
            ["Presence_Description"] = "Mark attendance (requires location PIN; if Web/PC doesn't allow it, use your phone)",

            ["Presence_Success"] = "✅ Attendance recorded successfully!",
            ["Presence_SuccessStub"] = "✅ *Attendance* message received successfully.",

            ["Presence_ErrorTimeout"] = "⏱️ The server took too long to respond. Your attendance was not recorded — please try again in a few minutes.",
            ["Presence_ErrorUnavailable"] = "🔌 The attendance server is temporarily unavailable. Please try again later.",
            ["Presence_ErrorGeneric"] = "⚠️ There was a problem recording your attendance. The technical team has been notified. Please try again later.",

            ["Presence_ErrorUnknownApi"] = "⚠️ An internal error occurred (unknown API). The technical team has been notified.",
            ["Presence_ErrorEmailMandatory"] = "⚠️ Could not identify your corporate email. Contact support to ensure your Teams account is properly configured.",
            ["Presence_ErrorNumberMandatory"] = "⚠️ Could not read your phone number to link to your employee record.",
            ["Presence_ErrorTokenMandatory"] = "⚠️ Security error (missing token). Please try again later.",
            ["Presence_ErrorInvalidDatetime"] = "⚠️ The message date was not understood by the server. Please try sending your attendance again.",
            ["Presence_ErrorLoginError"] = "⚠️ An error occurred while validating your data. Please check with Human Resources that your {0} is correctly linked to your profile. Type *help* for more information.",
            ["Presence_ErrorDecodeError"] = "⚠️ A data transmission error occurred. Please try again.",
            ["Presence_ErrorBusinessGeneric"] = "⚠️ Could not record attendance. Server error: {0}",
            ["Presence_ErrorUnexpectedResponse"] = "⚠️ Unexpected server response: {0}",

            ["Presence_IdType_Email"] = "email",
            ["Presence_IdType_Phone"] = "phone number",

            // ═══════════════════════════════════════════════════════════════
            // LISTAGEM
            // ═══════════════════════════════════════════════════════════════

            ["Listagem_CommandName"] = "attendance_records",
            ["Listagem_Description"] = "View your attendance records for a given period (up to 7 days)",

            ["Listagem_Expired"] = "⚠️ The previous request has expired. Please type *view attendance* to start again.",
            ["Listagem_ConfirmSingle"] = "Do you confirm you want to view attendance records for the date *{0}*? (yes/no)",
            ["Listagem_ConfirmRange"] = "Do you confirm you want to view attendance records for the period *{0} to {1}*? (yes/no)",
            ["Listagem_Cancelled"] = "❌ Attendance listing cancelled. If you need help, type *help*.",
            ["Listagem_AskPeriod"] =
                "What period do you want to check? You can enter a date (e.g. *10/10/2025*, *today*, *yesterday*) or a period of up to 7 days (e.g. *01/10/2025 to 07/10/2025*).\n\n" +
                "💡 _Tip: You can write 'cancel' or 'exit' to cancel the request._",

            ["Listagem_EmptyInput"] = "⚠️ The message text is empty.",
            ["Listagem_NoDateFound"] = "⚠️ I couldn't find any valid date or period keyword.",
            ["Listagem_PeriodTooLong"] = "⚠️ The analysis period ({0} days) exceeds the maximum of 7 days.",
            ["Listagem_InvalidFormat"] = "⚠️ Invalid format. Please enter only the dates.",
            
            // WebService Errors
            ["Listagem_Error_InvalidStartDateTime"] = "⚠️ The specified start date is invalid.",
            ["Listagem_Error_InvalidDateTimePeriod"] = "⚠️ The specified date period is invalid.",
            ["Listagem_Error_InvalidNrDaysPeriod"] = "⚠️ The specified period exceeds the allowed limit (maximum 7 days).",
            ["Listagem_Error_Null"] = "⚠️ An error occurred while processing the bookings. Please try again later.",
            ["Listagem_Error_NoTimeBookings"] = "ℹ️ There are no bookings registered for the specified period.",
            ["Listagem_ErrorGeneric"] = "❌ An error occurred while fetching the records. Please try again later.",
            ["Listagem_DateRetryHelp"] =
                "\n\nPlease enter a specific date or period (up to 7 days).\n" +
                "Examples:\n" +
                "• *10/10/2025*\n" +
                "• *01/10/2025 to 07/10/2025*\n" +
                "• *today*\n" +
                "• *yesterday*\n\n" +
                "💡 _Tip: You can write 'cancel' or 'exit' to cancel the request._",

            ["Listagem_DateToday"] = "today",
            ["Listagem_DateYesterday"] = "yesterday",

            ["Listagem_MockTitle"] = "📅 *Attendance Records*",
            ["Listagem_MockUser"] = "👤 *Employee:* {0}",
            ["Listagem_MockPeriod"] = "📆 *Period:* {0}",
            ["Listagem_MockSeparator"] = "──────────────────────────",
            ["Listagem_MockWeekend"] = "*• {0} ({1}):* 🛌 _Weekend_",
            ["Listagem_MockDayHeader"] = "*• {0} ({1}):*",
            ["Listagem_MockEntry"] = "   📥 08:30 - Entry (ELO Office)",
            ["Listagem_MockLunchOut"] = "   📤 12:30 - Exit (Lunch)",
            ["Listagem_MockLunchIn"] = "   📥 13:30 - Entry (Lunch)",
            ["Listagem_MockExit"] = "   📤 17:30 - Exit (End of Shift)",
            ["Listagem_MockNote"] = "💡 _Note: This data is for demonstration purposes while the Web Service integration is not active._",
            ["Listagem_DefaultUser"] = "Employee",

            ["Day_Monday"] = "Monday",
            ["Day_Tuesday"] = "Tuesday",
            ["Day_Wednesday"] = "Wednesday",
            ["Day_Thursday"] = "Thursday",
            ["Day_Friday"] = "Friday",
            ["Day_Saturday"] = "Saturday",
            ["Day_Sunday"] = "Sunday",

            // ═══════════════════════════════════════════════════════════════
            // VACATION LISTING
            // ═══════════════════════════════════════════════════════════════

            ["Ferias_CommandName"] = "vacation_listing",
            ["Ferias_Description"] = "View your vacations for a given year",

            ["Ferias_Expired"] = "⚠️ The previous request has expired. Please type *vacations* to start again.",
            ["Ferias_ConfirmYear"] = "Do you confirm you want to view your vacations for the year *{0}*? (yes/no)",
            ["Ferias_Cancelled"] = "❌ Vacation listing cancelled. If you need help, type *help*.",
            ["Ferias_AskYear"] =
                "Which year do you want to check your vacations for? Enter the year (e.g. *2025*, *2026*).\n\n" +
                "💡 _Tip: You can write 'cancel' or 'exit' to cancel the request._",

            ["Ferias_EmptyInput"] = "⚠️ The message text is empty.",
            ["Ferias_InvalidYear"] = "⚠️ I couldn't identify a valid year.",
            ["Ferias_YearRetryHelp"] =
                "\n\nPlease enter the desired year (e.g. *2025*, *2026*).\n\n" +
                "💡 _Tip: You can write 'cancel' or 'exit' to cancel the request._",

            ["Ferias_ErrorGeneric"] = "❌ An error occurred while fetching the vacations. Please try again later.",

            ["Ferias_Placeholder"] = "🏖️ The vacation listing feature for the year *{0}* will be available soon. Stay tuned!",

            // ═══════════════════════════════════════════════════════════════
            // AJUDA
            // ═══════════════════════════════════════════════════════════════

            ["Help_CommandName"] = "help",
            ["Help_Description"] = "Shows the list of available commands",
            ["Help_Title"] = "🤖 *Available commands:*",
            ["Help_CommandEntry"] = "▸ *{0}* — {1}",
            ["Help_TriggerLabel"] = "   _Type:_ {0}",
            ["Help_FooterSeparator"] = "───────────────────",
            ["Help_NotePresence"] = "📍 _Attendance note: marking requires a location PIN._",
            ["Help_NoteMobile"] = "📱 _If Web/PC doesn't allow PIN, mark attendance on your phone._",
            ["Help_Hint"] = "💡 _Type any of the commands above to get started._",

            // ═══════════════════════════════════════════════════════════════
            // CONFIRMAÇÕES
            // ═══════════════════════════════════════════════════════════════

            ["Confirmation_Prompt_1"] = "I received a request to record your attendance. Shall I proceed? (yes/no)",

            ["Confirmation_YesNoHelp_1"] = "❓ I asked about *{0}* — reply with YES or NO (y/n).",
            ["Confirmation_YesNoHelp_2"] = "📋 Still waiting: do you really want *{0}*? YES or NO (y/n).",
            ["Confirmation_YesNoHelp_3"] = "🗣️ This is about *{0}* — just reply YES or NO (y/n).",
            ["Confirmation_YesNoHelp_4"] = "💬 Do you want *{0}* or not? Reply YES or NO (y/n).",
            ["Confirmation_YesNoHelp_5"] = "✋ To proceed with *{0}*, confirm with YES or NO (y/n).",

            ["Confirmation_RemainingAttempts"] = "\n\n⏳ Remaining attempts: *{0}*",

            ["Confirmation_NoPending_1"] = "⚠️ I don't have any pending request to confirm.\n\nIf you sent a command before, it may have expired or the system was restarted.\n\nType *help* to see available commands.",
            ["Confirmation_NoPending_2"] = "⚠️ Hmm, I can't find any pending confirmation.\n\nThe request may have expired or was lost during a restart.\n\nTry sending the command again or type *help*.",
            ["Confirmation_NoPending_3"] = "⚠️ There's nothing awaiting confirmation right now.\n\nIf you sent something before, it may have been too long ago or the system restarted.\n\nType *help* to see available commands.",
            ["Confirmation_NoPending_4"] = "⚠️ Confirm what? I don't have any pending request.\n\nIf you sent something, it may have expired.\n\nType *help* to see what you can do.",
            ["Confirmation_NoPending_5"] = "⚠️ Nothing pending for confirmation.\n\nIt may have expired or was lost during a restart.\n\nUse *help* to see all commands.",
            ["Confirmation_NoPending_6"] = "⚠️ Oops, I have no record of a pending request from you.\n\nIf you sent something before, it may have been too long ago or there was a restart.\n\nType *help* for guidance.",

            ["Confirmation_InvalidFinal_1"] = "⚠️ The expected answer was *yes* or *no* for *{0}*.\n\nI'm cancelling this request for now. If you need help, type *help*.",
            ["Confirmation_InvalidFinal_2"] = "⚠️ That wasn't the expected answer for *{0}* — I needed *yes* or *no*.\n\nRequest cancelled. Type *help* to see the options.",
            ["Confirmation_InvalidFinal_3"] = "⚠️ For *{0}* I only expected *yes* or *no*.\n\nAfter 3 invalid attempts, I cancelled the request. Type *help* to continue.",
            ["Confirmation_InvalidFinal_4"] = "⚠️ Couldn't confirm *{0}* because the answer wasn't *yes*/*no*.\n\nConfirmation cancelled. Type *help* if needed.",
            ["Confirmation_InvalidFinal_5"] = "⚠️ The confirmation for *{0}* was cancelled: the answer was neither *yes* nor *no*.\n\nUse *help* to continue.",

            ["Confirmation_Cancelled"] = "❌ Ok, *{0}* cancelled. If you need help, type help.",

            // ═══════════════════════════════════════════════════════════════
            // LOCALIZAÇÃO
            // ═══════════════════════════════════════════════════════════════

            ["Location_Request_1"] = "📍 Great — to complete your *attendance*, send your location PIN from the app now.\nYou have *{0} seconds* to send your current location.",
            ["Location_Request_2"] = "📍 Just the location left to finish your *attendance*. Send the app's PIN.\nThe location must be *current* and you have *{0} seconds*.",
            ["Location_Request_3"] = "📍 Confirmed. To finish *attendance*, share your current location from the app.\nWindow: *{0} seconds*.",
            ["Location_Request_4"] = "📍 Almost there: send your app's location PIN to complete *attendance*.\nMax time: *{0} seconds*.",
            ["Location_Request_5"] = "📍 Last step to record *attendance*: send your current location PIN.\nOnly *current* location is accepted.",

            ["Location_Help_1"] = "📍 I still need your current location PIN to complete *attendance*.\nSend your location from the app within *{0} seconds*.",
            ["Location_Help_2"] = "🗺️ To finish *attendance*, send your current location from the app.\nThe window is short: *{0} seconds*.",
            ["Location_Help_3"] = "📌 To validate *attendance* I need the current location PIN.\nOld or edited locations won't work.",
            ["Location_Help_4"] = "📍 Current location is missing to validate *attendance*.\nYou need to send the PIN from the app quickly.",
            ["Location_Help_5"] = "🧭 To complete *attendance* I need your location PIN *now*.\nIf the window expires, you'll need to start over.",

            ["Location_Final_1"] = "⚠️ I didn't receive the location PIN in time. *Attendance* request cancelled.\nTo try again: type *present* and then send the PIN via *📎 Location*.",
            ["Location_Final_2"] = "⚠️ Without a location PIN within the time limit, I can't complete *attendance*. Request cancelled.",
            ["Location_Final_3"] = "⚠️ *Attendance* was cancelled because the location didn't arrive in time.\nWhenever you're ready, start again with *present*.",
            ["Location_Final_4"] = "⚠️ The location PIN window has expired. *Attendance* cancelled.\nNext time, send the location within the time limit.",
            ["Location_Final_5"] = "⚠️ *Attendance* request cancelled: location PIN was not received within the allowed window.",

            ["Location_Received"] = "📍 Location PIN received.",

            // ═══════════════════════════════════════════════════════════════
            // COMANDO DESCONHECIDO
            // ═══════════════════════════════════════════════════════════════

            ["Unknown_1"] = "🤔 Hmm, I didn't understand what you meant.\n\nType *help* to see what I can do.",
            ["Unknown_2"] = "❓ That message doesn't match any command.\n\nTry typing *help*.",
            ["Unknown_3"] = "🙈 I don't recognise that message.\n\nSend *menu* to see available options.",
            ["Unknown_4"] = "⚠️ Command not found.\n\nType *?* to see the command list.",
            ["Unknown_5"] = "🤷 I don't know how to do that yet!\n\nType *help* to see what's available.",
            ["Unknown_6"] = "📭 Unsupported message.\n\nType *help* to see the commands I accept.",

            // ═══════════════════════════════════════════════════════════════
            // MULTI-LÍNGUA
            // ═══════════════════════════════════════════════════════════════

            ["MultiLang_Hint"] = "Type *help*",
        });
    }
}
