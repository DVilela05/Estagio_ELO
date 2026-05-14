using System.Text.Json;

namespace WebApplication1.Api.Parsing
{
    internal static class WebhookJsonHelpers
    {
        public static bool TryGetPropertyIgnoreCase(JsonElement source, string propertyName, out JsonElement value)
        {
            foreach (JsonProperty prop in source.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryReadDoubleProperty(JsonElement source, string propertyName, out double value)
        {
            value = default;

            if (!TryGetPropertyIgnoreCase(source, propertyName, out JsonElement propEl))
                return false;

            if (propEl.ValueKind == JsonValueKind.Number)
                return propEl.TryGetDouble(out value);

            if (propEl.ValueKind == JsonValueKind.String)
            {
                return double.TryParse(
                    propEl.GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value);
            }

            return false;
        }

        public static string RemoveBotMention(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return System.Text.RegularExpressions.Regex
                .Replace(text, @"<at>.*?</at>\s*", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .Trim();
        }
    }
}
