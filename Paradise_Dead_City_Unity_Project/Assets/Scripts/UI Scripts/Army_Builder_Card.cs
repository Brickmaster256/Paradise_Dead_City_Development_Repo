using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System;

public class Army_Builder_Card : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image Portrait_Image;
    [SerializeField] private TextMeshProUGUI Name_Text;

    public Model_Stats_SO Stats { get; private set; }
    public bool Is_Roster_Card { get; private set; }

    private Action<Army_Builder_Card> On_Left_Click;
    private Action<Army_Builder_Card> On_Double_Click;
    private Action On_Right_Click;

    public void Setup(Model_Stats_SO stats, bool isRoster, Action<Army_Builder_Card> onLeftClick, Action<Army_Builder_Card> onDoubleClick, Action onRightClick)
    {
        Stats = stats;
        Is_Roster_Card = isRoster;
        On_Left_Click = onLeftClick;
        On_Double_Click = onDoubleClick;
        On_Right_Click = onRightClick;

        Name_Text.text = stats.Model_Name;
        // Portrait_Image.sprite = stats.Unit_Portrait; 
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            On_Right_Click?.Invoke();
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (eventData.clickCount == 2)
            {
                On_Double_Click?.Invoke(this);
            }
            else if (eventData.clickCount == 1)
            {
                On_Left_Click?.Invoke(this);
            }
        }
    }
}