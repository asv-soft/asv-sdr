# Development

## Build and Test

Run these commands from the repository root:

```bash
dotnet restore src/Asv.Sdr.slnx
dotnet build src/Asv.Sdr.slnx -c Release --no-restore -p:GeneratePackageOnBuild=false
dotnet test --solution src/Asv.Sdr.slnx -c Release --no-build --no-restore
```

The root `global.json` selects Microsoft.Testing.Platform for .NET 10 and xUnit v3
4.x. Use `--solution` or `--project` when selecting tests explicitly. For example:

```bash
dotnet test --project src/Asv.Sdr.Test/Asv.Sdr.Test.csproj -c Release
```

The core test project uses `coverlet.MTP` for optional coverage collection:

```bash
dotnet test --project src/Asv.Sdr.Test/Asv.Sdr.Test.csproj -c Release --coverlet
```

The `Code Quality` workflow (`.github/workflows/code-quality.yml`) builds the solution
and runs both test projects on pushes to `main`, pull requests, and manual runs.

## Documentation

Documentation sources live in `src/Writerside/topics`. Register new topics in
`src/Writerside/asv-sdr.tree`. Keep documentation in English.

The `Publish documentation` workflow (`.github/workflows/docs.yml`) builds the
`Writerside/asv-sdr` instance and checks the Writerside report before deployment.
Pull requests validate documentation; pushes to `main` publish it to
[asv.me/asv-sdr](https://asv.me/asv-sdr/). Manual runs deploy only when `main` is selected.
GitHub Pages must use GitHub Actions as its publishing source.

During each documentation build, the workflow reads the single non-empty
`ProductVersion` from `src/Directory.Build.props` and sets the Writerside instance
version in the build checkout. Editing that properties file also triggers the
workflow. No generated version update needs to be committed.

To build locally with Docker, first run the version-setting Python snippet from
`.github/workflows/docs.yml`, then run:

```bash
mkdir -p artifacts
docker run --rm \
  -v "$PWD:/opt/sources" \
  -e SOURCE_DIR=/opt/sources/src \
  -e MODULE_INSTANCE=Writerside/asv-sdr \
  -e OUTPUT_DIR=/opt/sources/artifacts \
  -e RUNNER=other \
  jetbrains/writerside-builder:2026.08.0328
```

Inspect `artifacts/report.json` and `artifacts/webHelpASV-SDR2-all.zip`.
Local version injection modifies `writerside.cfg`; keep that generated change out
of source control. The CI workflow applies the same injection in its disposable checkout.
