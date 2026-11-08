# AI Agent Test Prompts for Krafter

> Copy a prompt and give it to an AI agent with access to this codebase.

---

## Prompt 1: Basic (Backend Only)

```
Create a "NotificationPreference" feature in the Krafter project.

Entity properties:
- UserId (string)
- EmailEnabled (bool)
- PushEnabled (bool)  
- Frequency (string - values: "Instant", "Daily", "Weekly")

Requirements:
1. Create the entity in src/AditiKraft.Krafter.Backend/Features/NotificationPreferences/Common/
2. Create shared contracts in src/AditiKraft.Krafter.Contracts/Contracts/NotificationPreferences/
3. Create separate CreateNotificationPreference.cs and UpdateNotificationPreference.cs operations,
   each with its Handler and Route, using the corresponding Create or Update permission
4. Keep each request's FluentValidation validator with that request
5. Add persistence configuration, migration, routes, and permission catalog entries using existing feature patterns
```

---

## Prompt 2: Intermediate (Backend + Contracts)

```
Implement a complete "Category" feature in Krafter.

Entity properties:
- Name (string, required, max 100 chars)
- Description (string, optional, max 500 chars)
- Color (string, hex format like "#FF5733")
- SortOrder (int)
- IsActive (bool, default true)

Requirements:
1. Shared contracts in src/AditiKraft.Krafter.Contracts/Contracts/Categories/:
   CategoryDto, CreateCategoryRequest, and UpdateCategoryRequest, with each validator beside its request
2. Backend entity in src/AditiKraft.Krafter.Backend/Features/Categories/Common/
3. Separate GetCategories (paginated), CreateCategory, UpdateCategory, and DeleteCategory operations,
   each with its Handler and Route and the corresponding View, Create, Update, or Delete permission
4. Add DbSet to ApplicationDbContext with EF configuration
5. Add routes in Common/ApiRoutes.cs, permission catalog entries in Common/Auth/Permissions/,
   and an EntityKind value in Common/Enums/ (all under src/AditiKraft.Krafter.Contracts/)
```

---

## Prompt 3: Advanced (Full Stack)

```
Build a complete "Activity Log" feature to track user actions in Krafter.

Entity properties:
- Action (string - "Created", "Updated", "Deleted", "Viewed")
- EntityType (string - e.g., "User", "Role")
- EntityId (string)
- Details (string)
- PerformedAt (DateTime)
- PerformedById (string)

Requirements:
1. Shared contracts in src/AditiKraft.Krafter.Contracts/Contracts/ActivityLogs/
   (ActivityLogDto, CreateActivityLogRequest with its validator)
2. Backend entity in Features/ActivityLogs/Common/ and separate GetActivityLogs (with date filtering)
   and CreateActivityLog operations in Features/ActivityLogs/, each with its Handler and Route
   (all backend paths are under src/AditiKraft.Krafter.Backend/)
3. UI list page with RadzenDataGrid and date filter
4. Refit interface and registration
5. Menu item integration
6. Permissions: View, Create, Export (Export as IsRoot: true)
```

---

## Prompt 4: Complex (Multi-Entity + Background Jobs)

```
Implement a "Project" feature with team member assignments in Krafter.

Entities:

Project:
- Name (string, required, max 200 chars)
- Description (string, optional)
- OwnerId (string, FK to User)
- Status (string - "Active", "OnHold", "Completed")
- StartDate (DateTime?)
- DueDate (DateTime?)

ProjectMember:
- ProjectId (string, FK to Project)
- UserId (string, FK to User)
- Role (string - "Lead", "Member")
- AssignedAt (DateTime)

Requirements:
1. Shared contracts for both entities in src/AditiKraft.Krafter.Contracts/Contracts/Projects/;
   keep request validators with their requests and backend entities in
   src/AditiKraft.Krafter.Backend/Features/Projects/Common/
2. Backend operations:
   - Separate GetProjects, CreateProject, UpdateProject, and DeleteProject operations,
     each with its Handler and Route and the corresponding permission (only owner can delete)
   - AssignMember (send email notification via background job)
   - RemoveMember (owner cannot be removed)
   - GetMembers
3. Business rules:
   - Same user cannot be assigned twice (return conflict)
   - Send email when member assigned using IJobService
4. UI: Project list, form dialog, member management
5. Full integration (Refit, menu, permissions)
```
