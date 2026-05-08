using System.Collections.Generic;
using UnityEngine;

namespace AirRace
{
    public class WayfindingDisplay : MonoBehaviour
    {
        Transform m_Drone;
        Transform m_Camera;
        TrackManager m_Track;
        LineRenderer m_RouteLine;
        GameObject m_WorldArrow;
        Material m_PendingMaterial;
        Material m_CurrentMaterial;
        Material m_ReachedMaterial;
        readonly List<GameObject> m_CheckpointVisuals = new List<GameObject>();

        int m_TargetIndex = -1;
        int m_LastClearedIndex = -1;

        public void Configure(Transform drone, Transform cameraTransform)
        {
            m_Drone = drone;
            m_Camera = cameraTransform;
        }

        public void Initialize(TrackManager track)
        {
            m_Track = track;
            CreateMaterials();
            CreateCheckpointVisuals();
            CreateRouteLine();
        }

        public void UpdateTarget(int targetIndex, int lastClearedIndex)
        {
            m_TargetIndex = targetIndex;
            m_LastClearedIndex = lastClearedIndex;
        }

        public void PulseTarget(int targetIndex)
        {
            m_TargetIndex = targetIndex;
        }

        void LateUpdate()
        {
            if (m_Track == null || m_Drone == null || m_TargetIndex < 0 || m_TargetIndex >= m_Track.Checkpoints.Count)
                return;

            UpdateCheckpointMaterials();
            UpdateRouteAid();
        }

        void CreateMaterials()
        {
            m_PendingMaterial = MakeTransparentMaterial("Checkpoint Pending", new Color(0.2f, 0.55f, 1f, 0.18f));
            m_CurrentMaterial = MakeTransparentMaterial("Checkpoint Current", new Color(1f, 0.9f, 0.15f, 0.45f));
            m_ReachedMaterial = MakeTransparentMaterial("Checkpoint Reached", new Color(1f, 0.08f, 0.05f, 0.28f));
        }

        void CreateCheckpointVisuals()
        {
            foreach (var existing in m_CheckpointVisuals)
                Destroy(existing);

            m_CheckpointVisuals.Clear();

            var diameter = m_Track.checkpointRadiusMeters * 2f;
            for (var i = 0; i < m_Track.Checkpoints.Count; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Checkpoint {i + 1}";
                sphere.transform.SetParent(transform, false);
                sphere.transform.position = m_Track.Checkpoints[i];
                sphere.transform.localScale = Vector3.one * diameter;

                var collider = sphere.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                sphere.GetComponent<Renderer>().sharedMaterial = m_PendingMaterial;
                m_CheckpointVisuals.Add(sphere);
            }
        }

        void CreateRouteLine()
        {
            var lineObject = new GameObject("World Route Aid");
            lineObject.transform.SetParent(transform, false);
            m_RouteLine = lineObject.AddComponent<LineRenderer>();
            m_RouteLine.positionCount = 2;
            m_RouteLine.widthMultiplier = 0.45f;
            m_RouteLine.material = MakeLineMaterial(new Color(1f, 0.85f, 0.1f, 0.85f));
            m_RouteLine.textureMode = LineTextureMode.Tile;

            m_WorldArrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            m_WorldArrow.name = "World Direction Arrow";
            m_WorldArrow.transform.SetParent(transform, false);
            m_WorldArrow.transform.localScale = new Vector3(0.4f, 1.2f, 0.4f);
            var collider = m_WorldArrow.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            m_WorldArrow.GetComponent<Renderer>().sharedMaterial = MakeLineMaterial(new Color(1f, 0.85f, 0.1f, 1f));
        }

        void UpdateCheckpointMaterials()
        {
            for (var i = 0; i < m_CheckpointVisuals.Count; i++)
            {
                var renderer = m_CheckpointVisuals[i].GetComponent<Renderer>();
                if (i <= m_LastClearedIndex)
                    renderer.sharedMaterial = m_ReachedMaterial;
                else if (i == m_TargetIndex)
                    renderer.sharedMaterial = m_CurrentMaterial;
                else
                    renderer.sharedMaterial = m_PendingMaterial;

                var targetScale = m_Track.checkpointRadiusMeters * 2f * (i == m_TargetIndex ? 1.15f : 1f);
                m_CheckpointVisuals[i].transform.localScale = Vector3.one * targetScale;
            }
        }

        void UpdateRouteAid()
        {
            var target = m_Track.Checkpoints[m_TargetIndex];
            var dronePosition = m_Drone.position;
            var direction = target - dronePosition;
            var distance = direction.magnitude;
            if (distance < 0.01f)
                return;

            var normalized = direction / distance;
            m_RouteLine.SetPosition(0, dronePosition);
            m_RouteLine.SetPosition(1, target);

            m_WorldArrow.transform.position = dronePosition + normalized * Mathf.Min(distance * 0.35f, 12f);
            m_WorldArrow.transform.rotation = Quaternion.FromToRotation(Vector3.up, normalized);
            m_WorldArrow.SetActive(m_Camera == null || Vector3.Distance(m_Camera.position, m_WorldArrow.transform.position) > 1.5f);
        }

        static Material MakeTransparentMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }

        static Material MakeLineMaterial(Color color)
        {
            var material = new Material(Shader.Find("Sprites/Default")) { name = "Waypoint Line", color = color };
            return material;
        }
    }
}
