using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum Tooltip_Anchor_Corner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center
}

public enum Tooltip_Pivot_Point
{
    BottomLeft,
    BottomRight,
    TopLeft,
    TopRight,
    Center
}

public class Status_Tooltip_UI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image Background_Image;
    [SerializeField] private TextMeshProUGUI Name_Text;
    [SerializeField] private TextMeshProUGUI Description_Text;

    [Header("Background")]
    [SerializeField] private Sprite Background_Sprite;
    [SerializeField] private bool Background_Sprite_Is_Sliced = false;
    [SerializeField] private Color Background_Color = new Color(0.12f, 0.12f, 0.12f, 0.92f);

    [Header("Placement")]
    [SerializeField] private Tooltip_Anchor_Corner Anchor_To = Tooltip_Anchor_Corner.TopRight;
    [SerializeField] private Tooltip_Pivot_Point Tooltip_Pivot = Tooltip_Pivot_Point.BottomLeft;
    [SerializeField] private float Horizontal_Offset = 8f;
    [SerializeField] private float Vertical_Offset = 6f;

    private RectTransform RT;

    private void Awake()
    {
        RT = (RectTransform)transform;

        Apply_Pivot();

        if (Background_Image != null)
        {
            Background_Image.sprite = Background_Sprite;
            Background_Image.color = Background_Color;
            Background_Image.type = (Background_Sprite != null && Background_Sprite_Is_Sliced)
                ? Image.Type.Sliced
                : Image.Type.Simple;
        }

        gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Setting RectTransform.pivot triggers OnRectTransformDimensionsChange,
        // which uses SendMessage internally — Unity forbids that during
        // OnValidate. Deferring the write by one editor tick moves it outside
        // the protected window and lets the pivot update in the Scene view
        // as soon as the enum changes.
        UnityEditor.EditorApplication.delayCall += Apply_Pivot_Editor;
    }

    private void Apply_Pivot_Editor()
    {
        UnityEditor.EditorApplication.delayCall -= Apply_Pivot_Editor;

        // Guard against the component being destroyed between OnValidate
        // and the deferred call.
        if (this == null) return;

        if (RT == null)
            RT = (RectTransform)transform;

        if (RT != null)
            RT.pivot = Get_Pivot_Vector();
    }
#endif

    public void Show(Status_Definition_SO Definition, RectTransform Anchor)
    {
        if (Definition == null || Anchor == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);

        if (Name_Text != null)
            Name_Text.text = Definition.Display_Name;

        if (Description_Text != null)
            Description_Text.text = Definition.Description;

        if (Background_Image != null)
            Background_Image.color = Background_Color;

        LayoutRebuilder.ForceRebuildLayoutImmediate(RT);

        Position_Relative_To_Icon(Anchor);
    }

    public void Hide()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void Apply_Pivot()
    {
        if (RT != null)
            RT.pivot = Get_Pivot_Vector();
    }

    private Vector2 Get_Pivot_Vector()
    {
        switch (Tooltip_Pivot)
        {
            case Tooltip_Pivot_Point.BottomLeft: return new Vector2(0f, 0f);
            case Tooltip_Pivot_Point.BottomRight: return new Vector2(1f, 0f);
            case Tooltip_Pivot_Point.TopLeft: return new Vector2(0f, 1f);
            case Tooltip_Pivot_Point.TopRight: return new Vector2(1f, 1f);
            case Tooltip_Pivot_Point.Center: return new Vector2(0.5f, 0.5f);
        }
        return new Vector2(0f, 0f);
    }

    private Vector3 Get_Icon_Corner(RectTransform Anchor)
    {
        Vector3[] Corners = new Vector3[4];
        Anchor.GetWorldCorners(Corners);

        switch (Anchor_To)
        {
            case Tooltip_Anchor_Corner.BottomLeft: return Corners[0];
            case Tooltip_Anchor_Corner.TopLeft: return Corners[1];
            case Tooltip_Anchor_Corner.TopRight: return Corners[2];
            case Tooltip_Anchor_Corner.BottomRight: return Corners[3];
            case Tooltip_Anchor_Corner.Center:
                return (Corners[0] + Corners[2]) * 0.5f;
        }
        return Corners[2];
    }

    private void Position_Relative_To_Icon(RectTransform Anchor)
    {
        Canvas Canvas = GetComponentInParent<Canvas>();
        Camera Cam = (Canvas == null || Canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : Canvas.worldCamera;

        Vector3 Anchor_World = Get_Icon_Corner(Anchor);
        Vector2 Screen_Point = RectTransformUtility.WorldToScreenPoint(Cam, Anchor_World);

        RectTransform Parent_RT = RT.parent as RectTransform;
        if (Parent_RT == null) return;

        Vector2 Local_Point;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Parent_RT, Screen_Point, Cam, out Local_Point))
        {
            RT.anchoredPosition = Local_Point + new Vector2(Horizontal_Offset, Vertical_Offset);
        }
    }
}