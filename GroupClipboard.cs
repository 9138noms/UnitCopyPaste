using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;

namespace UnitCopyPaste
{
    public class CopiedUnitData
    {
        public string Type;
        public string Faction;
        public Vector3 RelativeOffset;
        public Quaternion Rotation;
        public SavedUnit SourceSaved;
    }

    public static class GroupClipboard
    {
        public static readonly List<CopiedUnitData> Items = new List<CopiedUnitData>();
        public static bool HasData => Items.Count > 0;

        public static void Clear()
        {
            Items.Clear();
        }
    }
}
