using System.Text.Json;
using WebApplication1.Core.Models;

namespace WebApplication1.Api.Parsing
{
    internal static class WhatsAppWebhookParser
    {
        public static IEnumerable<IncomingMessage> ParseMessages(JsonElement root)
        {
            if (!root.TryGetProperty("entry", out JsonElement entryArray) || entryArray.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (JsonElement entry in entryArray.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out JsonElement changesArray) || changesArray.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement change in changesArray.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out JsonElement value) ||
                        !value.TryGetProperty("messages", out JsonElement messagesArray) ||
                        messagesArray.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement message in messagesArray.EnumerateArray())
                    {
                        if (TryParseMessage(message, out IncomingMessage parsedMessage))
                            yield return parsedMessage;
                    }
                }
            }
        }

        private static bool TryParseMessage(JsonElement message, out IncomingMessage result)
        {
            result = new IncomingMessage();

            string messageId = message.TryGetProperty("id", out JsonElement idEl)
                ? idEl.GetString() ?? string.Empty
                : string.Empty;

            string from = message.TryGetProperty("from", out JsonElement fromEl)
                ? fromEl.GetString() ?? string.Empty
                : string.Empty;

            string body = string.Empty;
            if (message.TryGetProperty("text", out JsonElement textEl) &&
                textEl.TryGetProperty("body", out JsonElement bodyEl))
            {
                body = bodyEl.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(body) &&
                TryExtractWhatsAppLocation(message, out double previewLatitude, out double previewLongitude, out _, out _))
            {
                body = $"localização partilhada ({previewLatitude:F6}, {previewLongitude:F6})";
            }

            DateTime sentAt = DateTime.MinValue;
            if (message.TryGetProperty("timestamp", out JsonElement tsEl))
            {
                if (tsEl.ValueKind == JsonValueKind.String &&
                    long.TryParse(tsEl.GetString(), out long unixSeconds))
                {
                    sentAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                }
                else if (tsEl.ValueKind == JsonValueKind.Number &&
                         tsEl.TryGetInt64(out long unixNum))
                {
                    sentAt = DateTimeOffset.FromUnixTimeSeconds(unixNum).UtcDateTime;
                }
            }

            string messageType = message.TryGetProperty("type", out JsonElement typeEl)
                ? typeEl.GetString() ?? string.Empty
                : string.Empty;

            double latitude = default;
            double longitude = default;
            string? locationName = null;
            string? locationAddress = null;

            bool hasLocation = string.Equals(messageType, "location", StringComparison.OrdinalIgnoreCase) &&
                TryExtractWhatsAppLocation(message, out latitude, out longitude, out locationName, out locationAddress);

            result = new IncomingMessage
            {
                MessageId = messageId,
                From = from,
                ReplyEndpoint = from,
                UserId = from,
                UserName = string.Empty,
                UserPhone = from,
                Body = body,
                OriginalBody = body,
                Platform = MessagePlatform.WhatsApp,
                ReceivedAt = DateTime.UtcNow,
                SentAt = sentAt != DateTime.MinValue ? sentAt : DateTime.UtcNow,
                HasLocation = hasLocation,
                Latitude = hasLocation ? latitude : null,
                Longitude = hasLocation ? longitude : null,
                LocationName = hasLocation ? locationName : null,
                LocationAddress = hasLocation ? locationAddress : null
            };

            return true;
        }

        private static bool TryExtractWhatsAppLocation(
            JsonElement message,
            out double latitude,
            out double longitude,
            out string? name,
            out string? address)
        {
            latitude = default;
            longitude = default;
            name = null;
            address = null;

            if (!message.TryGetProperty("location", out JsonElement locationEl) ||
                locationEl.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            bool latOk = false;
            bool lonOk = false;

            if (locationEl.TryGetProperty("latitude", out JsonElement latEl))
            {
                if (latEl.ValueKind == JsonValueKind.Number)
                    latOk = latEl.TryGetDouble(out latitude);
                else if (latEl.ValueKind == JsonValueKind.String)
                    latOk = double.TryParse(latEl.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out latitude);
            }

            if (locationEl.TryGetProperty("longitude", out JsonElement lonEl))
            {
                if (lonEl.ValueKind == JsonValueKind.Number)
                    lonOk = lonEl.TryGetDouble(out longitude);
                else if (lonEl.ValueKind == JsonValueKind.String)
                    lonOk = double.TryParse(lonEl.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out longitude);
            }

            if (locationEl.TryGetProperty("name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String)
                name = nameEl.GetString();

            if (locationEl.TryGetProperty("address", out JsonElement addrEl) && addrEl.ValueKind == JsonValueKind.String)
                address = addrEl.GetString();

            return latOk && lonOk;
        }
    }
}
