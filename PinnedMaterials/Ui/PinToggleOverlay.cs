using System;
using System.IO;
using PinnedMaterials.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinnedMaterials.Ui;

// Lives on a material toggle; owns the small pin button in the bottom-right corner of its material icon.
// The pin Image is added after the existing ones on purpose: MaterialSelector reads its toggle images by index.
public sealed class PinToggleOverlay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Fallback (procedural glyph) tints. Custom images from ModAssets are shown as-is.
    private static readonly Color PinnedColor = new Color(1f, 0.75f, 0.2f, 1f);
    private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color HiddenColor = new Color(1f, 1f, 1f, 0f);

    private static bool spritesLoaded;
    private static Sprite pinnedSprite;
    private static Sprite unpinnedSprite;
    private static bool pinnedIsCustom;
    private static bool unpinnedIsCustom;

    private Tag materialTag;
    private Image image;
    private ToolTip toolTip;
    private bool hovered;

    public void Init(Tag tag)
    {
        materialTag = tag;
        LoadSprites();

        // Attach to the material icon image (index 1, the one MaterialSelector draws the material sprite on) so the
        // pin sits inside the icon rectangle's bottom-right corner rather than the toggle's.
        var images = GetComponentsInChildren<Image>(true);
        IconImage = images.Length > 1 ? images[1] : null;
        var host = IconImage != null ? IconImage.transform : transform;

        var go = new GameObject("PinButton", typeof(RectTransform));
        go.transform.SetParent(host, false);
        go.transform.SetAsLastSibling();

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(16f, 16f);
        rect.anchoredPosition = new Vector2(11f, 3f);

        image = go.AddComponent<Image>();
        image.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(OnPinClicked);

        go.AddComponent<PinDragHandle>().Init(this);

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
        if (pinned)
        {
            image.sprite = pinnedSprite;
            image.color = pinnedIsCustom ? Color.white : PinnedColor;
        }
        else
        {
            image.sprite = unpinnedSprite;
            image.color = hovered ? (unpinnedIsCustom ? Color.white : HoverColor) : HiddenColor;
        }

        if (toolTip != null)
        {
            toolTip.SetSimpleTooltip(pinned ? STRINGS.UI.UNPIN_TOOLTIP : STRINGS.UI.PIN_TOOLTIP);
        }
    }

    private void OnPinClicked()
    {
        PinStore.Toggle(materialTag);
        RefreshAll();
    }

    public Tag MaterialTag => materialTag;

    // The material icon image of this toggle (the one the game draws the material sprite on).
    public Image IconImage { get; private set; }

    // Dims the whole toggle while its pin is dragged, so the live insertion slot is easy to see.
    public void SetDragging(bool dragging)
    {
        var group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            if (!dragging)
            {
                return;
            }

            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = dragging ? 0.35f : 1f;
    }

    // Re-sorts every visible selector (a building can have several material slots) and updates all pin icons.
    public static void RefreshAll()
    {
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

    // ModAssets/pinned.png and ModAssets/unpinned.png win when present; a missing one falls back to the glyph.
    private static void LoadSprites()
    {
        if (spritesLoaded)
        {
            return;
        }

        spritesLoaded = true;
        pinnedSprite = LoadCustomSprite("pinned.png");
        unpinnedSprite = LoadCustomSprite("unpinned.png");
        pinnedIsCustom = pinnedSprite != null;
        unpinnedIsCustom = unpinnedSprite != null;

        if (pinnedSprite == null || unpinnedSprite == null)
        {
            var glyph = CreateGlyphSprite();
            pinnedSprite ??= glyph;
            unpinnedSprite ??= glyph;
        }
    }

    private static Sprite LoadCustomSprite(string fileName)
    {
        if (PinnedMaterialsMod.modPath == null)
        {
            return null;
        }

        var file = Path.Combine(PinnedMaterialsMod.modPath, "ModAssets", fileName);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (texture.LoadImage(File.ReadAllBytes(file)))
            {
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PinnedMaterials] Failed to load " + fileName + ": " + e.Message);
        }

        return null;
    }

    // Procedurally drawn white pin glyph (head + crossbar + needle), tinted through Image.color.
    private static Sprite CreateGlyphSprite()
    {
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
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
