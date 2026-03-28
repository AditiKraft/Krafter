# Krafter

<p align="center">
  <img src="docs/krafter-logo.svg" alt="Krafter logo" width="560" />
</p>

> A modern .NET 10 full-stack platform built with Vertical Slice Architecture (VSA), multi-tenancy, and Blazor WebAssembly — designed for efficient AI-assisted feature development.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-WebAssembly-512BD4?logo=blazor)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## 🚀 Demo

Try the live demo at [https://krafter.getkrafter.dev/](https://krafter.getkrafter.dev/)

**Default Credentials:**
- Email: `admin@getkrafter.dev`
- Password: `123Pa$$word!`

Alternatively, log in with Google to create a new account.

## ⚡ TL;DR Quick Start

**First local run:**

1) Run Aspire orchestration
- `dotnet run --project aspire/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj`

2) Wait for startup to finish
- `krafter-migrator` applies checked-in migrations before `krafter-api` starts

3) Open URLs
- Aspire Dashboard: https://localhost:17285
- Backend API: https://localhost:5199
- Scalar API Reference: https://localhost:5199/scalar/v1
- Blazor UI: https://localhost:7291

## 📋 Table of Contents

- [TL;DR Quick Start](#tldr-quick-start)
- [Overview](#overview)
- [Architecture](#architecture)
- [Key Features](#key-features)
- [Technology Stack](#technology-stack)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Quick Start](#quick-start)
  - [Database Migrations Workflow](#database-migrations-workflow)
  - [Configuration Notes](#configuration-notes)
  - [Troubleshooting](#troubleshooting)
- [Project Structure](#project-structure)
- [Development Guide](#development-guide)
- [Deployment](#deployment)
- [Contributing](#contributing)
- [License](#license)

## 🎯 Overview

**Krafter** is a production-ready, enterprise-grade full-stack platform built with .NET 10, combining modern architectural patterns with cutting-edge technologies. It provides a solid foundation for building scalable, multi-tenant SaaS applications with rich user interfaces.

### What Makes Krafter Special?

- **🏗️ Vertical Slice Architecture (VSA)** - Backend organized by features, not layers
- **🌐 Hybrid Blazor** - WebAssembly + Server rendering for optimal performance
- **🏢 Multi-Tenancy** - Complete tenant isolation at the database level
- **🔐 Permission-Based Security** - Fine-grained authorization with JWT
- **⚡ Real-Time Updates** - SignalR integration for live notifications
- **📊 Observability** - OpenTelemetry with Aspire orchestration
- **🎨 Modern UI** - Radzen components with theming support
- **🔌 Refit API Client** - Type-safe HTTP client with automatic token handling

## 🏛️ Architecture

### Solution Architecture Diagram

![Krafter Solution Architecture](docs/architecture.svg)

### Project Dependencies Diagram

![Project Dependencies](docs/dependencies.svg)

## ✨ Key Features

### 🏗️ **Architecture**
- **Vertical Slice Architecture (VSA)** - Features organized by business capability
- **Clean Code** - Single Responsibility, DRY, SOLID principles
- **Auto-Registration** - Handlers, services, and routes discovered via markers
- **Response Pattern** - Consistent `Response<T>` wrapper for all operations

### 🔐 **Security**
- **JWT Authentication** - Secure token-based authentication
- **Google OAuth** - External authentication integration
- **Permission-Based Authorization** - Fine-grained access control
- **Multi-Tenancy** - Complete tenant isolation at DB level
- **Token Refresh** - Automatic token rotation

### 🎨 **User Interface**
- **Blazor Hybrid** - WebAssembly + Server rendering
- **Radzen Components** - 70+ professional UI components
- **Theme Support** - Light/Dark/Auto modes with WCAG compliance
- **Responsive Design** - Mobile and desktop optimized
- **Code-Behind Pattern** - Clean separation of markup and logic
- **Refit** - Type-safe REST API client

### 📊 **Data & Storage**
- **EF Core** - PostgreSQL & MySQL support
- **Multi-Database** - Separate contexts for tenants, jobs, and main data
- **Migrations** - Code-first database schema management
- **Soft Delete** - Recoverable data deletion

### ⚡ **Performance & Scalability**
- **Background Jobs** - TickerQ for async processing
- **SignalR** - Real-time bi-directional communication
- **Redis Cache** - Distributed caching support
- **Pagination** - Efficient data loading
- **Debouncing** - Optimized search and filtering

### 🔍 **Observability**
- **.NET Aspire** - Orchestration and service discovery
- **OpenTelemetry** - Distributed tracing and metrics
- **Structured Logging** - Comprehensive application logs
- **Health Checks** - Service health monitoring

### 🚀 **DevOps**
- **NUKE Build** - Automated build pipeline
- **Docker Support** - Containerized deployment
- **GitHub Actions** - CI/CD automation
- **Auto Deployment** - Webhook-triggered updates

## 🛠️ Technology Stack

### Backend
- **.NET 10** - Latest .NET framework
- **ASP.NET Core** - Minimal APIs
- **Entity Framework Core 10** - ORM
- **ASP.NET Core Identity** - User management
- **FluentValidation** - Input validation
- **TickerQ** - Background job processing
- **SignalR** - Real-time communication

### Frontend
- **Blazor WebAssembly** - Client-side SPA
- **Blazor Server** - Server-side rendering
- **Radzen Blazor** - UI component library
- **Refit** - Type-safe REST API client
- **Blazored LocalStorage** - Browser storage
- **FluentValidation.Blazor** - Client-side validation
- **Mapster** - Object mapping

### Infrastructure
- **.NET Aspire** - Cloud-native orchestration
- **OpenTelemetry** - Observability
- **Redis** - Caching (optional)
- **PostgreSQL / MySQL** - Database
- **Docker** - Containerization
- **NUKE** - Build automation

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Aspire/PostgreSQL)
- [Visual Studio 2022 17.11+](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)

### Quick Start

1. Clone the repository
   ```bash
   git clone https://github.com/AditiKraft/Krafter.git
   cd Krafter
   ```

2. Restore packages
   ```bash
   dotnet restore
   ```

3. Run AppHost
   ```bash
   dotnet run --project aspire/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj
   ```

4. Wait for `krafter-migrator` to finish
   - AppHost starts the short-lived migrator before `krafter-api`
   - No manual `dotnet ef database update` step is required for normal startup

5. Access the application
   - Aspire Dashboard: https://localhost:17285
   - Backend API: https://localhost:5199
   - Scalar API Reference: https://localhost:5199/scalar/v1
   - Blazor UI: https://localhost:7291

6. Default Credentials

   On first run, the application seeds a default admin account:
   - Email: `admin@getkrafter.dev`
   - Password: `123Pa$$word!`

   Alternatively, log in with Google, which will create a new account.

   > ⚠️ Important: Change the default password immediately in production!

---

### Database Migrations Workflow

AppHost applies checked-in migrations automatically through `krafter-migrator`. For normal local startup, run AppHost and let the migrator finish.

#### Create a New Migration

The existing `ConnectionStrings:KrafterDbMigration` value in `src/AditiKraft.Krafter.Backend/appsettings.Local.json` already works for `dotnet ef migrations add`. Do not change it.

Install EF Core tools once if you have not already:

```bash
dotnet tool install --global dotnet-ef
```

Create the migration from the Backend project:

```bash
cd src/AditiKraft.Krafter.Backend

# Examples
dotnet ef migrations add <MigrationName> --context KrafterContext
dotnet ef migrations add <MigrationName> --context TenantDbContext
dotnet ef migrations add <MigrationName> --context BackgroundJobsContext
```

Restart AppHost to apply the new migration:

```bash
dotnet run --project aspire/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj
```

Your database is now ready.

---

### Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| "Unable to create DbContext" | Missing `ConnectionStrings:KrafterDbMigration` or wrong working directory | Restore the `KrafterDbMigration` entry in `appsettings.Local.json` and run the command from `src/AditiKraft.Krafter.Backend` |
| "`krafter-api` does not start" | `krafter-migrator` failed first | Check the `krafter-migrator` logs and fix the migration error |
| "Migration already exists" | Duplicate migration name | Use `dotnet ef migrations remove --context <ContextName>` |
| "Pending model changes" | The model changed but no migration exists yet | Add a new migration for the affected context before restarting AppHost |
| `dotnet-ef` not found | EF tools not installed | Run `dotnet tool install --global dotnet-ef` |

---

### Configuration Notes

Local development works with the checked-in dev settings.

Change configuration only if you need custom values for:

- PostgreSQL container credentials in `aspire/AditiKraft.Krafter.Aspire.AppHost/appsettings.json`
- JWT, TickerQ, or Google auth settings in `src/AditiKraft.Krafter.Backend/appsettings.json`
- UI backend URL or Google client settings in `src/UI/AditiKraft.Krafter.UI.Web/appsettings.Development.json` and `src/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/appsettings.json`

For anything outside local development, prefer user-secrets or environment variables instead of committed values.

---

## 📁 Project Structure

```
AditiKraft.Krafter/
├── Agents.md                              # AI agent instructions (entry point)
├── aspire/                                # Aspire orchestration
│   ├── AditiKraft.Krafter.Aspire.AppHost/
│   └── AditiKraft.Krafter.Aspire.ServiceDefaults/
├── src/
│   ├── AditiKraft.Krafter.Contracts/      # Shared contracts library
│   │   ├── Agents.md
│   │   ├── Contracts/                     # Auth, Users, Roles, Tenants DTOs
│   │   ├── Common/                        # Routes, permissions, shared models
│   │   └── Realtime/                      # SignalR method contracts
│   ├── AditiKraft.Krafter.Backend/        # ASP.NET Core API (VSA)
│   │   ├── Agents.md
│   │   ├── Web/                           # HTTP pipeline, middleware, auth config
│   │   ├── Features/                      # Vertical slices
│   │   ├── Infrastructure/                # Jobs, notifications, persistence, realtime
│   │   ├── Common/                        # Context, entities, interfaces, extensions
│   │   ├── Errors/                        # Exception types
│   │   ├── Migrations/                    # EF Core migrations
│   │   └── Program.cs
│   ├── AditiKraft.Krafter.Backend.Migrator/ # Short-lived EF migration runner
│   └── UI/
│       ├── Agents.md
│       ├── AditiKraft.Krafter.UI.Web.Client/  # Blazor WebAssembly
│       │   ├── Features/
│       │   ├── Infrastructure/                # AuthApi, Refit, SignalR, Storage, Http
│       │   └── Common/                        # Shared components, models
│       └── AditiKraft.Krafter.UI.Web/         # Blazor Server host
├── build/                                  # NUKE build automation
├── docs/                                   # Documentation assets
├── .github/                                # GitHub Actions workflows
└── README.md                               # This file
```

For detailed structure, see [Agents.md](Agents.md) and sub-project Agents.md files.

## 📖 Development Guide

### Adding a New Feature

**Backend (VSA Pattern):**
1. Create feature folder: `Features/<Feature>/`
2. Add operation files (e.g., `Create<Feature>.cs`, `Get<Feature>s.cs`)
3. Add entity/service under `Features/<Feature>/Common/` if feature-specific
4. Update `KrafterContext.cs` with new `DbSet`
5. Update mappings/query behavior in `Infrastructure/Persistence/ModelBuilderExtensions.cs` as needed
6. Run migration for the context you changed, for example: `dotnet ef migrations add Add<Feature> --context KrafterContext`
7. Restart AppHost so `krafter-migrator` applies the new migration
8. Add permissions and routes in `src/AditiKraft.Krafter.Contracts/Common/`

**UI (Blazor):**
1. Create feature folder: `Features/<Feature>/`
2. Add list page: `<Feature>s.razor` + `<Feature>s.razor.cs`
3. Add form dialog: `CreateOrUpdate<Feature>.razor` + `.razor.cs`
4. Use route constants from `src/AditiKraft.Krafter.Contracts/Common/KrafterRoute.cs`
5. Create Refit interface: `Infrastructure/Refit/I<Feature>sApi.cs`
6. Register Refit client in `Infrastructure/Refit/RefitServiceExtensions.cs`
7. Use permissions from `src/AditiKraft.Krafter.Contracts/Common/Auth/Permissions/`
8. Update `Infrastructure/Services/MenuService.cs` for navigation

For complete guidelines, see [Agents.md](Agents.md) and the sub-project Agents.md files in `src/AditiKraft.Krafter.Backend/`, `src/UI/`, and `src/AditiKraft.Krafter.Contracts/`.

### Key Commands

```bash
# Build solution
dotnet build AditiKraft.Krafter.slnx

# Run tests
dotnet test

# Create migration
dotnet ef migrations add <Name> --project src/AditiKraft.Krafter.Backend --context KrafterContext
dotnet ef migrations add <Name> --project src/AditiKraft.Krafter.Backend --context BackgroundJobsContext
dotnet ef migrations add <Name> --project src/AditiKraft.Krafter.Backend --context TenantDbContext

# Apply migrations through the dedicated migrator
dotnet run --project aspire/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj
```

## 🐳 Deployment

### Docker Deployment

**Build images:**
```bash
dotnet publish src/AditiKraft.Krafter.Backend/AditiKraft.Krafter.Backend.csproj -c Release -p:PublishProfile=DefaultContainer
dotnet publish src/UI/AditiKraft.Krafter.UI.Web/AditiKraft.Krafter.UI.Web.csproj -c Release -p:PublishProfile=DefaultContainer
```

### CI/CD with GitHub Actions

The project includes automated CI/CD pipelines that:
- Build and test on every push
- Create Docker images for `main` and `dev` branches
- Push images to GitHub Container Registry
- Trigger deployment webhooks

See [.github/workflows](.github/workflows) for configuration.

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork** the repository
2. Create a **feature branch** (`git checkout -b feature/amazing-feature`)
3. Follow the **coding conventions** in [Agents.md](Agents.md) and the relevant sub-project `Agents.md` files
4. **Commit** your changes (`git commit -m 'feat: add amazing feature'`)
5. **Push** to the branch (`git push origin feature/amazing-feature`)
6. Open a **Pull Request**

### Commit Convention

Use [Conventional Commits](https://www.conventionalcommits.org/):
```
feat(scope): add new feature
fix(scope): fix bug
docs(scope): update documentation
refactor(scope): refactor code
test(scope): add tests
```

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [.NET Team](https://github.com/dotnet) - For the amazing .NET platform
- [Radzen](https://www.radzen.com/) - For the excellent Blazor components
- [Refit](https://github.com/reactiveui/refit) - For the type-safe REST client
- [NUKE Build](https://nuke.build/) - For the build automation framework

## 📞 Support

- **Documentation**: [Agents.md](Agents.md) (AI instructions and project structure)
- **Issues**: [GitHub Issues](https://github.com/AditiKraft/Krafter/issues)
- **Discussions**: [GitHub Discussions](https://github.com/AditiKraft/Krafter/discussions)

---

## 📦 .NET Template

Krafter ships as a template package with **two hosting variants** you choose at project creation time.

### Install the Template from NuGet

```bash
dotnet new install AditiKraft.Krafter.Templates
```

### Template Variants

| Template | Short Name | Description |
|----------|-----------|-------------|
| **Split Host** | `krafter` | Separate Backend API and Blazor UI hosts (two processes). Best for independent scaling, team separation, or microservice evolution. |
| **Single Host** | `krafter-single` | Combined API + Blazor UI in one process. Simpler deployment, lower latency, single Aspire resource. |

### Create a New Project

**Split Host** (separate API + UI):
```bash
dotnet new krafter -n MyCompanyApp
cd MyCompanyApp
dotnet run --project aspire/MyCompanyApp.Aspire.AppHost/MyCompanyApp.Aspire.AppHost.csproj
```

**Single Host** (combined):
```bash
dotnet new krafter-single -n MyCompanyApp
cd MyCompanyApp
dotnet run --project aspire/MyCompanyApp.Aspire.AppHost/MyCompanyApp.Aspire.AppHost.csproj
```

> **Important:** Always use the `-n` parameter to specify your project name. This replaces "Krafter" with "MyCompanyApp" throughout the entire codebase (namespaces, project files, folder names, etc.).

### When to Choose Each Variant

**Choose Split Host (`krafter`) when:**
- You want to scale API and UI independently
- Different teams own backend vs frontend
- You plan to add additional API consumers (mobile, CLI)
- You prefer explicit service boundaries

**Choose Single Host (`krafter-single`) when:**
- You want the simplest possible deployment
- Lower latency for server-side rendering
- Single container or App Service deployment
- Smaller teams managing the full stack

### Update the Template

```bash
dotnet new update
```

### Uninstall the Template

```bash
dotnet new uninstall AditiKraft.Krafter.Templates
```

---

---

<div align="center">

**Built with ❤️ by [Aditi Kraft](https://github.com/AditiKraft)**

⭐ Star this repository if you find it helpful!

</div>
