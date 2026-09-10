using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System;

public class InteractiveTooltipManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dimOverlay;
    public GameObject popupPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Button closeButton;

    [Header("Cone Visual Indicator")]
    public LineRenderer coneRenderer;
    public RectTransform popupTransform;

    [Header("Animation Settings")]
    public float typeSpeed = 0.015f;

    private Camera mainCamera;
    private Coroutine typingCoroutine;

    void Start()
    {
        try
        {
            mainCamera = Camera.main;
            ClosePopup();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(ClosePopup);
            }

            if (coneRenderer != null)
            {
                coneRenderer.positionCount = 2;
                coneRenderer.startWidth = 0.02f;
                coneRenderer.endWidth = 0.5f;
                coneRenderer.useWorldSpace = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[InteractiveTooltipManager] Exception in Start: {e.Message}\n{e.StackTrace}");
        }
    }

    void Update()
    {
        try
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            bool isCtrlPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            bool isLeftClickPressed = Mouse.current.leftButton.wasPressedThisFrame;

            if (isCtrlPressed && isLeftClickPressed)
            {
                ProcessClick();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[InteractiveTooltipManager] Exception in Update: {e.Message}\n{e.StackTrace}");
        }
    }

    private void ProcessClick()
    {
        try
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("[InteractiveTooltipManager] ERROR: No active camera tagged as 'MainCamera' found!");
                    return;
                }
            }

            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

            RefineryInteractiveElement clickedElement = null;
            Vector3 targetPoint = Vector3.zero;

            // 1. UI Raycast Check
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = mouseScreenPos
            };

            List<RaycastResult> raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, raycastResults);

            foreach (RaycastResult result in raycastResults)
            {
                clickedElement = result.gameObject.GetComponentInParent<RefineryInteractiveElement>();
                if (clickedElement != null)
                {
                    targetPoint = mainCamera.ScreenToWorldPoint(new Vector3(result.gameObject.transform.position.x, result.gameObject.transform.position.y, 2.0f));
                    break;
                }
            }

            // 2. 3D World Physics Raycast Check
            if (clickedElement == null)
            {
                Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    clickedElement = hit.collider.GetComponentInParent<RefineryInteractiveElement>();
                    if (clickedElement != null)
                    {
                        targetPoint = clickedElement.customTargetPoint != null ? clickedElement.customTargetPoint.position : hit.point;
                    }
                }
            }

            // 3. Open Tooltip Panel if Element Found
            if (clickedElement != null)
            {
                OpenPopup(clickedElement, targetPoint);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[InteractiveTooltipManager] Exception in ProcessClick: {e.Message}\n{e.StackTrace}");
        }
    }

    private void OpenPopup(RefineryInteractiveElement element, Vector3 startPoint)
    {
        try
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);

            dimOverlay.SetActive(true);
            popupPanel.SetActive(true);
            titleText.text = element.elementTitle;
            descriptionText.text = "";

            if (coneRenderer != null)
            {
                coneRenderer.enabled = true;
                coneRenderer.SetPosition(0, startPoint);

                Vector3 popupWorldPos = popupTransform.position;
                popupWorldPos.z = mainCamera.nearClipPlane + 2.0f;
                coneRenderer.SetPosition(1, mainCamera.ScreenToWorldPoint(popupWorldPos));
            }

            typingCoroutine = StartCoroutine(TypeText(element.elementExplanation));
        }
        catch (Exception e)
        {
            Debug.LogError($"[InteractiveTooltipManager] Exception in OpenPopup: {e.Message}\n{e.StackTrace}");
        }
    }

    private IEnumerator TypeText(string content)
    {
        int index = 0;
        while (index < content.Length)
        {
            try
            {
                descriptionText.text += content[index];
                index++;
            }
            catch (Exception e)
            {
                Debug.LogError($"[InteractiveTooltipManager] Exception in TypeText: {e.Message}\n{e.StackTrace}");
                yield break;
            }

            yield return new WaitForSeconds(typeSpeed);
        }
    }

    public void ClosePopup()
    {
        try
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);

            dimOverlay.SetActive(false);
            popupPanel.SetActive(false);
            if (coneRenderer != null) coneRenderer.enabled = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[InteractiveTooltipManager] Exception in ClosePopup: {e.Message}\n{e.StackTrace}");
        }
    }
}