using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fysik.Game
{
    internal sealed class Overlay
    {
        private const float Gap = 10f;

        public static Overlay Instance { get; } = new Overlay();

        private readonly List<Rect> _below = new List<Rect>();
        private readonly Vector3[] _corners = new Vector3[4];
        private TextMeshProUGUI _template;
        private TextMeshProUGUI _panel;
        private Canvas _canvas;
        private RectTransform _root;
        private int _shownFrame = -1;

        public static bool CanShow(Hud hud) =>
            hud != null && hud.IsVisible() && !InventoryGui.IsVisible() && !Menu.IsVisible() && !Minimap.IsOpen() &&
            !StoreGui.IsVisible() && (hud.m_pieceSelectionWindow == null || !hud.m_pieceSelectionWindow.activeSelf);

        public void Show(Hud hud, string text)
        {
            if (!Ensure(hud))
                return;
            _shownFrame = Time.frameCount;
            Camera ui = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (string.IsNullOrEmpty(text))
            {
                _panel.gameObject.SetActive(false);
                return;
            }
            _panel.text = text;
            _panel.gameObject.SetActive(true);
            PlacePanel(hud, ui);
        }

        public void HideIfStale()
        {
            if (_panel == null || Time.frameCount - _shownFrame <= 1)
                return;
            _panel.gameObject.SetActive(false);
        }

        private bool Ensure(Hud hud)
        {
            if (_panel != null)
                return true;
            if (hud == null || hud.m_hoverName == null || hud.m_hoverName.canvas == null)
                return false;
            _template = hud.m_hoverName;
            _canvas = _template.canvas.rootCanvas;
            _root = (RectTransform)_canvas.transform;
            _panel = Clone();
            return true;
        }

        private TextMeshProUGUI Clone()
        {
            GameObject go = Object.Instantiate(_template.gameObject, _root, false);
            go.name = "FysikPanel";
            foreach (Transform child in go.transform)
                Object.Destroy(child.gameObject);
            foreach (ContentSizeFitter fitter in go.GetComponents<ContentSizeFitter>())
                Object.Destroy(fitter);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.text = "";
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0, 1);
            go.SetActive(false);
            return text;
        }

        private void PlacePanel(Hud hud, Camera ui)
        {
            float scale = _canvas.scaleFactor;
            Vector2 size = _panel.GetPreferredValues(_panel.text);
            _panel.rectTransform.sizeDelta = size;
            size *= scale;
            float gap = Gap * scale;

            Rect anchor = ScreenRect(_template.rectTransform, ui);
            var origin = new Vector2(anchor.xMin, anchor.yMax);
            _below.Clear();
            if (!string.IsNullOrEmpty(_template.text) && TextRect(_template, ui, out Rect vanilla))
                _below.Add(vanilla);
            if (hud.m_hoveredPieceAuthorWindow != null && hud.m_hoveredPieceAuthorWindow.activeInHierarchy)
                _below.Add(ScreenRect((RectTransform)hud.m_hoveredPieceAuthorWindow.transform, ui));
            if (hud.m_pieceHealthRoot != null && hud.m_pieceHealthRoot.gameObject.activeInHierarchy)
            {
                Rect bar = ScreenRect(hud.m_pieceHealthRoot, ui);
                if (Overlaps(origin, size, bar))
                    origin.x = bar.xMax + gap;
            }
            for (int pass = 0; pass < 3; pass++)
                foreach (Rect r in _below)
                    if (Overlaps(origin, size, r))
                        origin.y = r.yMin - gap;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(_root, origin, ui, out Vector3 world);
            _panel.rectTransform.position = world;
            if (_panel.transform.GetSiblingIndex() != _root.childCount - 1)
                _panel.transform.SetAsLastSibling();
        }

        private static bool Overlaps(Vector2 topLeft, Vector2 size, Rect r) =>
            topLeft.x < r.xMax && topLeft.x + size.x > r.xMin && topLeft.y > r.yMin && topLeft.y - size.y < r.yMax;

        private Rect ScreenRect(RectTransform rect, Camera ui)
        {
            rect.GetWorldCorners(_corners);
            return Bounds(_corners, ui);
        }

        private bool TextRect(TMP_Text text, Camera ui, out Rect rect)
        {
            text.ForceMeshUpdate();
            Bounds b = text.textBounds;
            rect = default;
            if (b.size.x <= 0 || b.size.y <= 0)
                return false;
            Transform t = text.transform;
            _corners[0] = t.TransformPoint(new Vector3(b.min.x, b.min.y));
            _corners[1] = t.TransformPoint(new Vector3(b.min.x, b.max.y));
            _corners[2] = t.TransformPoint(new Vector3(b.max.x, b.max.y));
            _corners[3] = t.TransformPoint(new Vector3(b.max.x, b.min.y));
            rect = Bounds(_corners, ui);
            return true;
        }

        private static Rect Bounds(Vector3[] corners, Camera ui)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 c in corners)
            {
                Vector2 s = RectTransformUtility.WorldToScreenPoint(ui, c);
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
