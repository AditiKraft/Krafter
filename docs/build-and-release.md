# Build and release commands

## Application validation

Run from the repository root:

```bash
dotnet run --project build/_build.csproj -- --configuration Release
```

The default target is `Validate`: restore the selected solution, build it, then run the regression test project. The template repository selects `AditiKraft.Krafter.Dev.slnx` to build both source host compositions. A generated application selects its single `AditiKraft.Krafter.slnx`. `--target Validate` selects the same target explicitly. These commands do not publish images, push packages, invoke deployment webhooks, start Aspire, or run database migrations.

The application GitHub Actions workflow, `.github/workflows/validation.yml`, runs this target on pushes and pull requests with `contents: read`. It needs no NuGet publishing configuration. PostgreSQL integration tests require the documented dedicated test databases (`KRAFTER_TEST_POSTGRES` and `KRAFTER_TEST_OAUTH_POSTGRES`); those cases report skipped when the variables are absent.

The shared server HTTP policy retries safe reads on transient failures. POST, PUT, PATCH, DELETE and CONNECT do not retry automatically, including credential exchanges and mutations whose responses may be lost after commit. HTTP 429 remains a per-client throttling response and does not open the shared dependency circuit. HTTP 408/5xx, transport errors and timeouts still contribute to that circuit. Google initiation and provider exchange retain their explicit no-resilience handlers.

## Template-repository releases

These targets live in `build/UpstreamRelease.cs`, which is excluded from generated applications along with its branch-selector helper/tests, `.github/workflows/main.yml` and the packaging project. Supply a new, normalized NuGet version explicitly; there is no default release version.

| Target | Work performed |
| --- | --- |
| `PackTemplate` | Validate both source compositions and the tests, then create the exact versioned package under `nupkg/`. |
| `ValidateTemplates` | Run `PackTemplate`, install that package into an isolated hive, generate both applications in a temporary directory, and run each generated application's default build/tests. |
| `PublishTemplate` | Run `ValidateTemplates`, then push that exact versioned artifact to NuGet.org. Requires `NuGetPAT`. |
| `BuildAndPublishDockerImage` | Run `Validate`, log into GHCR, and publish upstream Backend/UI images for the `main` or `dev` branch. Requires `PAT`; accepts `ContainerUser` and `ContainerNamespace`. |
| `PublishImageAndMakeCallToWebhook` | Publish the validated images, then invoke `DeploymentWebhookUrl`; a failed HTTP status fails the target. |

For example, validate a candidate template release locally:

```bash
dotnet run --project build/_build.csproj -- --target ValidateTemplates --configuration Release --template-version 1.2.3-preview.1
```

This packs and validates locally. Template publication is a separate explicit command:

```bash
# Supply NuGetPAT through the environment or CI secret store.
dotnet run --project build/_build.csproj -- --target PublishTemplate --configuration Release --template-version 1.2.3
```

The `Publish template release` workflow is manual (`workflow_dispatch`) and requires a version input. It retains the existing `main.yml` filename and NuGet trusted-publisher identity; `NUGET_USER` remains its GitHub Actions secret. The publication target validates source and generated applications before pushing, and selects `AditiKraft.Krafter.Templates.<version>.nupkg` directly even if older packages exist in the output directory. Existing versions are immutable at NuGet.org; choose a new version for each release.

Image publication and webhook deployment are separate explicit targets; template publication does not invoke them. Before publishing application images, set `ContainerNamespace` to the intended owner, keep the registry hostname only in `ContainerRegistry`, and use a repository such as `your-org/krafter.api`. Generated single-host applications publish only UI.Web.

The upstream image targets accept only the complete `main` or `dev` branch name (or the corresponding `refs/heads/` form). Feature branches such as `feature/main`, tags and detached HEAD cannot select the release image tags.

## Packaging regression

```bash
python3 tests/operations/verify-template-private-files.py
```

The fixture copies only packaging/build metadata, workflow definitions and the public icon, then creates harmless markers. It tests two successive packs into its own `nupkg/` folder, mixed-case package artifacts in every overlay source, private-file exclusions, retained public examples, and generated application automation from both package and source installations. It does not copy application configuration or private source data and never executes publishing targets.
