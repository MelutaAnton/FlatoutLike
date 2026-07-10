---
name: hotspot-playerprefs-tests
description: EditMode tests that hit real PlayerPrefs share the same key store as the dev's in-editor game profile
type: project
---

`PlayerPrefsProfileStorageTests` (Assets/Development/Tests/EditMode) exercises the REAL `PlayerPrefs`, using the exact same keys the shipping game uses (`FlatOutLike.Profile.*`). Its SetUp/TearDown call `DeleteAllTestKeys()`.

**Why:** EditMode tests run in the same Unity project registry store as playing in the Editor — there is no isolation. So running these tests deletes any coins/selected-car/upgrade profile the developer accumulated while playtesting.

**How to apply:** When reviewing changes to Meta/profile persistence tests, flag any test that writes/deletes real `PlayerPrefs` keys without backing up and restoring pre-existing values. Suggested fix: snapshot the keys in SetUp and restore in TearDown, or inject a key prefix into the storage. This is a known trade-off the team accepted for black-box format testing — confirm it stays contained to this one fixture.
