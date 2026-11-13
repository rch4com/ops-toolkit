using System.Text.Json;
using System.Text.Json.Serialization;

namespace LdapAuthTester.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LdapServerSettings
{
    public string Domain { get; set; } = string.Empty;
    public int LdapPort { get; set; } = 389;
    public int LdapsPort { get; set; } = 636;
    public AuthTestMethod TestMethod { get; set; }
    public AuthTestTarget TestTarget { get; set; }
    public int TimeoutSeconds { get; set; } = 10;

    public static LdapServerSettings FromRequest(AdAuthTestRequest request) => new()
    {
        Domain = request.Domain,
        LdapPort = request.LdapPort,
        LdapsPort = request.LdapsPort,
        TestMethod = request.TestMethod,
        TestTarget = request.TestTarget,
        TimeoutSeconds = request.TimeoutSeconds
    };

    public static LdapServerSettings Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<LdapServerSettings>(json) ?? throw new JsonException("Missing server settings.");
        settings.Validate();
        return settings;
    }

    public void Validate()
    {
        ValidateServer(Domain);
        if (LdapPort is < 1 or > 65535 || LdapsPort is < 1 or > 65535 || TimeoutSeconds is < 1 or > 300 ||
            !Enum.IsDefined(TestMethod) || !Enum.IsDefined(TestTarget))
        {
            throw new ArgumentException("Invalid LDAP server settings.");
        }
    }

    public static void ValidateServer(string server)
    {
        if (string.IsNullOrWhiteSpace(server) || Uri.CheckHostName(server) == UriHostNameType.Unknown)
        {
            throw new ArgumentException("A server hostname or IP address is required.", nameof(server));
        }
    }
}
