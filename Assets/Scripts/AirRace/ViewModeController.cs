using UnityEngine;

namespace AirRace
{
    public class ViewModeController : MonoBehaviour
    {
        public enum ViewMode
        {
            PilotNoVisuals,
            PilotCockpit,
            ThirdPerson
        }

        public Vector3 firstPersonOffset = Vector3.zero;
        public Vector3 thirdPersonOffset = new Vector3(0f, 1.2f, -8f);

        HandFlightInput m_Input;
        Transform m_XrOrigin;
        GameObject m_DroneVisuals;
        GameObject m_CockpitVisuals;

        public ViewMode CurrentMode { get; private set; } = ViewMode.PilotNoVisuals;

        public void Configure(HandFlightInput input, Transform xrOrigin, GameObject droneVisuals, GameObject cockpitVisuals)
        {
            m_Input = input;
            m_XrOrigin = xrOrigin;
            m_DroneVisuals = droneVisuals;
            m_CockpitVisuals = cockpitVisuals;
            ApplyMode();
        }

        void Update()
        {
            if (m_Input != null && m_Input.ConsumeViewSwitchGesture())
                CycleMode();
        }

        public void CycleMode()
        {
            CurrentMode = (ViewMode)(((int)CurrentMode + 1) % 3);
            ApplyMode();
        }

        void ApplyMode()
        {
            if (m_XrOrigin != null)
            {
                m_XrOrigin.localPosition = CurrentMode == ViewMode.ThirdPerson ? thirdPersonOffset : firstPersonOffset;
                m_XrOrigin.localRotation = Quaternion.identity;
            }

            if (m_DroneVisuals != null)
                m_DroneVisuals.SetActive(CurrentMode == ViewMode.ThirdPerson);

            if (m_CockpitVisuals != null)
                m_CockpitVisuals.SetActive(CurrentMode == ViewMode.PilotCockpit);
        }
    }
}
