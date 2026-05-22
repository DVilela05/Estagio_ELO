using System.Collections.ObjectModel;

namespace WebApplication1.Resources
{
    /// <summary>
    /// Mensagens em Francês (FR).
    /// </summary>
    internal static class Messages_FR
    {
        public static readonly ReadOnlyDictionary<string, string> Strings = new(new Dictionary<string, string>
        {
            // ═══════════════════════════════════════════════════════════════
            // TRIGGERS
            // ═══════════════════════════════════════════════════════════════

            ["Presence_Triggers"] =
                "présent|présence|presence|marquer présence|marquer presence|" +
                "pointage|je suis là|je suis la|arrivé|arrive",

            ["Listagem_Triggers"] =
                "voir pointages|voir présences|pointages|présences|" +
                "liste pointages|liste présences|lister pointages|lister présences|" +
                "mes pointages|mes présences|voir assiduité|assiduité",

            ["Help_Triggers"] = "aide|help|commandes",

            ["YesTokens"] = "oui|o|yes|y",
            ["NoTokens"] = "non|n|no",

            // ═══════════════════════════════════════════════════════════════
            // PRESENÇA
            // ═══════════════════════════════════════════════════════════════

            ["Presence_CommandName"] = "présence",
            ["Presence_Description"] = "Marquer la présence (nécessite un PIN de localisation ; si le Web/PC ne le permet pas, utilisez votre téléphone)",

            ["Presence_Success"] = "✅ Présence enregistrée avec succès !",
            ["Presence_SuccessStub"] = "✅ Message de *présence* reçu avec succès.",

            ["Presence_ErrorTimeout"] = "⏱️ Le serveur a mis trop de temps à répondre. Votre présence n'a pas été enregistrée — réessayez dans quelques minutes.",
            ["Presence_ErrorUnavailable"] = "🔌 Le serveur d'enregistrement est temporairement indisponible. Réessayez plus tard.",
            ["Presence_ErrorGeneric"] = "⚠️ Un problème est survenu lors de l'enregistrement de la présence. L'équipe technique a été notifiée. Réessayez plus tard.",

            ["Presence_ErrorUnknownApi"] = "⚠️ Une erreur interne s'est produite (API inconnue). L'équipe technique a été notifiée.",
            ["Presence_ErrorEmailMandatory"] = "⚠️ Impossible d'identifier votre e-mail professionnel. Contactez le support pour vérifier la configuration de votre compte Teams.",
            ["Presence_ErrorNumberMandatory"] = "⚠️ Impossible de lire votre numéro de téléphone pour l'associer à votre fiche collaborateur.",
            ["Presence_ErrorTokenMandatory"] = "⚠️ Erreur de sécurité (jeton manquant). Réessayez plus tard.",
            ["Presence_ErrorInvalidDatetime"] = "⚠️ La date du message n'a pas été comprise par le serveur. Réessayez d'envoyer votre présence.",
            ["Presence_ErrorLoginError"] = "⚠️ Une erreur s'est produite lors de la validation de vos données. Vérifiez auprès des Ressources Humaines que votre {0} est correctement associé(e) à votre fiche. Tapez *aide* pour plus d'informations.",
            ["Presence_ErrorDecodeError"] = "⚠️ Une erreur de transmission des données s'est produite. Réessayez.",
            ["Presence_ErrorBusinessGeneric"] = "⚠️ Impossible d'enregistrer la présence. Erreur du serveur : {0}",
            ["Presence_ErrorUnexpectedResponse"] = "⚠️ Réponse inattendue du serveur : {0}",

            ["Presence_IdType_Email"] = "e-mail",
            ["Presence_IdType_Phone"] = "nº de téléphone",

            // ═══════════════════════════════════════════════════════════════
            // LISTAGEM
            // ═══════════════════════════════════════════════════════════════

            ["Listagem_CommandName"] = "liste_pointages",
            ["Listagem_Description"] = "Voir vos pointages de présence pour une période donnée (maximum 7 jours)",

            ["Listagem_Expired"] = "⚠️ La demande précédente a expiré. Veuillez taper *voir pointages* pour recommencer.",
            ["Listagem_ConfirmSingle"] = "Confirmez-vous vouloir voir les pointages pour la date du *{0}* ? (oui/non)",
            ["Listagem_ConfirmRange"] = "Confirmez-vous vouloir voir les pointages pour la période du *{0} au {1}* ? (oui/non)",
            ["Listagem_Cancelled"] = "❌ Demande de liste annulée. Si vous avez besoin d'aide, tapez *aide*.",
            ["Listagem_AskPeriod"] =
                "Quelle période souhaitez-vous consulter ? Vous pouvez indiquer une date (ex : *10/10/2025*, *aujourd'hui*, *hier*) ou une période jusqu'à 7 jours (ex : *01/10/2025 à 07/10/2025*).\n\n" +
                "💡 _Astuce : Vous pouvez écrire 'annuler' ou 'quitter' pour annuler la demande._",

            ["Listagem_EmptyInput"] = "⚠️ Le texte envoyé est vide.",
            ["Listagem_NoDateFound"] = "⚠️ Je n'ai trouvé aucune date ou mot-clé de période valide.",
            ["Listagem_PeriodTooLong"] = "⚠️ La période d'analyse ({0} jours) dépasse le maximum de 7 jours.",
            ["Listagem_InvalidFormat"] = "⚠️ Format invalide. Veuillez n'entrer que les dates.",
            
            // Erreurs WebService
            ["Listagem_Error_InvalidStartDateTime"] = "⚠️ La date de début indiquée est invalide.",
            ["Listagem_Error_InvalidDateTimePeriod"] = "⚠️ La période de dates indiquée est invalide.",
            ["Listagem_Error_InvalidNrDaysPeriod"] = "⚠️ La période indiquée dépasse la limite autorisée (maximum 7 jours).",
            ["Listagem_Error_Null"] = "⚠️ Une erreur s'est produite lors du traitement des pointages. Veuillez réessayer plus tard.",
            ["Listagem_Error_NoTimeBookings"] = "ℹ️ Il n'y a pas de pointages enregistrés pour la période indiquée.",
            ["Listagem_ErrorGeneric"] = "❌ Une erreur s'est produite lors de la consultation des pointages. Veuillez réessayer plus tard.",
            ["Listagem_DateRetryHelp"] =
                "\n\nVeuillez indiquer une date précise ou une période (maximum 7 jours).\n" +
                "Exemples :\n" +
                "• *10/10/2025*\n" +
                "• *01/10/2025 à 07/10/2025*\n" +
                "• *aujourd'hui*\n" +
                "• *hier*\n\n" +
                "💡 _Astuce : Vous pouvez écrire 'annuler' ou 'quitter' pour annuler la demande._",

            ["Listagem_DateToday"] = "aujourd'hui",
            ["Listagem_DateYesterday"] = "hier",

            ["Listagem_MockTitle"] = "📅 *Liste des pointages de présence*",
            ["Listagem_MockUser"] = "👤 *Collaborateur :* {0}",
            ["Listagem_MockPeriod"] = "📆 *Période :* {0}",
            ["Listagem_MockSeparator"] = "──────────────────────────",
            ["Listagem_MockWeekend"] = "*• {0} ({1}) :* 🛌 _Week-end_",
            ["Listagem_MockDayHeader"] = "*• {0} ({1}) :*",
            ["Listagem_MockEntry"] = "   📥 08:30 - Entrée (Bureau ELO)",
            ["Listagem_MockLunchOut"] = "   📤 12:30 - Sortie (Déjeuner)",
            ["Listagem_MockLunchIn"] = "   📥 13:30 - Entrée (Déjeuner)",
            ["Listagem_MockExit"] = "   📤 17:30 - Sortie (Fin de service)",
            ["Listagem_MockNote"] = "💡 _Note : Ces données sont de démonstration en attendant l'activation de l'intégration avec le Web Service._",
            ["Listagem_DefaultUser"] = "Collaborateur",

            ["Day_Monday"] = "Lundi",
            ["Day_Tuesday"] = "Mardi",
            ["Day_Wednesday"] = "Mercredi",
            ["Day_Thursday"] = "Jeudi",
            ["Day_Friday"] = "Vendredi",
            ["Day_Saturday"] = "Samedi",
            ["Day_Sunday"] = "Dimanche",

            // ═══════════════════════════════════════════════════════════════
            // AJUDA
            // ═══════════════════════════════════════════════════════════════

            ["Help_CommandName"] = "aide",
            ["Help_Description"] = "Affiche la liste des commandes disponibles",
            ["Help_Title"] = "🤖 *Commandes disponibles :*",
            ["Help_CommandEntry"] = "▸ *{0}* — {1}",
            ["Help_TriggerLabel"] = "   _Tapez :_ {0}",
            ["Help_FooterSeparator"] = "───────────────────",
            ["Help_NotePresence"] = "📍 _Note de présence : le pointage nécessite un PIN de localisation._",
            ["Help_NoteMobile"] = "📱 _Si le Web/PC ne permet pas l'envoi du PIN, marquez la présence sur le téléphone._",
            ["Help_Hint"] = "💡 _Tapez l'une des commandes ci-dessus pour commencer._",

            // ═══════════════════════════════════════════════════════════════
            // CONFIRMAÇÕES
            // ═══════════════════════════════════════════════════════════════

            ["Confirmation_Prompt_1"] = "J'ai reçu une demande d'enregistrement de présence. Puis-je continuer ? (oui/non)",

            ["Confirmation_YesNoHelp_1"] = "❓ J'ai posé une question sur *{0}* — répondez par OUI ou NON (o/n).",
            ["Confirmation_YesNoHelp_2"] = "📋 J'attends toujours : voulez-vous vraiment *{0}* ? OUI ou NON (o/n).",
            ["Confirmation_YesNoHelp_3"] = "🗣️ C'est à propos de *{0}* — répondez simplement OUI ou NON (o/n).",
            ["Confirmation_YesNoHelp_4"] = "💬 Voulez-vous *{0}* ou non ? Répondez OUI ou NON (o/n).",
            ["Confirmation_YesNoHelp_5"] = "✋ Pour continuer avec *{0}*, confirmez par OUI ou NON (o/n).",

            ["Confirmation_RemainingAttempts"] = "\n\n⏳ Tentatives restantes : *{0}*",

            ["Confirmation_NoPending_1"] = "⚠️ Je n'ai aucune demande en attente de confirmation.\n\nSi vous avez envoyé une commande avant, elle a peut-être expiré ou le système a redémarré.\n\nTapez *aide* pour voir les commandes disponibles.",
            ["Confirmation_NoPending_2"] = "⚠️ Hmm, je ne trouve aucune confirmation en attente.\n\nLa demande a peut-être expiré ou a été perdue lors d'un redémarrage.\n\nEssayez d'envoyer la commande à nouveau ou tapez *aide*.",
            ["Confirmation_NoPending_3"] = "⚠️ Il n'y a rien en attente de confirmation pour le moment.\n\nSi vous avez envoyé quelque chose avant, cela fait peut-être trop longtemps ou le système a redémarré.\n\nTapez *aide* pour voir les commandes.",
            ["Confirmation_NoPending_4"] = "⚠️ Confirmer quoi ? Je n'ai aucune demande en attente.\n\nSi vous aviez envoyé quelque chose, cela a peut-être expiré.\n\nTapez *aide* pour voir vos options.",
            ["Confirmation_NoPending_5"] = "⚠️ Rien en attente de confirmation.\n\nCela a peut-être expiré ou a été perdu lors d'un redémarrage.\n\nUtilisez *aide* pour voir toutes les commandes.",
            ["Confirmation_NoPending_6"] = "⚠️ Oups, je n'ai aucun enregistrement d'une demande en attente de votre part.\n\nSi vous avez envoyé quelque chose avant, cela fait peut-être trop longtemps.\n\nTapez *aide* pour savoir quoi faire.",

            ["Confirmation_InvalidFinal_1"] = "⚠️ La réponse attendue était *oui* ou *non* pour *{0}*.\n\nJ'annule cette demande pour le moment. Si besoin, tapez *aide*.",
            ["Confirmation_InvalidFinal_2"] = "⚠️ Ce n'était pas la réponse attendue pour *{0}* — il fallait *oui* ou *non*.\n\nDemande annulée. Tapez *aide* pour les options.",
            ["Confirmation_InvalidFinal_3"] = "⚠️ Pour *{0}* j'attendais seulement *oui* ou *non*.\n\nAprès 3 tentatives invalides, j'ai annulé la demande. Tapez *aide* pour continuer.",
            ["Confirmation_InvalidFinal_4"] = "⚠️ Impossible de confirmer *{0}* car la réponse n'était pas *oui*/*non*.\n\nConfirmation annulée. Tapez *aide* si nécessaire.",
            ["Confirmation_InvalidFinal_5"] = "⚠️ La confirmation de *{0}* a été annulée : la réponse n'était ni *oui* ni *non*.\n\nUtilisez *aide* pour reprendre.",

            ["Confirmation_Cancelled"] = "❌ D'accord, *{0}* annulé. Si vous avez besoin d'aide, tapez aide.",

            // ═══════════════════════════════════════════════════════════════
            // LOCALIZAÇÃO
            // ═══════════════════════════════════════════════════════════════

            ["Location_Request_1"] = "📍 Parfait — pour finaliser la *présence*, envoyez maintenant le PIN de localisation depuis l'application.\nVous avez *{0} secondes* pour envoyer votre localisation actuelle.",
            ["Location_Request_2"] = "📍 Il ne manque que la localisation pour finaliser la *présence*. Envoyez le PIN de l'app.\nLa localisation doit être *actuelle* et vous avez *{0} secondes*.",
            ["Location_Request_3"] = "📍 Confirmé. Pour terminer la *présence*, partagez votre localisation actuelle depuis l'app.\nDélai : *{0} secondes*.",
            ["Location_Request_4"] = "📍 Presque fini : envoyez le PIN de localisation de votre app pour compléter la *présence*.\nTemps maximum : *{0} secondes*.",
            ["Location_Request_5"] = "📍 Dernière étape pour enregistrer la *présence* : envoyez le PIN de votre localisation actuelle.\nSeule la localisation *actuelle* est acceptée.",

            ["Location_Help_1"] = "📍 J'ai encore besoin du PIN de localisation actuelle pour finaliser la *présence*.\nEnvoyez votre localisation depuis l'app dans les *{0} secondes*.",
            ["Location_Help_2"] = "🗺️ Pour terminer la *présence*, envoyez votre localisation actuelle depuis l'app.\nLe délai est court : *{0} secondes*.",
            ["Location_Help_3"] = "📌 Pour valider la *présence* j'ai besoin du PIN de localisation actuelle.\nUne localisation ancienne ou modifiée ne fonctionnera pas.",
            ["Location_Help_4"] = "📍 La localisation actuelle manque pour valider la *présence*.\nVous devez envoyer le PIN depuis l'app rapidement.",
            ["Location_Help_5"] = "🧭 Pour compléter la *présence* j'ai besoin de votre PIN de localisation *maintenant*.\nSi le délai expire, vous devrez recommencer.",

            ["Location_Final_1"] = "⚠️ Je n'ai pas reçu le PIN de localisation à temps. Demande de *présence* annulée.\nPour réessayer : tapez *présent* puis envoyez le PIN via *📎 Localisation*.",
            ["Location_Final_2"] = "⚠️ Sans PIN de localisation dans le temps imparti, impossible de compléter la *présence*. Demande annulée.",
            ["Location_Final_3"] = "⚠️ La *présence* a été annulée car la localisation n'est pas arrivée à temps.\nQuand vous êtes prêt(e), recommencez avec *présent*.",
            ["Location_Final_4"] = "⚠️ Le délai d'envoi du PIN a expiré. *Présence* annulée.\nLa prochaine fois, envoyez la localisation dans le délai imparti.",
            ["Location_Final_5"] = "⚠️ Demande de *présence* annulée : le PIN de localisation n'a pas été reçu dans le délai autorisé.",

            ["Location_Received"] = "📍 PIN de localisation reçu.",

            // ═══════════════════════════════════════════════════════════════
            // COMANDO DESCONHECIDO
            // ═══════════════════════════════════════════════════════════════

            ["Unknown_1"] = "🤔 Hmm, je n'ai pas compris ce que vous vouliez dire.\n\nTapez *aide* pour voir ce que je peux faire.",
            ["Unknown_2"] = "❓ Ce message ne correspond à aucune commande.\n\nEssayez de taper *aide*.",
            ["Unknown_3"] = "🙈 Je ne reconnais pas ce message.\n\nEnvoyez *menu* pour voir les options disponibles.",
            ["Unknown_4"] = "⚠️ Commande introuvable.\n\nTapez *?* pour voir la liste des commandes.",
            ["Unknown_5"] = "🤷 Je ne sais pas encore faire ça !\n\nTapez *aide* pour voir ce qui est disponible.",
            ["Unknown_6"] = "📭 Message non pris en charge.\n\nTapez *aide* pour voir les commandes acceptées.",

            ["MultiLang_Hint"] = "Tapez *aide*",
        });
    }
}
