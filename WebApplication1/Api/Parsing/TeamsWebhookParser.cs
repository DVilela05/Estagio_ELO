using System.Text.Json;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Messaging;

namespace WebApplication1.Api.Parsing
{
    internal static class TeamsWebhookParser
    {
        public static bool IsReadReceipt(TeamsActivity activity, out string? lastReadMessageId)
        {
            lastReadMessageId = null;

            if (!string.Equals(activity.Type, "event", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(activity.Name, "application/vnd.microsoft.readReceipt", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (activity.Value.HasValue &&
                activity.Value.Value.ValueKind == JsonValueKind.Object &&
                activity.Value.Value.TryGetProperty("lastReadMessageId", out JsonElement lastReadEl))
            {
                lastReadMessageId = lastReadEl.GetString();
            }

            return true;
        }

        public static bool TryParseMessage(TeamsActivity activity, JsonElement root, out IncomingMessage message)
        {
            message = new IncomingMessage();

            bool isMessageOrInvoke =
                string.Equals(activity.Type, "message", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(activity.Type, "invoke", StringComparison.OrdinalIgnoreCase);

            if (!isMessageOrInvoke)
                return false;

            bool hasLocation = TryExtractTeamsLocation(
                activity,
                root,
                out double latitude,
                out double longitude,
                out string? locationName,
                out string? locationAddress);

            string cleanText = WebhookJsonHelpers.RemoveBotMention(activity.Text);

            if (string.IsNullOrWhiteSpace(cleanText) && hasLocation)
                cleanText = $"localização partilhada ({latitude:F6}, {longitude:F6})";

            if (string.IsNullOrWhiteSpace(cleanText) && !hasLocation)
                cleanText = "(mensagem vazia)";

            string serviceUrl = activity.ServiceUrl?.TrimEnd('/') ?? string.Empty;
            string conversationId = activity.Conversation?.Id ?? string.Empty;
            string activityId = activity.Id ?? string.Empty;
            string replyEndpoint = $"{serviceUrl}/v3/conversations/{conversationId}/activities/{activityId}";
            string senderKey = activity.From?.AadObjectId
                ?? activity.From?.Id
                ?? conversationId;

            DateTime activityTime = activity.Timestamp != default ? activity.Timestamp : DateTime.UtcNow;

            message = new IncomingMessage
            {
                MessageId = activity.Id ?? string.Empty,
                From = senderKey,
                ReplyEndpoint = replyEndpoint,
                // Preferir identificador estável de conta (AAD Object ID) para
                // permissões/admin e correlação entre mensagens.
                UserId = activity.From?.AadObjectId
                    ?? activity.From?.Id
                    ?? activity.From?.UserPrincipalName
                    ?? senderKey,
                UserName = activity.From?.Name ?? string.Empty,
                UserEmail = activity.From?.UserPrincipalName,
                Body = cleanText,
                OriginalBody = cleanText,
                Platform = MessagePlatform.Teams,
                ReceivedAt = DateTime.UtcNow,
                SentAt = activityTime,
                HasLocation = hasLocation,
                Latitude = hasLocation ? latitude : null,
                Longitude = hasLocation ? longitude : null,
                LocationName = hasLocation ? locationName : null,
                LocationAddress = hasLocation ? locationAddress : null
            };

            return true;
        }

        private static bool TryExtractTeamsLocation(
            TeamsActivity activity,
            JsonElement root,
            out double latitude,
            out double longitude,
            out string? name,
            out string? address)
        {
            latitude = default;
            longitude = default;
            name = null;
            address = null;

            if (activity.Entities.HasValue &&
                activity.Entities.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement entity in activity.Entities.Value.EnumerateArray())
                {
                    if (TryExtractCoordinatesFromTeamsObject(entity, out latitude, out longitude, out name, out address))
                        return true;
                }
            }

            if (activity.Attachments.HasValue &&
                activity.Attachments.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement attachment in activity.Attachments.Value.EnumerateArray())
                {
                    if (TryExtractCoordinatesFromTeamsObject(attachment, out latitude, out longitude, out name, out address))
                        return true;

                    if (WebhookJsonHelpers.TryGetPropertyIgnoreCase(attachment, "content", out JsonElement content) &&
                        content.ValueKind == JsonValueKind.Object &&
                        TryExtractCoordinatesFromTeamsObject(content, out latitude, out longitude, out name, out address))
                    {
                        return true;
                    }
                }
            }

            if (WebhookJsonHelpers.TryGetPropertyIgnoreCase(root, "value", out JsonElement valueEl) &&
                valueEl.ValueKind == JsonValueKind.Object &&
                TryExtractCoordinatesFromTeamsObject(valueEl, out latitude, out longitude, out name, out address))
            {
                return true;
            }

            return false;
        }

        private static bool TryExtractCoordinatesFromTeamsObject(
            JsonElement source,
            out double latitude,
            out double longitude,
            out string? name,
            out string? address)
        {
            latitude = default;
            longitude = default;
            name = null;
            address = null;

            if (source.ValueKind != JsonValueKind.Object)
                return false;

            if (WebhookJsonHelpers.TryGetPropertyIgnoreCase(source, "name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String)
                name = nameEl.GetString();

            if (WebhookJsonHelpers.TryGetPropertyIgnoreCase(source, "address", out JsonElement addressEl))
            {
                if (addressEl.ValueKind == JsonValueKind.String)
                    address = addressEl.GetString();
                else if (addressEl.ValueKind == JsonValueKind.Object &&
                         WebhookJsonHelpers.TryGetPropertyIgnoreCase(addressEl, "streetAddress", out JsonElement streetEl) &&
                         streetEl.ValueKind == JsonValueKind.String)
                    address = streetEl.GetString();
            }

            bool latOk = WebhookJsonHelpers.TryReadDoubleProperty(source, "latitude", out latitude);
            bool lonOk = WebhookJsonHelpers.TryReadDoubleProperty(source, "longitude", out longitude);
            if (latOk && lonOk)
                return true;

            if (WebhookJsonHelpers.TryGetPropertyIgnoreCase(source, "geo", out JsonElement geoEl) &&
                geoEl.ValueKind == JsonValueKind.Object)
            {
                latOk = WebhookJsonHelpers.TryReadDoubleProperty(geoEl, "latitude", out latitude);
                lonOk = WebhookJsonHelpers.TryReadDoubleProperty(geoEl, "longitude", out longitude);
                if (latOk && lonOk)
                    return true;
            }

            if (WebhookJsonHelpers.TryGetPropertyIgnoreCase(source, "coordinates", out JsonElement coordEl) &&
                coordEl.ValueKind == JsonValueKind.Object)
            {
                latOk = WebhookJsonHelpers.TryReadDoubleProperty(coordEl, "latitude", out latitude);
                lonOk = WebhookJsonHelpers.TryReadDoubleProperty(coordEl, "longitude", out longitude);
                if (latOk && lonOk)
                    return true;
            }

            return false;
        }
    }
}
