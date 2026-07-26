# Audit de conformité à la spec §6 (accès) et au workflow métier

> **MISE À JOUR 24/07/2026 — les points P0/P1 ci-dessous ont été corrigés.**
> Voir la section « Correctifs appliqués » en fin de document. Le corps de
> l'audit décrit l'état **avant** correction (conservé comme trace).

> Rédigé le 24/07/2026. Référence : `GPIA_Specifications_Fonctionnelles_Techniques_Codex_v2.0`.
> Méthode : lecture de la spec (§6 rôles/matrice, §12 réservation, §31 contrat
> API, §47 transitions), inventaire statique des 18 contrôleurs ProjectAPI + 2
> AuthenticationAPI, audit du frontend, puis **tests live** sur les deux API
> réellement démarrées (SQLEXPRESS, 7 comptes réels).

## Verdict en une phrase

Le **workflow métier** (machines à états réservation/bien) est correctement
modélisé et conforme au §47. La **gestion des accès (§6) n'est pas appliquée
côté API** : aucun contrôleur n'impose de rôle, et un appel **anonyme** obtient
des données — ce que le §6.4 interdit explicitement (« contrôles appliqués côté
API, jamais uniquement dans l'interface »).

---

## 1. Preuve live : l'API ne protège rien (CRITIQUE)

### 1.1 Fuite de l'annuaire utilisateurs — anonyme, sans jeton

```
GET http://localhost:48988/api/User        (aucun header Authorization)
→ HTTP 200 — 7 comptes complets :
  nom, prénom (FR + AR), email, téléphone, rôles, état de verrouillage
```

`§6.3` réserve « Utilisateurs internes » à `Admin projet`/`Admin global`. Ici
n'importe qui sur le réseau lit l'annuaire complet, PII comprise. C'est aussi une
violation directe de `§34` (sécurité) et de `§6.4`.
`GET /api/User/roles` et `GET /api/User/by-role/Admin` : idem, `200` anonyme.

### 1.2 Aucune différenciation par rôle (ProjectAPI)

Probe : login réel de chaque rôle, puis même endpoint. Colonne `ANON` = sans jeton.

| Endpoint | GLOBAL_ADMIN | SALES_AGENT | NOTARY | BUYER | TECHNICIAN | ANON |
|---|---|---|---|---|---|---|
| GET /api/Project | 404 | 404 | 404 | 404 | 404 | **404** |
| GET /api/Immeuble/all | 500 | 500 | 500 | 500 | 500 | **500** |
| GET /api/Reservations/list | 500 | 500 | 500 | 500 | 500 | **500** |
| GET /api/ProjectAssignment | 500 | 500 | 500 | 500 | 500 | **500** |
| GET /api/after-sales/claims | 500 | 500 | 500 | 500 | 500 | **500** |
| GET /api/AdminDashboard/overview | 500 | 500 | 500 | 500 | 500 | **500** |

Le point n'est pas le code de statut mais son **uniformité** : la colonne `ANON`
est identique aux colonnes authentifiées. Un endpoint protégé rendrait `401` à
l'anonyme — jamais le cas ici. Le `BUYER` obtient exactement la même chose que le
`GLOBAL_ADMIN` sur des endpoints d'administration.

> Les `500` proviennent d'un souci d'accès Windows-auth du process au
> `GPIA_Project` sur cette machine (le process et `sqlcmd` présentent tous deux
> le compte `whiredead` mais avec des jetons de logon différents), **pas** du
> code applicatif. Le `404` sur `/api/Project` prouve à lui seul l'absence de
> gate d'authentification : un anonyme atteint le handler (404 « liste vide »)
> au lieu d'être rejeté en `401`.

---

## 2. Inventaire de l'enforcement (analyse statique)

### 2.1 ProjectAPI — 18 contrôleurs

| Contrôleur | État de l'autorisation | Attendu §6.3 |
|---|---|---|
| `AppointmentsController` | `[Authorize(Bearer)]` classe, mais **POST/GET/PATCH principaux en `[AllowAnonymous]`**, `[Authorize(Roles=…)]` commenté | RDV : `C` visiteur, `V/M` agent, `A` admin |
| `ReservationsController` | **aucun** attribut ; `approve/reject/submit` ouverts | `V périmètre` admin projet ; agent `C/M` |
| `ProjectController` | `[Authorize]` **commenté** en tête de classe | `A périmètre` admin, `L` public |
| `ImmeubleController` | `[Authorize]` **commenté** | `A périmètre` admin |
| `NotaryAppointmentsController` | `[AllowAnonymous]`, `[Authorize(Roles="Admin,Notaire")]` commenté | `V/M affecté` notaire |
| `ClaimsController` (after-sales) | **aucun** attribut | SAV : `C/M affecté` technicien, `A` admin |
| `ConstructionController` | **aucun** attribut | `A périmètre` admin, `L` selon rôle |
| `PaymentsController` | **aucun** attribut | `C/M périmètre` admin, `L propre` acheteur |
| `DeliveriesController` | **aucun** attribut | `A périmètre` admin |
| `FinalVisitsController` | **aucun** attribut | `V/M affecté` agent, `C/M propre` acheteur |
| `SalesController` | **aucun** attribut | `A` admin |
| `ProjectAssignmentController` | **aucun** attribut | `A` admin uniquement |
| `AdminDashboardController` | **aucun** attribut | admin |
| `TypeBiensController` | **aucun** attribut | admin |
| `FeedbackController` | `[Authorize(Bearer)]` classe, endpoints `[AllowAnonymous]` | modération admin |
| `FileController` | **aucun** attribut | `§24.2` doc privé = accès contrôlé |
| `NotaryBlocksController` | **aucun** attribut | notaire propriétaire |
| `ConstructionController`/autres | idem | — |

Aucun contrôleur n'appelle `ProjectScopeService` / `EnsureProjectAccessAsync`
(la brique de périmètre §6.4 existe mais **n'est câblée nulle part**).

### 2.2 Règles §6.4 spécifiques — état

| Règle §6.4 | Implémenté ? |
|---|---|
| Un agent ne peut pas approuver **sa propre** réservation | ❌ `Approve` n'a aucun contrôle du `owner_sales_agent_id` |
| Vérifier l'affectation projet à chaque requête ressource | ❌ `ProjectScopeService` jamais appelé |
| Un `PROJECT_ADMIN` ne peut pas s'octroyer `GLOBAL_ADMIN` | ❌ pas de garde |
| Un `TECHNICIAN` n'accède qu'à ses réclamations affectées | ❌ `ClaimsController` ouvert |
| Un notaire n'accède pas au CRM complet | ❌ pas de restriction |
| Un acheteur n'accède qu'à ses propres données | ❌ `EnsureBuyerOwnsReservationAsync` existe mais non appelé |

---

## 3. Workflow métier — **conforme** (le bon côté)

- `§47.1` réservation : `ReservationStateMachine.CanTransition` implémente
  exactement `DRAFT→SUBMITTED→{CHANGES_REQUESTED,APPROVED,REJECTED,EXPIRED}…`,
  terminaux corrects, et `BlocksUnit` exclut `DRAFT` (§12.1 : un brouillon ne
  bloque pas le bien). ✔
- `§47.2` bien : transitions AVAILABLE→HOLD_PENDING_APPROVAL→RESERVED… présentes.
- Les endpoints de réservation existent tous (`submit`, `approve`, `reject`,
  `request-changes`, `cancel`) et correspondent au §31.4.

---

## 4. Contrat API §31 — écarts

| Exigence | État |
|---|---|
| `§31.5` en-tête `Idempotency-Key` (submit/approve, paiement, campagne, RDV) | ❌ Non lu nulle part. Seule la constante `IDEMPOTENCY_KEY_REUSED` existe. |
| `§31.6` `If-Match`/ETag + `409 RESOURCE_VERSION_CONFLICT` | ❌ Aucun contrôle de version optimiste (la colonne `version` du §48.1 n'est pas exploitée par les handlers). |
| `§31.3` codes HTTP (403 hors périmètre, 404 hors périmètre) | ⚠️ jamais atteints puisque rien n'est refusé. |
| Nommage des routes (`/reservations`, `/crm/…`) | ⚠️ divergent (`/api/Reservations/create`, PascalCase) mais cohérent en interne. |

---

## 5. Frontend (`realestateFront`) — écarts

1. **Vocabulaire de rôles désaligné.** `UserRole` = `"Admin" | "Agent" |
   "Notaire" | "Acheteur"` (`src/types/index.ts`), alors que la réponse de login
   renvoie déjà les **codes spec** (`GLOBAL_ADMIN`, `SALES_AGENT`, `NOTARY`,
   `BUYER`). `authApi.ts` fait `roles as UserRole[]` sans conversion : le gating
   de `ProtectedRoute`/`AdminLayout` compare `"Admin"` à `"GLOBAL_ADMIN"` →
   **plus aucune correspondance**. Concrètement, avec l'API réelle, tout
   utilisateur connecté est traité comme sans rôle et redirigé hors `/admin`.
2. **`TECHNICIAN` absent** du gating et de la nav (§28.5 lui donne 6 rubriques,
   dont *Réclamations* et *Interventions*).
3. **Nav très en deçà du §28.7/§28.8** : `AdminLayout` a 13 entrées ; l'admin
   projet en attend ~20, l'admin global ~30.
4. **`403 PROJECT_SCOPE_DENIED` non géré** dans `client.ts` (le code n'est même
   pas dans l'union `BusinessErrorCode`).
5. **Jetons en `localStorage`** (`client.ts`, `AuthContext.tsx`) : `§28.1`
   interdit « aucun jeton conservé dans un stockage web non sécurisé ».

---

## 6. Priorités recommandées

1. **P0 — fermer l'API.** Politique d'autorisation par défaut (`FallbackPolicy`
   = authentifié) dans les deux `Program.cs`, puis retirer les `[AllowAnonymous]`
   des endpoints non publics. Sans cela, tout le reste est cosmétique.
2. **P0 — annuaire users.** Mettre `[Authorize(Roles=…AnyAdmin)]` sur
   `UserController` (sauf `login`/`register`/`confirm-email`).
3. **P1 — câbler `ProjectScopeService`** dans les handlers réservation /
   paiement / claims / construction (la brique est prête, cf. `RBAC_PROGRESS.md`).
4. **P1 — self-approval** : bloquer `approve` quand `caller == owner_sales_agent_id`.
5. **P1 — frontend** : convertir/normaliser les rôles sur les codes spec, ajouter
   `TECHNICIAN`, gérer `403`.
6. **P2 — §31.5/§31.6** : Idempotency-Key + If-Match sur les commandes sensibles.

---

## Annexe — comment reproduire

```
# 1. démarrer les deux API (voir RBAC_PROGRESS.md « Environnement local »)
# 2. fuite anonyme :
curl http://localhost:48988/api/User            # → 200 + 7 users, sans token
# 3. matrice par rôle :
powershell scratchpad/probe_authz.ps1           # (script d'audit, hors repo)
```

---

## Correctifs appliqués (24/07/2026)

### API — fermeture par défaut
- **Les deux `Program.cs`** : `FallbackPolicy = RequireAuthenticatedUser()` →
  tout endpoint exige une authentification sauf `[AllowAnonymous]` explicite.
- **AuthenticationAPI** : le schéma Bearer était déclaré (Infrastructure) mais le
  pipeline n'appelait jamais `UseAuthentication()` — ajouté. `UserController`
  passe en `[Authorize]`, avec `[AllowAnonymous]` sur login/register/confirm-email/
  reset-password et OTP ; les endpoints d'annuaire (`GetUsers`, lockout, unlock,
  delete, update) en `[Authorize(Roles = admins)]`.
- **ProjectAPI** : `PostConfigure<AuthenticationOptions>` réaffirme Bearer comme
  challenge par défaut (sinon AddIdentity redirige en **302** au lieu de 401).
- **RegisterHandler** : un compte interne ne peut être créé que par un admin
  authentifié (§6.2) — plus d'auto-attribution de rôle Admin en anonyme.

### Matrice §6.3 sur les contrôleurs
`[Authorize(Roles = …)]` appliqué via `RoleGroups.cs` (codes spec) sur les 18
contrôleurs. Lectures catalogue public (`Projects`, `Immeuble`, `TypeBiens`,
construction publiée), demande de RDV visiteur et feedback public restent
`[AllowAnonymous]`.

### §6.4
- Auto-approbation bloquée : `ApproveReservationHandler` compare l'appelant
  (identité du **token**, pas `AdminUserId` du corps) à `reservation.AgentId` →
  403 `SELF_APPROVAL_FORBIDDEN`. `ValidatedBy` est renseigné depuis le token.
- Brique de périmètre `ProjectScopeService` prête et enregistrée (le câblage
  handler-par-handler reste à finir — voir « restant »).

### Frontend
- `UserRole` migré sur les codes §6.1 ; `src/lib/roles.ts` (miroir de
  `RoleGroups.cs`) + `normalizeRole()` défensif. `App.tsx`, `AdminLayout.tsx`,
  `ProtectedRoute` alignés. `TECHNICIAN` ajouté (section Réclamations SAV).
- `client.ts` : codes `PROJECT_SCOPE_DENIED` / `SELF_APPROVAL_FORBIDDEN`,
  helpers `isForbidden`/`isScopeDenied`, et déconnexion auto sur 401.

### Vérifié en live (deux API démarrées, 7 comptes réels)
| Cas | Avant | Après |
|---|---|---|
| `GET /api/User` anonyme | 200 + 7 comptes (PII) | **401** |
| Endpoints protégés anonyme | 200/500 | **401** |
| Catalogue public anonyme | 200 | 200 (inchangé) |
| `AdminDashboard` admin / agent / buyer / tech | 200 / 200 / 200 / 200 | **200 / 403 / 403 / 403** |
| `after-sales/claims` admin / agent / buyer / tech | 200 partout | **200 / 403 / 403 / 200** |

Frontend : `tsc -b` et `vite build` OK.

### Restant (non couvert par cette passe)
- Câbler `ProjectScopeService.EnsureProjectAccessAsync` / `EnsureReservationAccessAsync`
  dans les handlers (réservation, paiement, claims, construction) : le rôle est
  vérifié, **le périmètre projet ne l'est pas encore** au niveau handler.
- `§31.5` Idempotency-Key et `§31.6` If-Match/ETag toujours absents.
- Bug préexistant hors périmètre : `GET /api/Reservations/list` renvoie 500
  (erreur de handler, pas d'autorisation) — à investiguer séparément.
- Nuance §6.3 : l'agent a « L affecté » sur le SAV ; l'endpoint `claims` GET est
  actuellement restreint admin+technicien (lecture agent non ré-ouverte).
