# Project Deletion Flow Diagram

## Mermaid Flowchart

```mermaid
flowchart TD
    A[Start: DeleteProject Request] --> B{Validate Project Exists}
    B -->|Not Found| C[Return 404 Not Found]
    B -->|Exists| D{Check Already Deleted}
    D -->|Already Deleted| E[Return 400 Bad Request]
    D -->|Not Deleted| F{Validate User Permissions}
    F -->|No Permission| G[Return 403 Forbidden]
    F -->|Has Permission| H{Check Project Status}
    H -->|Not Deletable Status| I[Return 400 Bad Request<br/>Status not allowed]
    H -->|Deletable Status| J{Check Active Appointments}
    J -->|Has Active Appointments| K[Return 400 Bad Request<br/>Active appointments exist]
    J -->|No Active Appointments| L{Check Pending Reservations}
    L -->|Has Pending Reservations| M[Return 400 Bad Request<br/>Pending reservations exist]
    L -->|No Pending Reservations| N{Check Active Sales}
    N -->|Has Active Sales| O[Return 400 Bad Request<br/>Active sales exist]
    N -->|No Active Sales| P[Begin Database Transaction]
    P --> Q[Update Project: Set IsDeleted=true]
    Q --> R[Set DeletedAt = DateTime.UtcNow]
    R --> S[Set DeletedBy = UserId]
    S --> T[Set DeletionReason]
    T --> U[Create Audit Log Entry]
    U --> V[Save Changes to Database]
    V --> W{Save Successful}
    W -->|Failure| X[Rollback Transaction]
    X --> Y[Return 500 Internal Server Error]
    W -->|Success| Z[Commit Transaction]
    Z --> AA[Send Notifications to Stakeholders]
    AA --> BB[Clear Related Cache]
    BB --> CC[Return 200 OK<br/>Deletion Successful]
    
    style A fill:#e1f5fe
    style CC fill:#c8e6c9
    style C fill:#ffcdd2
    style E fill:#ffcdd2
    style G fill:#ffcdd2
    style I fill:#ffcdd2
    style K fill:#ffcdd2
    style M fill:#ffcdd2
    style O fill:#ffcdd2
    style Y fill:#ffcdd2
```

## Entity Relationship Impact Diagram

```mermaid
erDiagram
    Project {
        Guid Id
        string Name
        bool IsDeleted
        DateTime DeletedAt
        string DeletedBy
        string DeletionReason
    }
    
    Immeuble {
        Guid Id
        Guid ProjectId
        bool IsDeleted
    }
    
    Unit {
        Guid Id
        Guid ProjectId
        bool IsDeleted
    }
    
    Appointment {
        Guid Id
        Guid ProjectId
        string Status
    }
    
    Reservation {
        Guid Id
        Guid UnitId
        string Status
    }
    
    Sale {
        Guid Id
        Guid UnitId
    }
    
    LikedProject {
        Guid Id
        Guid ProjectId
        string UserId
    }
    
    ProjectAssignment {
        Guid Id
        Guid ProjectId
        string AgentId
        string NotaryId
    }
    
    ProjectFeature {
        Guid Id
        Guid ProjectId
        string Name
    }
    
    EspaceTempsReel {
        Guid Id
        Guid ProjectId
        string VideoLink
    }
    
    Project ||--o{ Immeuble : contains
    Project ||--o{ Appointment : has
    Project ||--o{ LikedProject : liked_by
    Project ||--o{ ProjectAssignment : assigned_to
    Project ||--o{ ProjectFeature : has
    Project ||--o{ EspaceTempsReel : has
    Immeuble ||--o{ Unit : contains
    Unit ||--o{ Reservation : reserved
    Unit ||--o{ Sale : sold
```

## Validation Decision Tree

```mermaid
flowchart TD
    A[Project Deletion Request] --> B{Project Exists?}
    B -->|No| C[404 Not Found]
    B -->|Yes| D{Already Deleted?}
    D -->|Yes| E[400 Bad Request]
    D -->|No| F{User Has Delete Permission?}
    F -->|No| G[403 Forbidden]
    F -->|Yes| H{Project Status Deletable?}
    H -->|No| I[400 Bad Request]
    H -->|Yes| J{Active Appointments?}
    J -->|Yes| K[400 Bad Request]
    J -->|No| L{Pending Reservations?}
    L -->|Yes| M[400 Bad Request]
    L -->|No| N{Active Sales?}
    N -->|Yes| O[400 Bad Request]
    N -->|No| P[Proceed with Deletion]
    
    style P fill:#c8e6c9
    style C fill:#ffcdd2
    style E fill:#ffcdd2
    style G fill:#ffcdd2
    style I fill:#ffcdd2
    style K fill:#ffcdd2
    style M fill:#ffcdd2
    style O fill:#ffcdd2
```

## Transaction Flow Diagram

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant Handler
    participant Repository
    participant Database
    participant AuditService
    participant NotificationService
    
    Client->>API: DELETE /api/projects/{id}
    API->>Handler: DeleteProjectCommand
    Handler->>Repository: GetProjectById(id)
    Repository->>Database: SELECT * FROM Projects WHERE Id = id
    Database-->>Repository: Project Data
    Repository-->>Handler: Project Entity
    
    Handler->>Handler: Validate Business Rules
    
    alt Validation Fails
        Handler-->>API: Validation Error
        API-->>Client: 400 Bad Request
    else Validation Success
        Handler->>Database: BEGIN TRANSACTION
        Handler->>Repository: UpdateProject(project)
        Repository->>Database: UPDATE Projects SET IsDeleted = 1, ...
        Handler->>AuditService: LogDeletion(project, userId)
        AuditService->>Database: INSERT INTO AuditLog ...
        Handler->>Database: COMMIT TRANSACTION
        
        Handler->>NotificationService: SendDeletionNotifications(project)
        NotificationService-->>Handler: Notifications Sent
        
        Handler-->>API: Success Response
        API-->>Client: 200 OK
    end