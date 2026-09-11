using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class InteractiveTooltipManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dimOverlay;
    public GameObject popupPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Button closeButton;

    [Header("Animation Settings")]
    public float typeSpeed = 0.015f;

    [Header("Guided Tour Demo")]
    public Button tourButton;
    public Button nextButton;
    public Button prevButton;
    public List<RefineryInteractiveElement> tourElements;

    private bool isTourActive = false;
    private int currentTourIndex = 0;

    private Camera mainCamera;
    private Coroutine typingCoroutine;

    // --- COLOR TRACKING DICTIONARIES ---
    private Dictionary<TMP_Text, Color> originalTMPColors = new Dictionary<TMP_Text, Color>();
    private Dictionary<Graphic, Color> originalGraphicColors = new Dictionary<Graphic, Color>();
    private Dictionary<SpriteRenderer, Color> originalSpriteColors = new Dictionary<SpriteRenderer, Color>();

    void Start()
    {
        mainCamera = Camera.main;

        if (closeButton != null) closeButton.onClick.AddListener(ClosePopup);
        if (tourButton != null) tourButton.onClick.AddListener(StartGuidedTour);
        if (nextButton != null) nextButton.onClick.AddListener(NextTourElement);
        if (prevButton != null) prevButton.onClick.AddListener(PreviousTourElement);

        ClosePopup();
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        bool isCtrlPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
        bool isLeftClickPressed = Mouse.current.leftButton.wasPressedThisFrame;

        if (isCtrlPressed && isLeftClickPressed && !isTourActive)
        {
            ProcessClick();
        }
    }

    private void ProcessClick()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        RefineryInteractiveElement clickedElement = null;

        // 1. Check UI Canvas clicks first
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = mouseScreenPos };
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        foreach (RaycastResult result in raycastResults)
        {
            clickedElement = result.gameObject.GetComponentInParent<RefineryInteractiveElement>();
            if (clickedElement != null) break;
        }

        // 2. Check 2D/3D World clicks if no UI was hit
        if (clickedElement == null)
        {
            Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                clickedElement = hit.collider.GetComponentInParent<RefineryInteractiveElement>();
            }
        }

        if (clickedElement != null)
        {
            OpenPopup(clickedElement);
        }
    }

    private void OpenPopup(RefineryInteractiveElement element)
    {
        Time.timeScale = 0f;

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        // Brighten the colors on the clicked object
        ApplyHighlight(element);

        dimOverlay.SetActive(true);
        popupPanel.SetActive(true);
        titleText.text = element.elementTitle;
        descriptionText.text = "";

        if (nextButton != null) nextButton.gameObject.SetActive(isTourActive);
        if (prevButton != null)
        {
            prevButton.gameObject.SetActive(isTourActive);
            prevButton.interactable = (currentTourIndex > 0);
        }

        typingCoroutine = StartCoroutine(TypeText(element.elementExplanation));
    }

    private void ApplyHighlight(RefineryInteractiveElement element)
    {
        ClearHighlight(); // Make sure previous element is reset

        // 1. Brighten TextMeshPro Text
        TMP_Text[] tmpTexts = element.GetComponentsInChildren<TMP_Text>();
        foreach (TMP_Text tmp in tmpTexts)
        {
            originalTMPColors[tmp] = tmp.color;
            tmp.color = new Color(tmp.color.r * 2.5f, tmp.color.g * 2.5f, tmp.color.b * 2.5f, tmp.color.a);
        }

        // 2. Brighten Standard UI Graphics (Images, Backgrounds)
        Graphic[] graphics = element.GetComponentsInChildren<Graphic>();
        foreach (Graphic g in graphics)
        {
            if (g is TMP_Text) continue; // Skip TMP since we handled it above

            originalGraphicColors[g] = g.color;
            g.color = new Color(g.color.r * 2.5f, g.color.g * 2.5f, g.color.b * 2.5f, g.color.a);
        }

        // 3. Brighten standard 2D Sprites (if you are using regular GameObjects instead of UI)
        SpriteRenderer[] sprites = element.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in sprites)
        {
            originalSpriteColors[sr] = sr.color;
            sr.color = new Color(sr.color.r * 2.5f, sr.color.g * 2.5f, sr.color.b * 2.5f, sr.color.a);
        }
    }

    private void ClearHighlight()
    {
        // Restore TextMeshPro colors
        foreach (KeyValuePair<TMP_Text, Color> entry in originalTMPColors)
        {
            if (entry.Key != null) entry.Key.color = entry.Value;
        }
        originalTMPColors.Clear();

        // Restore UI Graphic colors
        foreach (KeyValuePair<Graphic, Color> entry in originalGraphicColors)
        {
            if (entry.Key != null) entry.Key.color = entry.Value;
        }
        originalGraphicColors.Clear();

        // Restore Sprite colors
        foreach (KeyValuePair<SpriteRenderer, Color> entry in originalSpriteColors)
        {
            if (entry.Key != null) entry.Key.color = entry.Value;
        }
        originalSpriteColors.Clear();
    }

    public void ClosePopup()
    {
        isTourActive = false;
        Time.timeScale = 1f;

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        // Put the clicked object's colors back to normal
        ClearHighlight();

        dimOverlay.SetActive(false);
        popupPanel.SetActive(false);

        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (prevButton != null) prevButton.gameObject.SetActive(false);
    }

    // --- Guided Tour Functions ---
    public void StartGuidedTour()
    {
        if (tourElements == null || tourElements.Count == 0) return;
        isTourActive = true;
        currentTourIndex = 0;
        OpenPopup(tourElements[currentTourIndex]);
    }

    public void NextTourElement()
    {
        if (currentTourIndex < tourElements.Count - 1)
        {
            currentTourIndex++;
            OpenPopup(tourElements[currentTourIndex]);
        }
        else
        {
            ClosePopup();
        }
    }

    public void PreviousTourElement()
    {
        if (currentTourIndex > 0)
        {
            currentTourIndex--;
            OpenPopup(tourElements[currentTourIndex]);
        }
    }

    private IEnumerator TypeText(string content)
    {
        int index = 0;
        while (index < content.Length)
        {
            descriptionText.text += content[index];
            index++;
            yield return new WaitForSecondsRealtime(typeSpeed);
        }
    }
}