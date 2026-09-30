using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Army Composition", menuName = "Scriptable Objects/Army Composition")]
public class Army_Composition_SO : ScriptableObject
{
    // ============================================================
    // SERIALIZED FIELDS
    // ============================================================

    public string Composition_Name;

    [System.Serializable]
    public class Model_Entry
    {
        public Model_Type Type;
        public int Count;
    }

    [Header("Army Roster")]
    public List<Model_Entry> Army_Roster = new List<Model_Entry>();

    [Header("Spawn Tile Prefab")]
    public GameObject Spawn_Tile_Prefab;


    // ============================================================
    // PUBLIC QUERIES
    // ============================================================

    public bool Has_Models_Left()
    {
        foreach (Model_Entry Entry in Army_Roster)
        {
            if (Entry.Count > 0)
                return true;
        }
        return false;
    }

    public int Get_Remaining_Count()
    {
        int Total = 0;
        foreach (Model_Entry Entry in Army_Roster)
            Total += Entry.Count;
        return Total;
    }


    // ============================================================
    // MODEL SELECTION
    // ============================================================

    /// <summary>
    /// Picks a random model type using the roster's remaining counts as
    /// weights, decrements that entry's count, and returns the chosen type.
    /// Returns null (and logs an error) if the roster has no models left.
    /// Callers should check Has_Models_Left() before calling.
    /// </summary>
    public bool Try_Get_Random_Model_Type(out Model_Type Selected_Type)
    {
        int Remaining = Get_Remaining_Count();

        if (Remaining <= 0)
        {
            Selected_Type = default;
            return false;
        }

        // Weighted pick in a single pass. We roll a number in [0, Remaining)
        // and walk the roster until the cumulative weight exceeds the roll.
        int Roll = Random.Range(0, Remaining);

        foreach (Model_Entry Entry in Army_Roster)
        {
            if (Entry.Count <= 0)
                continue;

            if (Roll < Entry.Count)
            {
                Entry.Count--;
                Selected_Type = Entry.Type;
                return true;
            }

            Roll -= Entry.Count;
        }

        // Unreachable if Remaining and the roster are consistent.
        Selected_Type = default;
        Debug.LogError($"[{name}] Try_Get_Random_Model_Type: roster sum mismatch (Remaining={Remaining}).");
        return false;
    }
}