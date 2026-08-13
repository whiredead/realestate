# Configuration secrets

Secrets are **never** committed to `appsettings.json`. They are supplied per
environment through the standard .NET configuration providers, which override
`appsettings.json` automatically:

* **Locally** — `dotnet user-secrets` (stored in your user profile, outside the
  repository).
* **In a deployment** — environment variables or app-service settings. Nested
  keys use a double underscore: `BlobStorage:ConnectionString` becomes
  `BlobStorage__ConnectionString`.

## ProjectAPI (`src/ProjectAPI/src/Api`)

| Key | Purpose | Required |
| --- | --- | --- |
| `BlobStorage:ConnectionString` | Azure Storage account for images and documents | Yes — startup fails without it |
| `Smtp:UserName` / `Smtp:Password` | Outbound mail | No — nothing in ProjectAPI sends mail today |

```bash
cd src/ProjectAPI/src/Api
dotnet user-secrets set "BlobStorage:ConnectionString" "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
```

## AuthenticationAPI (`src/AuthenticationAPI/src/Api`)

| Key | Purpose | Required |
| --- | --- | --- |
| `Smtp:UserName` | Mailbox used for password-reset mail | Only if password reset is used |
| `Smtp:Password` | Mailbox password / app password | Only if password reset is used |

```bash
cd src/AuthenticationAPI/src/Api
dotnet user-secrets set "Smtp:UserName" "someone@example.com"
dotnet user-secrets set "Smtp:Password" "app-specific-password"
```

Only `ForgotPasswordHandler` sends mail. If password reset is not in use, these
can be left unset — the API starts normally and only the send itself would fail.

## History

Two live credentials were previously hardcoded in this repository:

* an Azure Storage account key, in `src/ProjectAPI/src/Api/appsettings.json`
* an SMTP mailbox password, in both APIs' `Infrastructure/Providers/EmailService.cs`

Both have been moved to configuration. **They remain in the git history**, so
they should be treated as compromised and rotated:

1. Regenerate the storage account key in the Azure portal (`belmouddenstorage`).
2. Change the mailbox password for the SMTP account.
3. Set the new values via user-secrets / environment variables as above.

Rotating is what actually closes the exposure — rewriting history is optional
and does not help once a secret has been pushed.
