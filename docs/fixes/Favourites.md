# Fixes — Favourites → leads (backend)

## Liking twice duplicated the like, the counter and the leads
- **Files:** `src/ProjectAPI/src/Api/Application/Projects/LikedProjects/AddLikedProject/AddLikedProjectHandler.cs`
- **Wrong:** no existence check: a second like inserted another `LikedProject`, incremented `NumberLikes` again and duplicated every `Lead` and agent increment; un-favourite removed one row only, so counters could never be walked back. A like on an unknown project was stored anyway; timestamps were local time.
- **Changed:** idempotent (returns the existing favourite, no side effect); unknown project → 404; `DateTime.UtcNow`. Ownership pinned to the token for buyers — see Security.md.
