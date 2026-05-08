using UnityEngine;
using UnityEngine.UI;

namespace AirRace
{
    public class AirRaceHud : MonoBehaviour
    {
        Transform m_Camera;
        Text m_StatusText;
        Text m_DistanceText;
        Text m_ArrowText;
        Text m_NoticeText;
        float m_NoticeTime;

        public void Configure(Transform cameraTransform)
        {
            m_Camera = cameraTransform;
            CreateHud();
        }

        public void SetNotice(string message)
        {
            if (m_NoticeText == null)
                return;

            m_NoticeText.text = message;
            m_NoticeTime = 2.2f;
        }

        public void UpdateHud(
            float elapsedSeconds,
            RaceState state,
            int targetIndex,
            int checkpointCount,
            float countdownRemaining,
            Vector3 targetDirection,
            float targetDistance)
        {
            if (m_StatusText == null)
                return;

            var completed = Mathf.Clamp(targetIndex, 1, checkpointCount);
            if (state == RaceState.Finished)
                completed = checkpointCount;

            m_StatusText.text = $"Time {FormatTime(elapsedSeconds)}\nCompleted {completed}/{checkpointCount}\n{state}";
            m_DistanceText.text = countdownRemaining > 0f
                ? $"Countdown {Mathf.CeilToInt(countdownRemaining)}"
                : $"Target {targetDistance:0} m";

            UpdateArrow(targetDirection);

            if (m_NoticeTime > 0f)
            {
                m_NoticeTime -= Time.deltaTime;
                m_NoticeText.enabled = true;
            }
            else
            {
                m_NoticeText.enabled = false;
            }
        }

        void CreateHud()
        {
            if (m_Camera == null)
                return;

            var canvasObject = new GameObject("Air Race HUD");
            canvasObject.transform.SetParent(m_Camera, false);
            canvasObject.transform.localPosition = new Vector3(0f, -0.32f, 1.25f);
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.0015f;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = m_Camera.GetComponent<Camera>();

            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(760f, 360f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_StatusText = CreateText("Status", canvasObject.transform, font, new Vector2(-250f, 90f), new Vector2(260f, 150f), 28, TextAnchor.UpperLeft);
            m_DistanceText = CreateText("Distance", canvasObject.transform, font, new Vector2(210f, 100f), new Vector2(260f, 70f), 34, TextAnchor.MiddleCenter);
            m_ArrowText = CreateText("Direction Arrow", canvasObject.transform, font, new Vector2(220f, -25f), new Vector2(120f, 120f), 86, TextAnchor.MiddleCenter);
            m_ArrowText.text = "^";
            m_NoticeText = CreateText("Notice", canvasObject.transform, font, new Vector2(0f, -135f), new Vector2(620f, 60f), 34, TextAnchor.MiddleCenter);
            m_NoticeText.color = new Color(1f, 0.9f, 0.2f, 1f);
            m_NoticeText.enabled = false;
        }

        static Text CreateText(string name, Transform parent, Font font, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;

            var rect = text.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }

        void UpdateArrow(Vector3 targetDirection)
        {
            if (m_Camera == null || m_ArrowText == null || targetDirection.sqrMagnitude < 0.001f)
                return;

            var local = m_Camera.InverseTransformDirection(targetDirection.normalized);
            var angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            m_ArrowText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
        }

        static string FormatTime(float seconds)
        {
            var minutes = Mathf.FloorToInt(seconds / 60f);
            var secs = seconds - minutes * 60f;
            return $"{minutes:00}:{secs:00.00}";
        }
    }
}
