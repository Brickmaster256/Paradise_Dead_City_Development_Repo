using UnityEngine;

[CreateAssetMenu(fileName = "New Status Definition", menuName = "Scriptable Objects/Status Definition")]
public class Status_Definition_SO : ScriptableObject
{
    [Header("Identity")]
    public Status_Type Type;
    public string Display_Name = "Status";

    [TextArea(2, 4)]
    public string Description = "";

    [Header("Visuals")]
    [Tooltip("Optional. If null, the placeholder circle sprite on the icon " +
             "prefab is kept and tinted with Fallback_Color. If assigned, " +
             "the sprite is swapped in.")]
    public Sprite Icon;

    [Tooltip("Color used for the placeholder tint, and for the icon sprite " +
             "when Tint_Icon is on.")]
    public Color Fallback_Color = Color.white;

    [Tooltip("If true, the icon sprite is multiplied by Fallback_Color. " +
             "If false, the sprite is shown as-is and Fallback_Color is ignored.")]
    public bool Tint_Icon = false;
}