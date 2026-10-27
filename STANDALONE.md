# GitLab Search 2.0.2 — standalone downloads

These self-contained builds include the .NET runtime. No .NET SDK or runtime installation is required.

- Windows x64: `gitlab-search-v2.0.2-win-x64.exe`
- Linux x64 (Intel/AMD): `gitlab-search-v2.0.2-linux-x64.tar.gz`
- Linux ARM64: `gitlab-search-v2.0.2-linux-arm64.tar.gz`

## Windows (PowerShell)

```powershell
.\gitlab-search-v2.0.2-win-x64.exe --help
.\gitlab-search-v2.0.2-win-x64.exe setup YOUR_GITLAB_TOKEN --api-domain https://gitlab.example.com
.\gitlab-search-v2.0.2-win-x64.exe "hello" --groups "your-group"
```

## Linux

Choose the archive for your CPU (`uname -m`: x86_64 -> x64; aarch64 -> arm64).

```sh
tar -xzf gitlab-search-v2.0.2-linux-x64.tar.gz
./gitlab-search --version
./gitlab-search setup YOUR_GITLAB_TOKEN --api-domain https://gitlab.example.com
./gitlab-search "hello" --groups "your-group"
```

Optional installation:

```sh
mkdir -p "$HOME/.local/bin"
install -m 755 gitlab-search "$HOME/.local/bin/gitlab-search"
```

Ensure `$HOME/.local/bin` is on your PATH. If you already installed the .NET global tool, check `command -v gitlab-search` to see which executable is selected.

Linux builds target glibc-based distributions, not Alpine/musl. Standard OS dependencies (including ICU, OpenSSL and CA certificates) are still required. The bundled native runtime is extracted on first launch to the user's `.net` cache. The Windows executable is not code-signed.

## Validation

Linux x64 passed the CLI and HTTP smoke suite with `DOTNET_ROOT` set to a nonexistent path. Release workflows build each executable on its matching architecture and run version/help/setup checks before upload. The full HTTP suite is run on Linux x64.

## License

MIT; see the LICENSE bundled in Linux archives and the repository LICENSE for copyright and terms.
