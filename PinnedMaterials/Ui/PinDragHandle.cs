using PinnedMaterials.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinnedMaterials.Ui;

// Drag a pinned material's pin icon over another pinned material to move it to that position in the pin order.
// The order changes live while dragging and is saved on drop. While dragging, a copy of the material icon follows the
// pointer and the dragged toggle is dimmed in its current slot. Unpinned pins ignore dragging.
public sealed class PinDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private PinToggleOverlay owner;
    private bool dragging;
    private int lastMoveFrame = -100;
    private RectTransform ghost;
    private RectTransform ghostCanvasRect;

    public void Init(PinToggleOverlay overlay)
    {
        owner = overlay;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = owner != null && PinStore.IsPinned(owner.MaterialTag);
        lastMoveFrame = -100;
        if (!dragging)
        {
            return;
        }

        owner.SetDragging(true);
        CreateGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        MoveGhost(eventData);

        var hit = eventData.pointerCurrentRaycast.gameObject;
        var target = hit != null ? hit.GetComponentInParent<PinToggleOverlay>() : null;
        if (target == null)
        {
            return;
        }

        if (target == owner)
        {
            return;
        }

        // After a move the layout is rebuilt and the next raycast sees the new positions a frame or two later. Until
        // then the pointer can still be over the swapped neighbour, which would swap straight back (flicker).
        if (Time.frameCount - lastMoveFrame < 3 || !PinStore.IsPinned(target.MaterialTag))
        {
            return;
        }

        PinStore.MoveTo(owner.MaterialTag, PinStore.IndexOf(target.MaterialTag));
        lastMoveFrame = Time.frameCount;
        PinToggleOverlay.RefreshAll();
        Canvas.ForceUpdateCanvases();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyGhost();
        if (!dragging)
        {
            return;
        }

        dragging = false;
        owner.SetDragging(false);
        PinStore.Commit();
    }

    private void OnDisable()
    {
        // The toggle can be destroyed or hidden mid-drag (e.g. the selector is rebuilt); never leave the ghost behind.
        DestroyGhost();
    }

    private void CreateGhost(PointerEventData eventData)
    {
        var icon = owner.IconImage;
        var canvas = owner.GetComponentInParent<Canvas>();
        if (icon == null || icon.sprite == null || canvas == null)
        {
            return;
        }

        var root = canvas.rootCanvas;
        ghostCanvasRect = (RectTransform)root.transform;

        var go = new GameObject("PinDragGhost", typeof(RectTransform));
        go.transform.SetParent(root.transform, false);
        ghost = (RectTransform)go.transform;
        ghost.sizeDelta = ((RectTransform)icon.transform).rect.size;

        // Draw above everything else.
        var ghostCanvas = go.AddComponent<Canvas>();
        ghostCanvas.overrideSorting = true;
        ghostCanvas.sortingOrder = short.MaxValue;

        var image = go.AddComponent<Image>();
        image.sprite = icon.sprite;
        image.color = new Color(1f, 1f, 1f, 0.9f);
        image.raycastTarget = false; // must not block the raycast that finds the insertion target

        MoveGhost(eventData);
    }

    private void MoveGhost(PointerEventData eventData)
    {
        if (ghost == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(ghostCanvasRect, eventData.position,
                eventData.pressEventCamera, out var world))
        {
            ghost.position = world;
        }
    }

    private void DestroyGhost()
    {
        if (ghost != null)
        {
            Destroy(ghost.gameObject);
        }

        ghost = null;
    }
}
