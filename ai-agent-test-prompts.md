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
1. Create entity in Features/NotificationPreferences/_Shared/
2. Create shared contracts in Krafter.Shared/Contracts/NotificationPreferences/
3. Create CreateOrUpdateNotificationPreference.cs operation
4. Include FluentValidation for the request
```

---

## Prompt 2: Intermediate (Backend + Shared)

```
Implement a complete "Category" feature in Krafter.

Entity properties:
- Name (string, required, max 100 chars)
- Description (string, optional, max 500 chars)
- Color (string, hex format like "#FF5733")
- SortOrder (int)
- IsActive (bool, default true)

Requirements:
1. Shared contracts (CategoryDto, CreateCategoryRequest with validator)
2. Backend entity
3. All three operations: Get (paginated), CreateOrUpdate, Delete
4. Add DbSet to KrafterContext with EF configuration
5. Add permissions and EntityKind enum value
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
1. Shared contracts (ActivityLogDto, CreateActivityLogRequest)
2. Backend entity and operations (Get with date filtering, Create)
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
1. Shared contracts for both entities
2. Backend operations:
   - Project CRUD (only owner can delete)
   - AssignMember (send email notification via background job)
   - RemoveMember (owner cannot be removed)
   - GetMembers
3. Business rules:
   - Same user cannot be assigned twice (return conflict)
   - Send email when member assigned using IJobService
4. UI: Project list, form dialog, member management
5. Full integration (Refit, menu, permissions)
```
