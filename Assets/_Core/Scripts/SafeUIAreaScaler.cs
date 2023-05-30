namespace BlackRece.LaSARTag.Geospatial {
    using UnityEngine;

    /// <summary>
    /// A helper component that scale the UI rect to the same size as the safe area.
    /// </summary>
    public class SafeUIAreaScaler : MonoBehaviour
    {
        private Rect _screenSafeArea = new Rect(0, 0, 0, 0);

        public void Update()
        {
            Rect safeArea = Screen.safeArea;

            if (_screenSafeArea == safeArea)
                return;

            _screenSafeArea = safeArea;
            MatchRectTransformToSafeArea();
        }

        private void MatchRectTransformToSafeArea()
        {
            var rectTransform = GetComponent<RectTransform>();

            // lower left corner offset
            rectTransform.offsetMin =
                new Vector2(_screenSafeArea.xMin, Screen.height - _screenSafeArea.yMax);

            // upper right corner offset
            rectTransform.offsetMax =
                new Vector2(_screenSafeArea.xMax - Screen.width, -_screenSafeArea.yMin);
        }
    }
}
