using System.Text.Json;
using System.Text.Json.Serialization;
namespace GitLabSearch;

public sealed record Configuration(string Token, string Domain = "gitlab.com", bool IgnoreSSL = false, int Concurrency = 25)
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
    [JsonIgnore] public Uri ApiUri
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Domain)) throw new ArgumentException("A GitLab domain is required.");
            var root = Domain.Contains("://") ? Domain : "https://" + Domain;
            if (!Uri.TryCreate(root.TrimEnd('/') + "/api/v4/", UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
                throw new ArgumentException("Invalid GitLab domain or root URL.");
            return uri;
        }
    }
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Token) || Token.Contains('\r') || Token.Contains('\n')) throw new ArgumentException("A valid access token is required.");
        if (Concurrency < 1) throw new ArgumentException("Concurrency must be positive.");
        _ = ApiUri;
    }
    public static Configuration Load()
    {
        Configuration? config = null;
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
        {
            var path = System.IO.Path.Combine(dir.FullName, ".gitlabsearchrc");
            if (File.Exists(path)) { config = Read(path); break; }
        }
        var home = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitlabsearchrc");
        config ??= File.Exists(home) ? Read(home) : new Configuration("");
        var token = Environment.GetEnvironmentVariable("GITLABSEARCH_TOKEN");
        if (token != null) config = config with { Token = token };
        config.Validate();
        return config;
    }
    private static Configuration Read(string path) => JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path), JsonOptions) ?? throw new ArgumentException("Empty configuration. Run setup.");
}
