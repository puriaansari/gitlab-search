using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GitLabSearch;
public sealed record Group(long Id, string Name);
public sealed record Project(long Id, string Name, [property: JsonPropertyName("web_url")] string WebUrl, bool Archived);
public sealed record Match(string Data, string Filename, string Ref, int Startline);
public sealed record ProjectMatches(Project Project, List<Match> Matches);
public sealed class GitLabRequestException(HttpStatusCode status) : HttpRequestException("GitLab API error.", null, status);

public sealed class GitLabClient(HttpClient http, int concurrency, int requestDelayMs = 2500)
{
    private readonly GitLabRequestScheduler scheduler = new(http, requestDelayMs);

    public async Task<List<ProjectMatches>> SearchAsync(Arguments options, CancellationToken ct)
    {
        var groups = options.Groups?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? (await Pages<Group>("groups?per_page=100", ct)).Select(g => g.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var archive = options.Archive switch { "only" => "&archived=true", "exclude" => "&archived=false", _ => "" };
        var projects = new ConcurrentDictionary<long, Project>();
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = ct };
        await Parallel.ForEachAsync(groups.Distinct(), parallel, async (group, token) =>
        {
            foreach (var project in await Pages<Project>($"groups/{Uri.EscapeDataString(group)}/projects?per_page=100&include_subgroups=true{archive}", token))
                projects.TryAdd(project.Id, project);
        });
        var query = options.Value!;
        if (options.Filename != null) query += " filename:" + options.Filename;
        if (options.Extension != null) query += " extension:" + options.Extension;
        if (options.Path != null) query += " path:" + options.Path;
        var results = new ConcurrentBag<ProjectMatches>();
        await Parallel.ForEachAsync(projects.Values, parallel, async (project, token) =>
        {
            var matches = await Pages<Match>($"projects/{project.Id}/search?scope=blobs&per_page=100&search={Uri.EscapeDataString(query)}", token);
            if (matches.Count > 0) results.Add(new(project, matches));
        });
        return results.OrderBy(r => r.Project.Name, StringComparer.Ordinal).ThenBy(r => r.Project.Id).ToList();
    }
    private async Task<List<T>> Pages<T>(string path, CancellationToken ct)
    {
        var values = new List<T>();
        Uri? next = new(http.BaseAddress!, path);
        var visited = new HashSet<string>();
        while (next != null)
        {
            // A pagination header must never forward the private token to another host or path.
            if (!http.BaseAddress!.IsBaseOf(next)) throw new ArgumentException("GitLab returned an unsafe pagination URL.");
            if (!visited.Add(next.AbsoluteUri)) throw new ArgumentException("GitLab returned a pagination loop.");
            using var response = await scheduler.GetAsync(next, ct);
            if (!response.IsSuccessStatusCode) throw new GitLabRequestException(response.StatusCode);
            values.AddRange(await response.Content.ReadFromJsonAsync<List<T>>(Configuration.JsonOptions, ct) ?? throw new ArgumentException("GitLab returned an empty response."));
            Uri? following = null;
            if (response.Headers.TryGetValues("Link", out var links))
            {
                foreach (var entry in links.SelectMany(l => l.Split(',')))
                {
                    var link = Regex.Match(entry, "<([^>]+)>;\\s*rel=\"next\"");
                    if (link.Success) following = new Uri(next, link.Groups[1].Value);
                }
            }
            else if (response.Headers.TryGetValues("X-Next-Page", out var pages) && !string.IsNullOrWhiteSpace(pages.FirstOrDefault()))
            {
                if (!int.TryParse(pages.First(), out var page) || page < 1) throw new ArgumentException("Invalid pagination header.");
                var builder = new UriBuilder(next);
                var query = Regex.Replace(builder.Query.TrimStart('?'), @"(^|&)page=\d+(&|$)", "$1").Trim('&');
                builder.Query = query + "&page=" + page;
                following = builder.Uri;
            }
            next = following;
        }
        return values;
    }
    public static string BlobUrl(Project project, Match hit) =>
        $"{project.WebUrl.TrimEnd('/')}/-/blob/{Uri.EscapeDataString(hit.Ref)}/{string.Join('/', hit.Filename.Split('/').Select(Uri.EscapeDataString))}#L{hit.Startline}";
}
