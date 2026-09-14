# Fixes — CRM (backend)

## Reservation creation failed (409) as soon as a contact number was skipped
- **Files:** `Application/Common/Crm/ContactResolver.cs`
- **Wrong:** a new CRM contact was numbered `CT-{count + 1}`. After one deletion or an import out of sequence, count + 1 named a number that already existed and every new contact hit the unique index — no new buyer could be reserved for. The Azure development database was in that state (11 contacts, highest number CT-000191).
- **Changed:** next number = highest existing `CT-` number (and pending ones in the same save) + 1.
