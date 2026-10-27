using GitLabSearch;
using System.Text.Json;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    var options = Arguments.Parse(args);
    if (options.Help || args.Length == 0) { Console.WriteLine(Arguments.Usage); return 0; }
    if (options.Version) { Console.WriteLine("2.0.1"); return 0; }
    if (options.Setup)
    {
        var config = new Configuration(options.Value!, options.Domain, options.IgnoreSsl, options.Concurrency);
        config.Validate();
        var path = Path.Combine(Path.GetFullPath(options.Directory), ".gitlabsearchrc");
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Create with restrictive permissions before writing the token.
        var fileOptions = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var stream = new FileStream(path, fileOptions))
            JsonSerializer.Serialize(stream, config, Configuration.JsonOptions);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Console.WriteLine($"Successfully wrote config to {path}");
        return 0;
    }
    var settings = Configuration.Load();
    using var handler = new HttpClientHandler { AllowAutoRedirect = false };
    if (settings.IgnoreSSL) handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    using var http = new HttpClient(handler) { BaseAddress = settings.ApiUri, Timeout = TimeSpan.FromSeconds(60) };
    http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", settings.Token);
    var client = new GitLabClient(http, settings.Concurrency);
    var results = await client.SearchAsync(options, cancellation.Token);
    if (options.Json) Console.WriteLine(JsonSerializer.Serialize(results, Configuration.JsonOptions));
    else foreach (var project in results)
    {
        Console.WriteLine($"{project.Project.Name}{(project.Project.Archived ? " (archived)" : "")}:");
        foreach (var hit in project.Matches)
        {
            Console.WriteLine($"\n\t{GitLabClient.BlobUrl(project.Project, hit)}\n");
            Console.WriteLine("\t\t" + hit.Data.Replace("\n", "\n\t\t"));
        }
    }
    return 0;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { Console.Error.WriteLine("Cancelled."); return 130; }
catch (Exception ex) when (ex is ArgumentException or IOException or JsonException or HttpRequestException or OperationCanceledException or UnauthorizedAccessException)
{
    // Never echo configuration contents, tokens, or upstream response bodies.
    Console.Error.WriteLine(ex is JsonException ? "Invalid JSON in configuration or GitLab response." : ex is HttpRequestException ? $"GitLab request failed{(ex is GitLabRequestException ge ? $": HTTP {(int)ge.StatusCode!}" : ". Check the server address and connection.")}" : ex.Message);
    return 1;
}
