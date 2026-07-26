# Implémentation §6 « Acteurs et gestion des accès » — état d'avancement

> Document de reprise. Rédigé le 24/07/2026 à l'arrêt de la session.
> Référence : `GPIA_Specifications_Fonctionnelles_Techniques_Codex_v2.0`, section 6.

## Contexte

La spec §6 impose une matrice d'autorisation à 8 rôles, appliquée **côté API**
(§6.4 : « Les contrôles d'accès sont appliqués côté API, **jamais uniquement
dans l'interface** »).

État constaté au démarrage :

| Aspect | Constat |
|---|---|
| Rôles | 4 utilisés (`Admin`, `Agent`, `Notaire`, `Acheteur`) + 4 legacy inutilisés (`AgentBch`, `Observer`, `Other`, `SecurityOfficer`) |
| Rôles spec manquants | `TECHNICIAN`, `PROSPECT`, séparation `PROJECT_ADMIN`/`GLOBAL_ADMIN` |
| Constantes centralisées | Aucune — chaînes littérales dispersées |
| Périmètre projet | Inexistant, alors que presque chaque ligne de §6.3 dit « périmètre » / « affecté » |
| Enforcement API | Majoritairement `[Authorize(Roles=…)]` **commenté** ou `[AllowAnonymous]` |
| Enforcement UI | « connecté ou non » uniquement, même menu pour tous les rôles |

---

## ✅ Étapes terminées

### 1. Audit du modèle de rôles

Fait. Résultats dans le tableau ci-dessus.

Fichiers inspectés :
- `src/AuthenticationAPI/src/Api/Application/Users/Register/RegisterValidator.cs`
- `src/AuthenticationAPI/src/Api/Application/Users/Register/RegisterHandler.cs`
- `src/AuthenticationAPI/src/Infrastructure/Providers/TokenProvider.cs`
- `src/AuthenticationAPI/src/Domain/ApplicationUser/Entities/UserRoles.cs`

### 2. Alignement des codes de rôles sur §6.1

**Créé** `RoleCodes.cs` (en double, un par API — les deux projets ne partagent
pas d'assembly commun) :
- `src/ProjectAPI/src/Domain/Users/Entities/RoleCodes.cs`
- `src/AuthenticationAPI/src/Domain/ApplicationUser/Entities/RoleCodes.cs`

Contenu : les 8 codes §6.1 (`VISITOR`, `PROSPECT`, `BUYER`, `SALES_AGENT`,
`TECHNICIAN`, `NOTARY`, `PROJECT_ADMIN`, `GLOBAL_ADMIN`), les regroupements
(`Assignable`, `Internal`, `AnyAdmin`) et `Normalize()` qui mappe les libellés
legacy vers les codes spec.

**Deux mappings volontairement lossy, documentés dans le fichier :**
- `Admin` → `GLOBAL_ADMIN` (et non `PROJECT_ADMIN`) : le modèle legacy n'a qu'un
  niveau d'admin ; rétrograder les comptes existants leur retirerait des accès
  qu'ils ont déjà. La vraie séparation exige d'affecter des périmètres réels.
- Valeurs inconnues / legacy inutilisées → `PROSPECT` (fail **closed**, jamais
  open).

### 3. JWT et réponse de login

**Modifié** `src/AuthenticationAPI/src/Infrastructure/Providers/TokenProvider.cs`
→ `AddRolesToClaims` émet désormais **les deux** formes dans `ClaimTypes.Role` :
le libellé legacy (pour que les `[Authorize(Roles="Admin")]` existants
continuent de fonctionner) **et** le code spec normalisé (pour les nouvelles
règles). N'émettre que l'un des deux casserait soit tout l'existant, soit
l'applicabilité de la matrice.

**Modifié** `src/AuthenticationAPI/src/Api/Application/Users/Login/LoginHandler.cs`
→ `Roles = RoleCodes.Normalize(user.GetRoleNames()).ToList()` : la réponse de
login renvoie des codes spec purs, pour que l'UI et l'API partagent un seul
vocabulaire.

**Vérifié en live** (login réel sur `localhost:48988`, JWT décodé) :

| Compte | `roles` (réponse login) | `ClaimTypes.Role` (JWT) |
|---|---|---|
| yassine.aitmoussaa@gmail.com | `GLOBAL_ADMIN` | `Admin`, `GLOBAL_ADMIN` |
| sara.elfassi@samgroup.ma | `SALES_AGENT` | `Agent`, `SALES_AGENT` |
| amine.kabbaj@notaires.ma | `NOTARY` | `Notaire`, `NOTARY` |
| karim.tazi@gmail.com | `BUYER` | `Acheteur`, `BUYER` |

### 4. Rôle `TECHNICIAN` ajouté

**Modifié** `RegisterValidator.cs` : liste `AllowedRoles` acceptant les deux
vocabulaires (codes spec + libellés legacy). `VISITOR` volontairement exclu —
§6.1 le définit comme l'utilisateur **non authentifié**, il ne peut donc jamais
être stocké sur un compte.

**Base `GPIA_Auth`** : ajout des 7 rôles Identity assignables
(`PROSPECT`, `BUYER`, `SALES_AGENT`, `TECHNICIAN`, `NOTARY`, `PROJECT_ADMIN`,
`GLOBAL_ADMIN` + libellé legacy `Technicien`). Les rôles legacy sont
**conservés** : des comptes existants les référencent, les supprimer
orphelinerait leurs affectations.

> Note : `AspNetRoles` a une colonne `DisplayName` NOT NULL (entité `Role`
> personnalisée) et un index filtré — d'où `sqlcmd -I` (`QUOTED_IDENTIFIER ON`)
> obligatoire pour tout INSERT sur cette table.

**Vérifié en live** : compte `tech.sav@samgroup.ma` créé avec `roles:["TECHNICIAN"]`,
login OK, JWT → `role claims : TECHNICIAN`.

### 5. Modèle de périmètre projet (§6.4) — *partiellement fait*

Décision : **réutiliser** la table existante `ProjectAssignments`
(colonnes `AgentId` / `NotaryId` / `IsActive`, 7 lignes de données réelles)
plutôt que créer une table parallèle `user_project_roles`.

**Créés :**
- `src/ProjectAPI/src/Api/Application/Common/Security/CurrentUser.cs`
  → `ICurrentUser` / `CurrentUser` : identité du appelant exprimée en codes
  §6.1, un seul endroit qui répond « qui est-ce et que peut-il voir ».
- `src/ProjectAPI/src/Api/Application/Common/Security/ProjectScopeService.cs`
  → `GetScopedProjectIdsAsync` (null = tous, GLOBAL_ADMIN uniquement),
  `CanAccessProjectAsync`, `EnsureProjectAccessAsync`,
  `EnsureReservationAccessAsync` (chaîne réservation → unité → immeuble →
  projet), `EnsureBuyerOwnsReservationAsync` (§6.4 « un acheteur ne peut
  accéder qu'à ses propres données »).

**Modifié** `src/ProjectAPI/src/Api/Application/Common/Exceptions/BusinessErrorCodes.cs`
→ ajout de `PROJECT_SCOPE_DENIED` (distinct de `UNAUTHORIZED` : le appelant est
authentifié et correctement roled, simplement pas affecté à ce projet).

### 6. Câblage du périmètre — *terminé (session du 24/07/2026, reprise)*

**Ajouté** dans `src/ProjectAPI/src/Api/Application/Common/Exceptions/BusinessRuleException.cs` :
- `ProjectScopeDenied(Guid projectId)` → 403 `PROJECT_SCOPE_DENIED`. Le message
  ne nomme **pas** le projet : on vient de dire au appelant qu'il n'a pas le
  droit de le voir, en réafficher l'identifiant confirmerait son existence à
  quelqu'un hors périmètre. Le `projectId` reste en paramètre pour le log.
- `BuyerScopeDenied()` → même code/statut, message adapté. Un acheteur n'a pas
  de « périmètre de projets » ; `EnsureBuyerOwnsReservationAsync` appelait
  `ProjectScopeDenied(Guid.Empty)`, ce qui produisait un message faux.

**Modifié** `src/ProjectAPI/src/Api/Application/DependencyInjection.cs` →
`AddHttpContextAccessor()`, `ICurrentUser`/`CurrentUser` et `ProjectScopeService`
en **Scoped** (ils lisent les claims de la requête courante, un singleton leur
ferait survivre la requête).

**Vérifié** : `dotnet build src/Api/ProjectAPI.Api.csproj` → **0 erreur**
(326 warnings, tous préexistants). Le piège `Domain.Immeubles.Entities.Unit`
vs `MediatR.Unit` était déjà correctement traité dans les jointures.

Reste ouvert sur ce lot :
- [ ] `TECHNICIAN` et `PROJECT_ADMIN` n'ont **pas** de colonne dans
      `ProjectAssignments` : décider si on ajoute des colonnes, une table de
      liaison générique, ou si on se limite à agent/notaire pour l'instant

---

## ⛔ Point d'arrêt exact

**Arrêté après** le câblage du périmètre (étape 6) et la migration de
l'environnement vers SQL Server Express (voir « Environnement local » plus bas).

**La toute prochaine action** est l'étape 7 : appliquer la matrice §6.3 sur les
contrôleurs ProjectAPI. Rien ne bloque la compilation.

⚠️ **Bases repartant de zéro.** Schéma recréé sur SQLEXPRESS, 12 rôles et les
7 comptes de test réinsérés et revérifiés. En revanche **aucune donnée métier**
(projets, immeubles, biens, `ProjectAssignments`) n'a été recréée : les tests de
périmètre de l'étape 10 restent bloqués tant qu'il n'y a pas au moins deux
projets et des affectations.

---

## 📋 Étapes restantes

### 7. Appliquer la matrice §6.3 sur les 18 contrôleurs ProjectAPI
- [ ] Remplacer les `[Authorize(Roles=…)]` commentés / `[AllowAnonymous]` par les
      rôles réels de la matrice, contrôleur par contrôleur
- [ ] Appeler `EnsureProjectAccessAsync` / `EnsureReservationAccessAsync` dans les
      handlers qui touchent une ressource projet
- [ ] Contrôleurs connus comme ouverts : `NotaryAppointmentsController`
      (`[AllowAnonymous]`), `ImmeubleController`, `ProjectController`
      (`[Authorize]` commenté)

### 8. Règles §6.4 spécifiques
- [ ] Un agent commercial **ne peut pas approuver sa propre réservation**
- [ ] Un `PROJECT_ADMIN` ne peut pas s'attribuer `GLOBAL_ADMIN`
- [ ] Un `PROJECT_ADMIN` ne peut pas supprimer un compte global ni toucher aux
      affectations d'un autre projet
- [ ] Un `TECHNICIAN` n'accède qu'aux réclamations SAV qui lui sont affectées
- [ ] Un notaire n'accède pas au CRM complet
- [ ] Rôle `BUYER` attribué automatiquement à la 1re réservation approuvée (§6.2)

### 9. Frontend
- [ ] Mettre `UserRole` sur les codes spec (actuellement `"Admin" | "Agent" |
      "Notaire" | "Acheteur"`, à passer en `GLOBAL_ADMIN` / `SALES_AGENT` / …)
- [ ] Mettre à jour la map de nav de `AdminLayout.tsx` et les `roles` des routes
      dans `App.tsx` (déjà en place mais avec les anciens libellés)
- [ ] Ajouter `TECHNICIAN` (accès Réclamations SAV) — absent du gating actuel
- [ ] Gérer `403 PROJECT_SCOPE_DENIED` dans `client.ts` / l'UI

### 10. Vérification et documentation
- [ ] Test live par rôle : chaque rôle atteint ce qu'il doit atteindre, et est
      refusé ailleurs (403 attendu, pas 200 ni 500)
- [ ] Test de périmètre : un agent affecté au projet A refusé sur le projet B
- [ ] Mettre à jour `docs/GAP_ANALYSIS.md` §1.4 (frontend repo) avec ce qui est
      réellement couvert et ce qui reste

---

## Notes utiles pour la reprise

**Comptes de test** — mot de passe commun : `Passw0rd!123`

Recréés sur SQLEXPRESS le 24/07/2026 et **revérifiés par login réel** (JWT
décodé). `UserName` = email, téléphones `060000000N` (le validateur impose
`^(06|07)\d{8}$`).

| Login | Rôle stocké | `roles` (réponse login) | `ClaimTypes.Role` (JWT) |
|---|---|---|---|
| yassine.aitmoussaa@gmail.com | `Admin` | `GLOBAL_ADMIN` | `Admin`, `GLOBAL_ADMIN` |
| sara.elfassi@samgroup.ma | `Agent` | `SALES_AGENT` | `Agent`, `SALES_AGENT` |
| youssef.bennani@samgroup.ma | `Agent` | `SALES_AGENT` | `Agent`, `SALES_AGENT` |
| amine.kabbaj@notaires.ma | `Notaire` | `NOTARY` | `Notaire`, `NOTARY` |
| laila.ouazzani@notaires.ma | `Notaire` | `NOTARY` | `Notaire`, `NOTARY` |
| karim.tazi@gmail.com | `Acheteur` | `BUYER` | `Acheteur`, `BUYER` |
| tech.sav@samgroup.ma | `TECHNICIAN` | `TECHNICIAN` | `TECHNICIAN` |

Le rôle **stocké** est volontairement le libellé legacy quand il en existe un :
c'est ce qui fait émettre les **deux** claims et garde compatibles les
contrôleurs pas encore migrés. `tech.sav` n'a que `TECHNICIAN` (aucun compte
legacy `Technicien` n'a jamais existé) — c'est donc le seul compte qui échouera
sur un `[Authorize(Roles="…")]` en libellé legacy, ce qui est correct.

⚠️ **Il n'y a toujours aucune donnée métier** : 0 projet, 0 immeuble, 0 bien,
0 `ProjectAssignments`. Les tests de périmètre (étape 10) restent impossibles
tant que ces données n'existent pas.

> Rappel §3 : `AddRolesToClaims` émet le rôle **stocké** + son code spec
> normalisé. Un compte stockant `Agent` obtient donc `Agent` **et**
> `SALES_AGENT` (compatible avec les `[Authorize(Roles="Agent")]` existants) ;
> un compte stockant `SALES_AGENT` n'obtient que `SALES_AGENT`. Pour recréer
> des comptes utilisables par les contrôleurs non encore migrés, stocker le
> **libellé legacy**.

**Environnement local**
- AuthenticationAPI : `http://localhost:48988` (`/health`)
- ProjectAPI : `http://localhost:48989` (`/health`)
- **Bases : `GPIA_Auth` et `GPIA_Project` sur `DESKTOP-1CEDKH6\SQLEXPRESS`**,
  authentification Windows (`Trusted_Connection=True`). Les deux
  `appsettings.json` ont été basculés depuis `(localdb)\MSSQLLocalDB`
  le 24/07/2026.
- ⚠️ **Migration = repartir de zéro.** L'instance LocalDB existe toujours mais
  ne contient plus ni `GPIA_Auth` ni `GPIA_Project` : les données décrites dans
  les sections 3 et 4 ci-dessus (comptes de test, 7 lignes `ProjectAssignments`,
  projets/immeubles/biens) **sont perdues**. Les bases SQLEXPRESS ont été créées
  par `dotnet ef database update` (migration `InitialCreate` des deux projets),
  puis seules les 12 lignes `AspNetRoles` ont été réinsérées
  (7 codes spec + 5 libellés legacy).
- Recréer les bases si besoin :
  ```
  dotnet ef database update --project src/<API>/src/Infrastructure/<API>.Infrastructure.csproj `
                            --startup-project src/<API>/src/Api/<API>.Api.csproj
  ```
- `sqlcmd` : toujours `-I` (QUOTED_IDENTIFIER ON, index filtré sur `AspNetRoles`)
  **et** `-f 65001` si le script `.sql` contient des accents — sinon sqlcmd lit
  le fichier en ANSI et stocke du mojibake (`Réservataire` → `RÃ©servataire`).
- Les `dotnet build` échouent avec `MSB3027` (fichier verrouillé) si l'API tourne
  → arrêter le process avant de rebuilder.
- `dotnet-ef` n'était pas installé : `dotnet tool install --global dotnet-ef`
  (v10.0.10, fonctionne avec le SDK 9.0.309 et EF Core 8.0.8 du projet).

**Pièges rencontrés dans ce projet**
- `RegisterCommand.Id` est un `Guid` **non nullable** : un client qui ne l'envoie
  pas transmet `Guid.Empty`, que Mapster recopie sur `User.Id` (clé string).
  Résultat : sur une base vierge, **seul le tout premier compte pouvait être
  créé**, tous les suivants échouaient en `500` (collision de clé primaire).
  Corrigé le 24/07/2026 dans `RegisterHandler` (id généré quand absent, id
  explicite toujours respecté). Symptôme trompeur : le 500 ne dit rien de la
  collision.
- `Units.ProjectId` pointe en réalité vers `Immeubles.Id`, pas `Projects.Id`
  (nommage trompeur, déjà noté dans `GAP_ANALYSIS.md` lot 2.2). Toute jointure
  vers un projet passe par `Unit → Immeuble → Project`.
- `Unit` est ambigu entre `ProjectAPI.Domain.Immeubles.Entities.Unit` et
  `MediatR.Unit` → toujours qualifier pleinement dans les LINQ.
- EF Core : pour insérer deux entités liées dans le même `SaveChangesAsync`,
  utiliser la **propriété de navigation**, pas seulement la FK scalaire.
