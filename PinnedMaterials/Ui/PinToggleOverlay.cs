using System;
using PinnedMaterials.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinnedMaterials.Ui;

// Lives on a material toggle; owns the small pin button in its top-right corner.
// The pin Image is added as the LAST child on purpose: MaterialSelector reads its toggle images by index.
public sealed class PinToggleOverlay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color PinnedColor = new Color(1f, 0.75f, 0.2f, 1f);
    private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color HiddenColor = new Color(1f, 1f, 1f, 0f);

    private static Sprite pinSprite;

    private Tag materialTag;
    private Image image;
    private ToolTip toolTip;
    private bool hovered;

    public void Init(Tag tag)
    {
        materialTag = tag;

        var go = new GameObject("PinButton", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.SetAsLastSibling();

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(18f, 18f);
        rect.anchoredPosition = new Vector2(-2f, -2f);

        image = go.AddComponent<Image>();
        image.sprite = GetPinSprite();
        image.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(OnPinClicked);

        toolTip = go.AddComponent<ToolTip>();
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        Refresh();
    }

    private void OnDisable()
    {
        hovered = false;
    }

    public void Refresh()
    {
        if (image == null)
        {
            return;
        }

        var pinned = PinStore.IsPinned(materialTag);
        image.color = pinned ? PinnedColor : hovered ? HoverColor : HiddenColor;
        if (toolTip != null)
        {
            toolTip.SetSimpleTooltip(pinned ? STRINGS.UI.UNPIN_TOOLTIP : STRINGS.UI.PIN_TOOLTIP);
        }
    }

    private void OnPinClicked()
    {
        PinStore.Toggle(materialTag);

        // Re-sort every visible selector (a building can have several material slots) and update the icons.
        foreach (var selector in FindObjectsByType<MaterialSelector>(FindObjectsSortMode.None))
        {
            if (!selector.isActiveAndEnabled || selector.ElementToggles.Count == 0)
            {
                continue;
            }

            try
            {
                selector.RefreshToggleContents();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PinnedMaterials] Failed to refresh selector: " + e);
            }
        }

        foreach (var overlay in FindObjectsByType<PinToggleOverlay>(FindObjectsSortMode.None))
        {
            overlay.Refresh();
        }
    }

    // Procedurally drawn white pin glyph (head + crossbar + needle), tinted through Image.color.
    private static Sprite GetPinSprite()
    {
        if (pinSprite != null)
        {
            return pinSprite;
        }

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - 15.5f;
                var head = dx * dx + (y - 22f) * (y - 22f) <= 7f * 7f;
                var bar = y >= 12 && y <= 14 && Mathf.Abs(dx) <= 9f;
                var needle = y <= 12 && Mathf.Abs(dx) <= 1.5f;
                texture.SetPixel(x, y, head || bar || needle ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        pinSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return pinSprite;
    }
}
