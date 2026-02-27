using System;
using System.Collections.Generic;
using BepInEx.Logging;
using NuclearOption.MissionEditorScripts;
using NuclearOption.SavedMission;
using UnityEngine;

namespace UnitCopyPaste
{
    public static class GroupCopyPaste
    {
        private static ManualLogSource Log => Plugin.Log;

        private static int _spawnCounter = 0;

        public static void CopySelectedGroup()
        {
            var selection = SceneSingleton<UnitSelection>.i;
            if (selection == null)
            {
                Log.LogWarning("[UCP] UnitSelection not found");
                return;
            }

            var details = selection.SelectionDetails;
            if (details == null)
            {
                Log.LogWarning("[UCP] Nothing selected");
                return;
            }

            var unitDetails = new List<UnitSelectionDetails>();

            if (details is MultiSelectSelectionDetails multi)
            {
                foreach (var item in multi.Items)
                {
                    if (item is UnitSelectionDetails usd)
                        unitDetails.Add(usd);
                }
            }
            else if (details is UnitSelectionDetails single)
            {
                unitDetails.Add(single);
            }

            if (unitDetails.Count == 0)
            {
                Log.LogWarning("[UCP] No units in selection");
                return;
            }

            // Calculate group center using GlobalPosition (not local transform)
            Vector3 center = Vector3.zero;
            foreach (var ud in unitDetails)
            {
                center += ud.SavedUnit.globalPosition.AsVector3();
            }
            center /= unitDetails.Count;

            // Store clipboard data with global position offsets
            GroupClipboard.Clear();
            foreach (var ud in unitDetails)
            {
                GroupClipboard.Items.Add(new CopiedUnitData
                {
                    Type = ud.SavedUnit.type,
                    Faction = ud.SavedUnit.faction,
                    RelativeOffset = ud.SavedUnit.globalPosition.AsVector3() - center,
                    Rotation = ud.SavedUnit.rotation,
                    SourceSaved = ud.SavedUnit
                });
            }

            Log.LogInfo($"[UCP] Copied {GroupClipboard.Items.Count} units");
        }

        public static void PasteGroupAtCursor()
        {
            Log.LogInfo($"[UCP] PasteGroupAtCursor called, hasData={GroupClipboard.HasData}");

            if (!GroupClipboard.HasData)
            {
                Log.LogWarning("[UCP] Clipboard empty");
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                Log.LogWarning("[UCP] Camera.main is null");
                return;
            }

            // Raycast to find paste position
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Log.LogInfo($"[UCP] Raycast from {ray.origin} dir {ray.direction}");

            // Convert local raycast hit to global coordinates
            // Floating origin: globalPos = localPos + originOffset
            // Compute offset from a clipboard source unit that still exists
            Vector3 originOffset = Vector3.zero;
            foreach (var item in GroupClipboard.Items)
            {
                if (item.SourceSaved?.Unit != null)
                {
                    Vector3 localPos = item.SourceSaved.Unit.transform.position;
                    Vector3 globalPos = item.SourceSaved.globalPosition.AsVector3();
                    originOffset = globalPos - localPos;
                    break;
                }
            }

            Vector3 hitPoint;
            if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
            {
                hitPoint = hit.point;
            }
            else
            {
                // No terrain hit — try water plane for over-sea pasting
                Plane waterPlane = new Plane(Vector3.up, new Vector3(0, Datum.LocalSeaY, 0));
                if (waterPlane.Raycast(ray, out float enter))
                {
                    hitPoint = ray.GetPoint(enter);
                    Log.LogInfo($"[UCP] No terrain, using water plane at {hitPoint}");
                }
                else
                {
                    Log.LogWarning("[UCP] Raycast missed — no terrain or water under cursor");
                    return;
                }
            }

            Vector3 pasteCenter = hitPoint + originOffset;
            Log.LogInfo($"[UCP] Hit local={hitPoint} offset={originOffset} global={pasteCenter}");
            SpawnGroup(pasteCenter, originOffset);
        }

        public static void DuplicateInPlace()
        {
            CopySelectedGroup();
            if (!GroupClipboard.HasData) return;

            var first = GroupClipboard.Items[0];
            if (first.SourceSaved == null || first.SourceSaved.Unit == null)
            {
                Log.LogWarning("[UCP] Source unit no longer exists");
                return;
            }

            Vector3 localPos = first.SourceSaved.Unit.transform.position;
            Vector3 globalPos = first.SourceSaved.globalPosition.AsVector3();
            Vector3 originOffset = globalPos - localPos;

            Vector3 originalCenter = globalPos - first.RelativeOffset;
            Vector3 offset = new Vector3(10f, 0f, 10f);
            SpawnGroup(originalCenter + offset, originOffset);
        }

        private static void SpawnGroup(Vector3 pasteCenter, Vector3 originOffset)
        {
            Log.LogInfo("[UCP] SpawnGroup entered");

            var editor = SceneSingleton<MissionEditor>.i;
            var spawner = NetworkSceneSingleton<Spawner>.i;
            Log.LogInfo($"[UCP] editor={editor != null} spawner={spawner != null}");

            if (editor == null || spawner == null)
            {
                Log.LogWarning("[UCP] Editor or Spawner not available");
                return;
            }

            var mission = MissionManager.CurrentMission;
            Log.LogInfo($"[UCP] mission={mission != null}");

            if (mission == null)
            {
                Log.LogWarning("[UCP] No current mission");
                return;
            }

            var spawnedUnits = new List<Unit>();

            foreach (var data in GroupClipboard.Items)
            {
                try
                {
                    Log.LogInfo($"[UCP] Spawning type={data.Type} faction={data.Faction}");

                    UnitDefinition definition = null;
                    if (data.SourceSaved != null && data.SourceSaved.Unit != null)
                    {
                        definition = data.SourceSaved.Unit.definition;
                        Log.LogInfo($"[UCP] Got definition from source: {definition?.name}");
                    }

                    if (definition == null)
                    {
                        Log.LogInfo("[UCP] Trying Encyclopedia lookup...");
                        if (!Encyclopedia.Lookup.TryGetValue(data.Type, out definition))
                        {
                            Log.LogWarning($"[UCP] Unknown unit type: {data.Type}, skipping");
                            continue;
                        }
                    }

                    Log.LogInfo($"[UCP] Looking up faction: {data.Faction}");
                    FactionHQ factionHQ = null;
                    // Try name lookup first (reliable for named factions)
                    if (!string.IsNullOrEmpty(data.Faction))
                    {
                        factionHQ = FactionRegistry.HqFromName(data.Faction);
                    }
                    // Fallback: get from source unit directly (for neutral/scenery with no faction name)
                    if (factionHQ == null && data.SourceSaved?.Unit != null)
                    {
                        try { factionHQ = data.SourceSaved.Unit.NetworkHQ; }
                        catch { Log.LogWarning("[UCP] Failed to read NetworkHQ from source unit"); }
                    }
                    Log.LogInfo($"[UCP] FactionHQ={factionHQ?.name ?? "null"}");

                    Vector3 worldPos = pasteCenter + data.RelativeOffset;

                    // Ships: raise 35m above so they drop onto water surface
                    if (definition is ShipDefinition)
                    {
                        worldPos.y += 35f;
                        Log.LogInfo("[UCP] Ship detected, raised +35m for water drop");
                    }

                    GlobalPosition gpos = new GlobalPosition(worldPos);

                    string uniqueName = definition.unitPrefab.name + "_UCP" +
                        (++_spawnCounter) + "_" + DateTime.Now.Ticks.ToString("X");

                    Log.LogInfo($"[UCP] Calling SpawnFromUnitDefinitionInEditor name={uniqueName}");
                    Unit unit = spawner.SpawnFromUnitDefinitionInEditor(
                        definition, gpos, data.Rotation, factionHQ, uniqueName);

                    if (unit == null)
                    {
                        Log.LogWarning($"[UCP] Failed to spawn {data.Type}");
                        continue;
                    }

                    Log.LogInfo("[UCP] Unit spawned, registering...");
                    Physics.SyncTransforms();

                    SavedUnit savedUnit = editor.RegisterNewUnit(unit, uniqueName);
                    Log.LogInfo($"[UCP] Registered, savedUnit={savedUnit != null}");

                    if (data.SourceSaved != null && savedUnit != null)
                    {
                        NuclearOption.MissionEditorScripts.UnitCopyPaste.CopyPaste(
                            mission, data.SourceSaved, unit, savedUnit);
                        // Re-set position after CopyPaste to prevent any side effects
                        savedUnit.globalPosition = gpos;
                        savedUnit.rotation = data.Rotation;

                        // Apply aircraft livery visually (CopyPaste only sets SavedAircraft data)
                        if (unit is Aircraft aircraft
                            && data.SourceSaved is SavedAircraft srcAc
                            && savedUnit is SavedAircraft dstAc)
                        {
                            dstAc.liveryKey = srcAc.liveryKey;
                            aircraft.SetLiveryKey(dstAc.liveryKey);
                            Log.LogInfo($"[UCP] Livery applied: {dstAc.liveryKey}");
                        }

                        Log.LogInfo($"[UCP] Properties copied, pos re-set to {worldPos}");
                    }

                    // Clamp position to terrain — same as game's ClampPosition
                    // Uses layer 64 (terrain only) so structures aren't detected
                    ClampUnitToTerrain(unit, savedUnit, definition, originOffset);

                    spawnedUnits.Add(unit);
                }
                catch (Exception ex)
                {
                    Log.LogError($"[UCP] Error spawning {data.Type}: {ex}");
                }
            }

            if (spawnedUnits.Count > 0)
            {
                var selectables = new List<IEditorSelectable>();
                foreach (var u in spawnedUnits)
                    selectables.Add(u);

                try
                {
                    SceneSingleton<UnitSelection>.i?.ReplaceSelection(selectables);
                }
                catch (Exception ex)
                {
                    Log.LogError($"[UCP] Error selecting: {ex.Message}");
                }
            }

            Log.LogInfo($"[UCP] Pasted {spawnedUnits.Count} units");
        }

        /// <summary>
        /// Replicates the game's UnitSelectionDetails.ClampPosition logic:
        /// Raycast with layer 64 (terrain only, ignores structures/units),
        /// then clamp Y using definition.spawnOffset, minEditorHeight, maxEditorHeight.
        /// </summary>
        private static void ClampUnitToTerrain(Unit unit, SavedUnit savedUnit,
            UnitDefinition definition, Vector3 originOffset)
        {
            if (unit == null || savedUnit == null || definition == null) return;

            Vector3 localPos = unit.transform.position;

            // FindHighestTerrainPoint: raycast down with layer 64, exclude Unit colliders
            const int terrainLayer = 64;
            RaycastHit[] hits = Physics.RaycastAll(
                localPos + Vector3.up * 10000f, Vector3.down, 20000f, terrainLayer);

            float highestY = float.MinValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.GetComponentInParent<Unit>() == null
                    && hits[i].point.y > highestY)
                {
                    highestY = hits[i].point.y;
                }
            }

            if (highestY <= float.MinValue) return;

            // ClampPosition formula
            float terrainY = highestY + definition.spawnOffset.y;
            float minY = terrainY + definition.minEditorHeight;
            float maxY = terrainY + definition.maxEditorHeight;
            float clampedY = Mathf.Clamp(localPos.y, minY, maxY);

            if (Mathf.Abs(clampedY - localPos.y) > 0.01f)
            {
                unit.transform.position = new Vector3(localPos.x, clampedY, localPos.z);
                Physics.SyncTransforms();

                // Update saved global position
                Vector3 globalVec = savedUnit.globalPosition.AsVector3();
                globalVec.y = clampedY + originOffset.y;
                savedUnit.globalPosition = new GlobalPosition(globalVec);

                Log.LogInfo($"[UCP] Terrain clamped localY: {localPos.y:F1} -> {clampedY:F1}");
            }
        }
    }
}
