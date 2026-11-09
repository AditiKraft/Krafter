# Dependency security maintenance

Package versions are centralized in `Directory.Packages.props`. `Directory.Build.props` enables NuGet auditing for all direct and transitive packages and treats every reported vulnerability severity (NU1901–NU1904) as an error. Both generated templates inherit this policy. Do not disable the audit or suppress its findings to make a restore pass.

The application stays on .NET 10. ASP.NET Core and EF Core packages use the 10.0.14 servicing release; `RuntimeFrameworkVersion` establishes the same runtime minimum. ASP.NET Core OpenAPI requires Microsoft.OpenApi 2.12.2 or later in the 2.x line. Aspire SDK/hosting packages stay on the compatible 13.2 servicing line.

Some parent libraries still request older transitive packages. Explicit references establish these patched floors without migrating the scheduler or build framework to a different major version:

| Consumer | Explicit security floor | Reason |
| --- | --- | --- |
| Both Aspire AppHosts | MessagePack 2.5.307 | StreamJsonRpc requests an older 2.5 release; this stays within its supported major version. |
| Backend and its consumers | Newtonsoft.Json 13.0.4 | TickerQ Dashboard's legacy ASP.NET dependency graph requests 11.0.2. |
| Build tool | System.Security.Cryptography.Xml 10.0.14 | Replaces Fallout's older XML dependency and requires the matching patched System.Security.Cryptography.Pkcs 10.0.14. |

Web projects receive patched XML/PKCS assemblies from the ASP.NET Core shared framework. The test project explicitly references that framework as well, enabling the SDK to prune the legacy dashboard's redundant XML package graph. Do not add a redundant XML package reference to web projects or suppress the SDK's package-pruning diagnostics.

Keep these references until the parent graph resolves safe versions without them. Review the resolved dependency graph before removing a floor; fixing a package in a private design-time graph alone does not protect downstream projects.

In a generated application, validate with current advisory data from its root:

```bash
dotnet restore AditiKraft.Krafter.slnx
dotnet list AditiKraft.Krafter.slnx package --vulnerable --include-transitive --no-restore
dotnet build AditiKraft.Krafter.slnx --no-restore
dotnet test tests/AditiKraft.Krafter.Tests/AditiKraft.Krafter.Tests.csproj --no-restore
```

Template contributors use `AditiKraft.Krafter.Dev.slnx` for both source host compositions, and also restore/audit the separate packaging project:

```bash
dotnet restore AditiKraft.Krafter.Templates.csproj
dotnet list AditiKraft.Krafter.Templates.csproj package --vulnerable --include-transitive --no-restore
```

The build tool's nested `build/Directory.Build.props` explicitly imports the root policy. Keep that import; otherwise the normal nearest-file lookup would bypass the audit and runtime minimum for this project.

Pack the templates, generate clean split/single apps, and repeat restore, audit, build and browser checks in both outputs. A clean audit means no known vulnerabilities were reported by the configured advisory source at that time; it does not replace security review or production configuration checks.
