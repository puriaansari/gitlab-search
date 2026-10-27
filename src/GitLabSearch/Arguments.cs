namespace GitLabSearch;

public sealed class Arguments
{
    public bool Setup, Help, Version, IgnoreSsl, Json;
    public string? Value, Groups, Filename, Extension, Path;
    public string Archive = "all", Domain = "gitlab.com", Directory = ".";
    public int Concurrency = 25, RequestDelayMs = 2500;
    public const string Usage = """
        gitlab-search [options] <search-term>
        gitlab-search setup [options] <personal-access-token>

        Search options:
          -g, --groups <names>       Comma-separated group IDs or full paths
          -f, --filename <glob>      Filter filename
          -e, --extension <ext>      Filter extension
          -p, --path <path>          Filter path
          -a, --archive <mode>       all (default), only, exclude
          --json                    Print structured JSON
        Setup options:
          --api-domain <url>        GitLab domain or root URL (default gitlab.com)
          --dir <directory>         Configuration directory (default .)
          --concurrency <number>    Maximum concurrent requests (default 25)
          --request-delay-ms <ms>   Minimum delay between requests (default 2500)
          --ignore-ssl              Disable TLS certificate validation
        General:
          -h, --help                Show help
          -V, --version             Show version
          --                        Treat remaining argument as a literal search term
        Configuration: JSON .gitlabsearchrc in current/parent directories or home.
        GITLABSEARCH_TOKEN overrides the configured token.
        """;
    public static Arguments Parse(string[] args)
    {
        var o = new Arguments();
        var i = 0;
        if (args.FirstOrDefault() == "setup") { o.Setup = true; i++; }
        for (; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing option value.");
            var a = args[i];
            switch (a)
            {
                case "-h": case "--help": o.Help = true; break;
                case "-V": case "--version": o.Version = true; break;
                case "--ignore-ssl" when o.Setup: o.IgnoreSsl = true; break;
                case "--api-domain" when o.Setup: o.Domain = Next(); break;
                case "--dir" when o.Setup: o.Directory = Next(); break;
                case "--concurrency" when o.Setup:
                    if (!int.TryParse(Next(), out o.Concurrency) || o.Concurrency < 1) throw new ArgumentException("Concurrency must be a positive integer.");
                    break;
                case "--request-delay-ms" when o.Setup:
                    if (!int.TryParse(Next(), out o.RequestDelayMs) || o.RequestDelayMs < 0) throw new ArgumentException("Request delay must be a non-negative integer.");
                    break;
                case "--json" when !o.Setup: o.Json = true; break;
                case "-g" or "--groups" when !o.Setup: o.Groups = Next(); break;
                case "-f" or "--filename" when !o.Setup: o.Filename = Next(); break;
                case "-e" or "--extension" when !o.Setup: o.Extension = Next(); break;
                case "-p" or "--path" when !o.Setup: o.Path = Next(); break;
                case "-a" or "--archive" when !o.Setup: o.Archive = Next(); break;
                case "--":
                    if (o.Value != null || i + 2 != args.Length) throw new ArgumentException("Supply exactly one positional argument.");
                    o.Value = args[++i]; break;
                default:
                    if (a.StartsWith('-') || o.Value != null) throw new ArgumentException("Unknown option or extra argument. Use --help.");
                    o.Value = a; break;
            }
        }
        if (!o.Help && !o.Version)
        {
            if (string.IsNullOrWhiteSpace(o.Value)) throw new ArgumentException(o.Setup ? "An access token is required." : "A search term is required.");
            if (o.Archive is not ("all" or "only" or "exclude")) throw new ArgumentException("Archive must be all, only or exclude.");
            if (o.Groups != null && o.Groups.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length == 0) throw new ArgumentException("Groups cannot be empty.");
        }
        return o;
    }
}
