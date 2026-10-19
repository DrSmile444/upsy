// EcoFlow account login, used once to learn the user ID the stations authenticate against.
// Request shape from rabits/ha-ef-ble (Apache-2.0) eflib/login.py, see THIRD-PARTY-NOTICES.md.
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Upsy.Core.Protocol.EcoFlow;

/// <param name="UserId">The account's user ID; null when sign-in failed.</param>
/// <param name="Error">What to tell the user when sign-in failed.</param>
public sealed record EcoFlowSignIn(string? UserId, string? Error)
{
    public bool Succeeded => UserId is not null;
}

/// <summary>Signs in to EcoFlow's app API once to learn the account's user ID; the password is only used for this request.</summary>
public sealed class EcoFlowAccount(HttpClient http)
{
    /// <summary>The global API; the EU host is tried only when it cannot be reached (the user ID is the same on every region).</summary>
    public static readonly Uri[] Hosts = [new("https://api.ecoflow.com/auth/login"), new("https://api-e.ecoflow.com/auth/login")];

    public async Task<EcoFlowSignIn> SignInAsync(string email, string password, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrEmpty(password);
        var body = new Dictionary<string, object>
        {
            ["scene"] = "IOT_APP",
            ["appVersion"] = "1.0.0",
            ["password"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(password)),
            ["oauth"] = new Dictionary<string, string> { ["bundleId"] = "com.ef.EcoFlow" },
            ["userType"] = "ECOFLOW",
            ["email"] = email.Trim(),
        };

        string? unreachable = null;
        foreach (var host in Hosts)
        {
            try
            {
                using var response = await http.PostAsJsonAsync(host, body, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    unreachable = $"EcoFlow answered {(int)response.StatusCode}. Try again later.";
                    continue;
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
                if (json.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String && code.GetString() == "0"
                    && json.TryGetProperty("data", out var data) && data.TryGetProperty("user", out var user)
                    && user.TryGetProperty("userId", out var id) && id.GetString() is { Length: > 0 } userId)
                {
                    return new EcoFlowSignIn(userId, null);
                }

                // A clear answer from EcoFlow (wrong password, unknown account): another region would say the same.
                var message = json.TryGetProperty("message", out var m) && m.GetString() is { Length: > 0 } text ? text : "EcoFlow did not accept the sign-in.";
                return new EcoFlowSignIn(null, message);
            }
            catch (HttpRequestException)
            {
                unreachable = "EcoFlow could not be reached. Signing in needs internet once.";
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                unreachable = "EcoFlow did not answer in time. Signing in needs internet once.";
            }
            catch (JsonException)
            {
                unreachable = "EcoFlow sent an answer upsy does not understand. Try again later.";
            }
        }

        return new EcoFlowSignIn(null, unreachable);
    }
}
