# Workflow — Catalogue referentials (quartiers, features, property types)

**Status: validated end to end.**

## Business purpose

Admins maintain the referentials the catalogue is built from: **quartiers**
(name, description, optional images, and a list of **features** — title,
description, optional image) and **property types** (photo, 3D tour, minimum and
maximum area, bedrooms, bathrooms, shower rooms, parking), then link a project to
one quartier and one or many property types.

## Validated

Legend: ✅ validated end to end (Playwright UI test against the running stack, state re-read from the API/DB) · ⚠️ fixed during validation (fix logged in `docs/fixes/`) then validated · ❌ not validated (reason given).
Suite: `realestateFront/tests/` — run on 2026-09-14 against Azure SQL `GPIA_Project` (S2).

| Step | Result | Evidence (test) |
|---|---|---|
| Quartier create / edit / delete with optional image (uploaded to storage) | ⚠️ fixed (upload field, list without description) | `district_crud_with_name_description_optional_image`, `project_admin_can_crud_project_district_property_type` |
| Quartier features: add / edit / delete, title required, admins only | ⚠️ fixed (feature entity + endpoints + UI did not exist) | `district_supports_feature_list` |
| Images optional (quartier and feature) | ✅ | `district_image_and_feature_image_are_optional` |
| Property type: every field persisted, photo, 3D link | ⚠️ fixed (photo not editable) | `property_type_persists_all_fields` |
| Minimum area greater than maximum refused (UI and API, create and update) | ⚠️ fixed (creation accepted it) | `property_type_rejects_min_area_greater_than_max` |
| Project linked to one or many property types, unlink | ⚠️ fixed (no linking from the form, no unlink) | `project_can_link_one_or_many_property_types` |
| UI reloads the list after each mutation | ✅ | `ui_reloads_data_after_each_mutation` |
