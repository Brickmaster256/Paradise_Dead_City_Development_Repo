using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Army_Builder_Controller : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private Faction_Data_SO Current_Faction;

    [Header("UI References")]
    [SerializeField] private Transform Roster_Container;
    [SerializeField] private Transform Army_Container;
    [SerializeField] private GameObject Card_Prefab;
    [SerializeField] private GameObject Empty_Slot_Prefab;
    [SerializeField] private GameObject Background_Click_Area; // A full-screen invisible button behind everything

    [Header("Faction Info Panel")]
    [SerializeField] private TextMeshProUGUI Faction_Name_Text;
    [SerializeField] private TextMeshProUGUI Faction_Description_Text;

    [Header("Army Rules")]
    [SerializeField] private int Max_Points = 1200;
    [SerializeField] private int Max_Deathheads = 1;

    private List<Model_Stats_SO> Available_Units = new List<Model_Stats_SO>();
    private List<Model_Stats_SO> Selected_Army = new List<Model_Stats_SO>();
    private Army_Builder_Card Currently_Selected_Card;

    private void Start()
    {
        if (Current_Faction != null)
        {
            Faction_Name_Text.text = Current_Faction.Faction_Name;
            Faction_Description_Text.text = "Select a unit to view details.";

            if (Current_Faction.Chaff_Stats != null) Available_Units.Add(Current_Faction.Chaff_Stats);
            if (Current_Faction.Specialist_A_Stats != null) Available_Units.Add(Current_Faction.Specialist_A_Stats);
            if (Current_Faction.Specialist_B_Stats != null) Available_Units.Add(Current_Faction.Specialist_B_Stats);
            if (Current_Faction.Axillary_Stats != null) Available_Units.Add(Current_Faction.Axillary_Stats);
            if (Current_Faction.Death_Head_Stats != null) Available_Units.Add(Current_Faction.Death_Head_Stats);
        }

        // Setup the background click to deselect
        if (Background_Click_Area != null)
        {
            var btn = Background_Click_Area.GetComponent<UnityEngine.UI.Button>();
            if (btn != null) btn.onClick.AddListener(Deselect_All);
        }

        Refresh_UI();
    }

    public void Refresh_UI()
    {
        // Clear existing cards
        foreach (Transform child in Roster_Container) Destroy(child.gameObject);
        foreach (Transform child in Army_Container) Destroy(child.gameObject);

        // 1. Build Roster (Same as before)
        foreach (var stats in Available_Units)
        {
            GameObject cardGO = Instantiate(Card_Prefab, Roster_Container);
            RectTransform rt = cardGO.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
            rt.localScale = Vector3.one;

            var cardScript = cardGO.GetComponent<Army_Builder_Card>();
            cardScript.Setup(stats, true, On_Card_Left_Click, On_Card_Double_Click, Deselect_All);
        }

        // 2. Build Army Bar
        // First, check if the Deathhead is in the army
        Model_Stats_SO deathhead = Selected_Army.Find(x => x.Type == Model_Type.DeathHead);

        if (deathhead != null)
        {
            // Spawn the Deathhead card in slot 0
            GameObject cardGO = Instantiate(Card_Prefab, Army_Container);
            RectTransform rt = cardGO.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
            rt.localScale = Vector3.one;

            var cardScript = cardGO.GetComponent<Army_Builder_Card>();
            cardScript.Setup(deathhead, false, On_Card_Left_Click, On_Card_Double_Click, Deselect_All);
        }
        else
        {
            // Spawn the Empty Slot in slot 0
            GameObject emptyGO = Instantiate(Empty_Slot_Prefab, Army_Container);
            RectTransform rt = emptyGO.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
            rt.localScale = Vector3.one;
        }

        // Now spawn the rest of the units (ignoring the Deathhead since we handled it)
        foreach (var stats in Selected_Army)
        {
            if (stats.Type == Model_Type.DeathHead) continue; // Skip Deathhead

            GameObject cardGO = Instantiate(Card_Prefab, Army_Container);
            RectTransform rt = cardGO.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f);
            rt.localScale = Vector3.one;

            var cardScript = cardGO.GetComponent<Army_Builder_Card>();
            cardScript.Setup(stats, false, On_Card_Left_Click, On_Card_Double_Click, Deselect_All);
        }

        Update_Army_Composition_SO();
    }

    private void On_Card_Left_Click(Army_Builder_Card card)
    {
        Currently_Selected_Card = card;
        Show_Unit_Stats(card.Stats);
    }

    private void On_Card_Double_Click(Army_Builder_Card card)
    {
        if (card.Is_Roster_Card)
        {
            // Check limit rules before adding
            if (Can_Add_Unit(card.Stats))
            {
                Selected_Army.Add(card.Stats);
                Refresh_UI();
            }
            else
            {
                Debug.Log("Cannot add unit: Point limit reached or Max Deathheads exceeded.");
                // Optional: Flash a red text warning
            }
        }
        else
        {
            Selected_Army.Remove(card.Stats);
            Refresh_UI();
        }
    }

    private bool Can_Add_Unit(Model_Stats_SO unit)
    {
        // 1. Check Deathhead Limit
        if (unit.Type == Model_Type.DeathHead)
        {
            int deathheadCount = Selected_Army.FindAll(x => x.Type == Model_Type.DeathHead).Count;
            if (deathheadCount >= Max_Deathheads) return false;
        }

        // 2. Check Point Limit
        int currentPoints = Get_Current_Points();
        int newUnitCost = Get_Unit_Cost(unit);

        if (currentPoints + newUnitCost > Max_Points) return false;

        return true;
    }

    private void Deselect_All()
    {
        Currently_Selected_Card = null;
        Faction_Description_Text.text = "Select a unit to view details.";
    }

    private void Show_Unit_Stats(Model_Stats_SO stats)
    {
        int cost = Get_Unit_Cost(stats);

        string description = $"<b>{stats.Model_Name}</b> (Cost: {cost})\n\n" +
                             $"Health: {stats.Health}\n" +
                             $"Movement: {stats.Movement_Range}\n" +
                             $"Attack Skill: {stats.Attack_Skill}+\n" +
                             $"Damage: {stats.Attack_Damage}\n";

        if (stats.Ability_Data != null)
        {
            description += $"\n<b>Ability:</b> {stats.Ability_Data.Ability_Name}\n<i>{stats.Ability_Data.Description}</i>";
        }

        Faction_Description_Text.text = description;
    }

    private int Get_Current_Points()
    {
        int total = 0;
        foreach (var unit in Selected_Army) total += Get_Unit_Cost(unit);
        return total;
    }

    private int Get_Unit_Cost(Model_Stats_SO unit)
    {
        // This maps your specific costs based on type
        switch (unit.Type)
        {
            case Model_Type.Chaff: return 100;
            case Model_Type.Specialist_A: return 200;
            case Model_Type.Specialist_B: return 200;
            case Model_Type.Axillary: return 400;
            case Model_Type.DeathHead: return 300;
            default: return 100;
        }
    }

    private void Update_Army_Composition_SO()
    {
        if (Current_Faction == null || Current_Faction.Army_Composition == null) return;

        var roster = Current_Faction.Army_Composition.Army_Roster;
        roster.Clear();

        // Group up the selected units into counts
        foreach (var stats in Selected_Army)
        {
            var entry = roster.Find(x => x.Type == stats.Type);
            if (entry != null)
            {
                entry.Count++;
            }
            else
            {
                roster.Add(new Army_Composition_SO.Model_Entry { Type = stats.Type, Count = 1 });
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(Current_Faction.Army_Composition);
#endif
    }
}