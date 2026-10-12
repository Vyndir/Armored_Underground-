using System;
using System.Collections.Generic;
using UnityEngine;

namespace AU
{
    [Serializable]
    public class EventResult { public string id; public int bestPos; public float bestTime; }

    /// <summary>Career save: credits, rep, owned parts, current loadout and race bests.</summary>
    [Serializable]
    public class SaveData
    {
        const string Key = "au-save-v1";
        public int credits = 2000;
        public int rep;
        public List<string> owned = new();
        public Loadout loadout = new();
        public List<EventResult> results = new();

        public static SaveData Load()
        {
            SaveData s = null;
            try { var json = PlayerPrefs.GetString(Key, ""); if (!string.IsNullOrEmpty(json)) s = JsonUtility.FromJson<SaveData>(json); }
            catch { s = null; }
            s ??= new SaveData();
            foreach (Slot slot in Parts.Slots)
            {
                var id = new Loadout()[slot];
                if (!s.owned.Contains(id)) s.owned.Add(id);
            }
            return s;
        }

        public void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public static SaveData Reset() { PlayerPrefs.DeleteKey(Key); return Load(); }
        public EventResult Result(string id) => results.Find(r => r.id == id);
        public bool Owns(string id) => owned.Contains(id);
    }
}
