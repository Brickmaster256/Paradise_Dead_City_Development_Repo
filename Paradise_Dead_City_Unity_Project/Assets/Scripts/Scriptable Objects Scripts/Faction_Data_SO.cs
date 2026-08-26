using UnityEngine;

[CreateAssetMenu(fileName = "New Faction", menuName = "Scriptable Objects/Faction Data")]
public class Faction_Data_SO : ScriptableObject
{
    public string Faction_Name;

    [Header("Model Prefabs")]
    public GameObject Chaff_Prefab;
    public GameObject Specialist_A_Prefab;
    public GameObject Specialist_B_Prefab;
    public GameObject Axillary_Prefab;
    public GameObject Death_Head_Prefab;

    [Header("Model Stats")]
    public Model_Stats_SO Chaff_Stats;
    public Model_Stats_SO Specialist_A_Stats;
    public Model_Stats_SO Specialist_B_Stats;
    public Model_Stats_SO Axillary_Stats;
    public Model_Stats_SO Death_Head_Stats;

    [Header("Model Materials")]
    public Material[] Team_Materials;

    [Header("Army Composition")]
    public Army_Composition_SO Army_Composition;

    [Header("UI Icons")]
    public Sprite Faction_Icon;
    public Sprite Chaff_Icon;
    public Sprite Specialist_A_Icon;
    public Sprite Specialist_B_Icon;
    public Sprite Axillary_Icon;
    public Sprite Death_Head_Icon;

    // Helper method to get the prefab based on the model type
    public GameObject Get_Prefab_By_Type(Model_Type Type)
    {
        return Type switch
        {
            Model_Type.Chaff => Chaff_Prefab,
            Model_Type.Specialist_A => Specialist_A_Prefab,
            Model_Type.Specialist_B => Specialist_B_Prefab,
            Model_Type.Axillary => Axillary_Prefab,
            Model_Type.DeathHead => Death_Head_Prefab,
            _ => null
        };
    }

    // Helper method to get stats based on the model type
    public Model_Stats_SO Get_Stats_By_Type(Model_Type Type)
    {
        return Type switch
        {
            Model_Type.Chaff => Chaff_Stats,
            Model_Type.Specialist_A => Specialist_A_Stats,
            Model_Type.Specialist_B => Specialist_B_Stats,
            Model_Type.Axillary => Axillary_Stats,
            Model_Type.DeathHead => Death_Head_Stats,
            _ => null
        };
    }

    // Get the faction icon
    public Sprite Get_Faction_Icon()
    {
        return Faction_Icon;
    }

    // Get model icon based on type
    public Sprite Get_Model_Icon(Model_Type Type)
    {
        return Type switch
        {
            Model_Type.Chaff => Chaff_Icon,
            Model_Type.Specialist_A => Specialist_A_Icon,
            Model_Type.Specialist_B => Specialist_B_Icon,
            Model_Type.Axillary => Axillary_Icon,
            Model_Type.DeathHead => Death_Head_Icon,
            _ => null
        };
    }
}
