using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Collections.Concurrent;
using WebApplication1.Core.Interfaces;
using WebApplication1.Core.Models;
using WebApplication1.Infrastructure.Configuration;

namespace WebApplication1.Core.Commands
{
    public class AdminCommandHandler : ICommandHandler
    {
        private sealed class PendingAdminCommand
        {
            public string Action { get; init; } = "menu";
            public DateTime CreatedAtUtc { get; init; }
        }

        private sealed class PendingMenuUnlock
        {
            public DateTime CreatedAtUtc { get; init; }
        }

        private sealed class MenuActionGrant
        {
            public DateTime CreatedAtUtc { get; init; }
        }

        private static readonly ConcurrentDictionary<string, PendingAdminCommand> _pendingCommands = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, PendingMenuUnlock> _pendingMenuUnlocks = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, MenuActionGrant> _menuActionGrants = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan _pendingCommandExpiry = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan _menuUnlockExpiry = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan _menuActionGrantExpiry = TimeSpan.FromMinutes(3);
        private static readonly string[] _fakeNotFoundReplies =
        {
            "Comando inválido.",
            "Comando não reconhecido.",
            "Esse comando não existe.",
            "Não foi possível processar esse comando."
        };

        private readonly IBusinessApiClient _businessApiClient;
        private readonly AdminSettings _adminSettings;
        private readonly BusinessApiSettings _businessApiSettings;
        private readonly IHostEnvironment _hostEnvironment;

        public AdminCommandHandler(
            IBusinessApiClient businessApiClient,
            IOptions<AdminSettings> adminOptions,
            IOptions<BusinessApiSettings> businessApiOptions,
            IHostEnvironment hostEnvironment)
        {
            _businessApiClient = businessApiClient;
            _adminSettings = adminOptions.Value;
            _businessApiSettings = businessApiOptions.Value;
            _hostEnvironment = hostEnvironment;
        }

        public string CommandName => "adminMenu";

        public string Description => "Menu de administração (configuração rápida e ping ao serviço web)";

        public string[] Triggers => new[] { "admin", "adminMenu", "adminPing", "adminConfig" };

        public bool CanHandle(IncomingMessage message)
        {
            string raw = (message.OriginalBody ?? string.Empty).Trim();
            return IsAdminUnlockCode(raw)
                || TryParseUnlockAndCommand(raw, out _)
                || IsAdminLookingCommand(raw);
        }

        public async Task<string> ExecuteAsync(IncomingMessage message)
        {
            if (!_adminSettings.Enabled)
                return "⛔ O menu de administração está desativado.";

            string text = (message.OriginalBody ?? string.Empty).Trim();

            if (!IsAuthorized(message))
                return FakeNotFoundReply();

            if (TryParseUnlockAndCommand(text, out string chainedAction))
            {
                return await ExecuteActionAsync(chainedAction);
            }

            if (IsAdminUnlockCode(text))
            {
                if (TryConsumePendingAction(message, out string pendingAction))
                    return await ExecuteActionAsync(pendingAction);

                if (TryConsumePendingMenuUnlock(message))
                {
                    GrantSingleMenuAction(message);
                    return BuildMenuReply();
                }

                return FakeNotFoundReply();
            }

            if (TryGetRequestedAction(text, out string requestedAction))
            {
                if (requestedAction == "menu")
                {
                    StorePendingMenuUnlock(message);
                    return FakeNotFoundReply();
                }

                if (TryConsumeSingleMenuAction(message))
                    return await ExecuteActionAsync(requestedAction);

                StorePendingAction(message, requestedAction);
                return FakeNotFoundReply();
            }

            return BuildMenuReply();
        }

        private async Task<string> ExecuteActionAsync(string action)
        {
            return action switch
            {
                "ping" => await BuildPingReplyAsync(),
                "config" => BuildConfigReply(),
                "menu" => BuildMenuReply(),
                _ => BuildMenuReply()
            };
        }

        private async Task<string> BuildPingReplyAsync()
        {
            var sw = Stopwatch.StartNew();
            bool available = await _businessApiClient.IsAvailableAsync();
            sw.Stop();

            string status = available ? "UP" : "DOWN";
            string mode = _businessApiSettings.IsStubMode ? "STUB" : "REAL";
            string baseUrl = string.IsNullOrWhiteSpace(_businessApiSettings.BaseUrl)
                ? "(não definido)"
                : _businessApiSettings.BaseUrl;
            string path = string.IsNullOrWhiteSpace(_businessApiSettings.AttendancePath)
                ? "/api/attendance"
                : _businessApiSettings.AttendancePath;

            return string.Join("\n", new[]
            {
                "🧪 *Relatório técnico — Admin Ping*",
                "",
                "*Resultado*",
                $"• Resultado: {(available ? "✅ OK" : "❌ FALHA")}",
                $"• Estado: {status}",
                $"• Latência: {sw.ElapsedMilliseconds} ms",
                "",
                "*Ambiente*",
                $"• Ambiente: {_hostEnvironment.EnvironmentName}",
                $"• Modo BusinessApi: {mode}",
                "",
                "*Endpoint*",
                $"• BaseUrl: {baseUrl}",
                $"• AttendancePath: {path}",
                "",
                "*Segurança e Resiliência*",
                $"• Security configured: {_businessApiSettings.HasServiceSecurity}",
                $"• Timeout: {_businessApiSettings.TimeoutSeconds}s",
                $"• MaxRetries: {_businessApiSettings.MaxRetries}",
                "",
                "*Timestamp*",
                $"• UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
            });
        }

        private string BuildConfigReply()
        {
            string mode = _businessApiSettings.IsStubMode ? "STUB" : "REAL";
            string baseUrl = string.IsNullOrWhiteSpace(_businessApiSettings.BaseUrl)
                ? "(não definido)"
                : _businessApiSettings.BaseUrl;
            string path = string.IsNullOrWhiteSpace(_businessApiSettings.AttendancePath)
                ? "/api/attendance"
                : _businessApiSettings.AttendancePath;

            return string.Join("\n", new[]
            {
                "⚙️ *Configuração atual (resumo):*",
                "",
                "*Ambiente*",
                $"• Ambiente: {_hostEnvironment.EnvironmentName}",
                "",
                "*BusinessApi*",
                $"• BusinessApi modo: {mode}",
                $"• BusinessApi BaseUrl: {baseUrl}",
                $"• BusinessApi AttendancePath: {path}",
                "",
                "🔐 Segredos não são mostrados por segurança.",
                "💡 Formato da pass: adminDDMMYYYY."
            });
        }

        private static string BuildMenuReply()
        {
            var lines = new List<string>
            {
                "🛠️ *Menu de administrador*",
                "",
                "*Diagnóstico*",
                "• adminPing",
                "  └ testa a ligação ao BusinessApi",
                "",
                "*Configuração*",
                "• adminConfig",
                "  └ mostra ambiente, modo e endpoint atual",
                "",
                "*Reautenticação*",
                "• Este menu dá acesso a *1 ação*.",
                "• Depois de executar, repete:",
                "  1) adminMenu",
                "  2) adminDDMMYYYY"
            };

            return string.Join("\n", lines);
        }

        private static bool TryGetRequestedAction(string text, out string action)
        {
            action = "menu";

            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (text.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("adminMenu", StringComparison.OrdinalIgnoreCase))
            {
                action = "menu";
                return true;
            }

            if (text.Equals("adminPing", StringComparison.OrdinalIgnoreCase))
            {
                action = "ping";
                return true;
            }

            if (text.Equals("adminConfig", StringComparison.OrdinalIgnoreCase))
            {
                action = "config";
                return true;
            }

            return false;
        }

        private static bool IsAdminLookingCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return text.Equals("admin", StringComparison.OrdinalIgnoreCase)
                || text.Equals("adminMenu", StringComparison.OrdinalIgnoreCase)
                || text.Equals("adminPing", StringComparison.OrdinalIgnoreCase)
                || text.Equals("adminConfig", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAdminUnlockCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string todayCompact = DateTime.Now.ToString("ddMMyyyy");

            return text.Equals($"admin{todayCompact}", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseUnlockAndCommand(string text, out string action)
        {
            action = "menu";

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string[] parts = text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
                return false;

            if (IsAdminUnlockCode(parts[0]) && TryGetRequestedAction(parts[1], out action))
                return true;

            if (TryGetRequestedAction(parts[0], out action) && IsAdminUnlockCode(parts[1]))
                return true;

            return false;
        }

        private string GetPendingKey(IncomingMessage message)
        {
            return Normalize(!string.IsNullOrWhiteSpace(message.UserId) ? message.UserId : message.From);
        }

        private void StorePendingAction(IncomingMessage message, string action)
        {
            string key = GetPendingKey(message);
            _menuActionGrants.TryRemove(key, out _);
            _pendingCommands[key] = new PendingAdminCommand
            {
                Action = action,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        private bool TryConsumePendingAction(IncomingMessage message, out string action)
        {
            action = "menu";

            string key = GetPendingKey(message);

            if (!_pendingCommands.TryRemove(key, out var pending))
                return false;

            if (DateTime.UtcNow > pending.CreatedAtUtc.Add(_pendingCommandExpiry))
                return false;

            action = pending.Action;
            return true;
        }

        private void StorePendingMenuUnlock(IncomingMessage message)
        {
            string key = GetPendingKey(message);
            _pendingCommands.TryRemove(key, out _);
            _menuActionGrants.TryRemove(key, out _);
            _pendingMenuUnlocks[key] = new PendingMenuUnlock
            {
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        private bool TryConsumePendingMenuUnlock(IncomingMessage message)
        {
            string key = GetPendingKey(message);

            if (!_pendingMenuUnlocks.TryRemove(key, out var pending))
                return false;

            return DateTime.UtcNow <= pending.CreatedAtUtc.Add(_menuUnlockExpiry);
        }

        private void GrantSingleMenuAction(IncomingMessage message)
        {
            string key = GetPendingKey(message);
            _pendingCommands.TryRemove(key, out _);
            _menuActionGrants[key] = new MenuActionGrant
            {
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        private bool TryConsumeSingleMenuAction(IncomingMessage message)
        {
            string key = GetPendingKey(message);

            if (!_menuActionGrants.TryRemove(key, out var grant))
                return false;

            return DateTime.UtcNow <= grant.CreatedAtUtc.Add(_menuActionGrantExpiry);
        }

        private static string ActionToCommand(string action)
        {
            return action switch
            {
                "ping" => "adminPing",
                "config" => "adminConfig",
                _ => "adminMenu"
            };
        }

        private static string FakeNotFoundReply()
        {
            return _fakeNotFoundReplies[Random.Shared.Next(_fakeNotFoundReplies.Length)];
        }

        private bool IsAuthorized(IncomingMessage message)
        {
            var allowedUserIds = BuildNormalizedSet(_adminSettings.AllowedUserIds);
            var allowedPhones = BuildNormalizedSet(_adminSettings.AllowedPhones);
            var allowedEmails = BuildNormalizedSet(_adminSettings.AllowedEmails);

            bool hasWhitelist = allowedUserIds.Count > 0 || allowedPhones.Count > 0 || allowedEmails.Count > 0;

            if (!hasWhitelist && _hostEnvironment.IsDevelopment() && _adminSettings.AllowInDevelopmentWithoutWhitelist)
                return true;

            if (allowedUserIds.Contains(Normalize(message.UserId)))
                return true;

            if (allowedPhones.Contains(NormalizePhone(message.UserPhone)) ||
                allowedPhones.Contains(NormalizePhone(message.From)) ||
                allowedPhones.Contains(NormalizePhone(message.UserId)))
            {
                return true;
            }

            if (allowedEmails.Contains(Normalize(message.UserEmail)))
                return true;

            return false;
        }

        private static HashSet<string> BuildNormalizedSet(IEnumerable<string>? values)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (values == null)
                return set;

            foreach (var value in values)
            {
                string normalized = Normalize(value);
                if (!string.IsNullOrEmpty(normalized))
                    set.Add(normalized);

                string phone = NormalizePhone(value);
                if (!string.IsNullOrEmpty(phone))
                    set.Add(phone);
            }

            return set;
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static string NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("351", StringComparison.Ordinal) && digits.Length > 9)
                digits = digits[3..];

            return digits;
        }
    }
}
