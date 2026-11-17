using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public class Crypt_Blade_Prompt_UI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup CanvasGroup;
    [SerializeField] private Button Normal_Damage_Button;
    [SerializeField] private Button Roll_Dice_Button;
    [SerializeField] private TextMeshProUGUI Prompt_Text;

    [Header("Fading")]
    [SerializeField] private float FadeDuration = 0.15f;

    private Action<bool> On_Choice_Made;
    private Coroutine FadeCoroutine;

    private void Awake()
    {
        // Auto-grab the CanvasGroup if you forget to assign it
        if (CanvasGroup == null) CanvasGroup = GetComponent<CanvasGroup>();

        // Hook up the buttons
        if (Normal_Damage_Button != null)
        {
            Normal_Damage_Button.onClick.RemoveAllListeners();
            Normal_Damage_Button.onClick.AddListener(() => Make_Choice(false));
        }

        if (Roll_Dice_Button != null)
        {
            Roll_Dice_Button.onClick.RemoveAllListeners();
            Roll_Dice_Button.onClick.AddListener(() => Make_Choice(true));
        }

        // Hide the panel immediately when the game starts
        gameObject.SetActive(false);
    }

    public void Show_Prompt(Model_Standard_Behavior Attacker, Model_Standard_Behavior Defender, Action<bool> callback)
    {
        On_Choice_Made = callback;
        gameObject.SetActive(true);

        if (Prompt_Text != null)
            Prompt_Text.text = $"{Attacker.Stats.Model_Name} hit {Defender.Stats.Model_Name}!\nUse Crypt Blade to roll for higher damage?";

        if (FadeCoroutine != null) StopCoroutine(FadeCoroutine);
        FadeCoroutine = StartCoroutine(FadeCanvas(1f, true));
    }

    private void Make_Choice(bool rollDice)
    {
        if (FadeCoroutine != null) StopCoroutine(FadeCoroutine);
        FadeCoroutine = StartCoroutine(FadeAndHide(rollDice));
    }

    private IEnumerator FadeAndHide(bool rollDice)
    {
        // Wait for the fade out to finish
        yield return StartCoroutine(FadeCanvas(0f, false));

        // Hide the panel and send the choice back to the Combat Manager
        gameObject.SetActive(false);
        On_Choice_Made?.Invoke(rollDice);
    }

    private IEnumerator FadeCanvas(float targetAlpha, bool blockRaycasts)
    {
        if (CanvasGroup == null) yield break;

        CanvasGroup.blocksRaycasts = blockRaycasts;
        CanvasGroup.interactable = blockRaycasts;

        float startAlpha = CanvasGroup.alpha;
        float time = 0f;

        while (time < FadeDuration)
        {
            time += Time.unscaledDeltaTime;
            CanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / FadeDuration);
            yield return null;
        }

        CanvasGroup.alpha = targetAlpha;
    }
}