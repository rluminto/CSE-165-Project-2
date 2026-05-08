using UnityEngine;

namespace AirRace
{
    public class DroneFlightController : MonoBehaviour
    {
        public float maxSpeed = 46f;
        public float minSpeed = 0f;
        public float turnSmoothing = 4f;
        public float throttleSmoothing = 3.5f;
        public float crashCooldownSeconds = 3f;
        public float throttleDeadZone = 0.08f;
        public float maxTurnDegreesPerSecond = 58f;
        public float highSpeedTurnDegreesPerSecond = 120f;
        public float accelerationMetersPerSecondSquared = 15f;
        public float brakingMetersPerSecondSquared = 21f;

        RaceManager m_RaceManager;
        Vector3 m_AimDirection = Vector3.forward;
        float m_TargetThrottle;
        float m_CurrentSpeed;
        bool m_InputValid;

        public bool FlightEnabled { get; private set; }
        public float CurrentSpeed => m_CurrentSpeed;
        public float Speed01 => maxSpeed <= 0f ? 0f : Mathf.Clamp01(m_CurrentSpeed / maxSpeed);

        public void Configure(RaceManager raceManager)
        {
            m_RaceManager = raceManager;
        }

        public void SetFlightEnabled(bool enabled)
        {
            FlightEnabled = enabled;
            if (!enabled)
            {
                m_TargetThrottle = 0f;
                m_CurrentSpeed = 0f;
            }
        }

        public void SetInput(Vector3 aimDirection, float throttle, bool inputValid)
        {
            if (aimDirection.sqrMagnitude > 0.001f)
                m_AimDirection = aimDirection.normalized;

            m_TargetThrottle = Mathf.Clamp01(throttle);
            m_InputValid = inputValid;
        }

        void Update()
        {
            if (!FlightEnabled || !m_InputValid)
            {
                m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, 0f, brakingMetersPerSecondSquared * Time.deltaTime);
                return;
            }

            var hasThrottle = m_TargetThrottle > throttleDeadZone;
            if (hasThrottle || m_CurrentSpeed > 0.25f)
            {
                var targetRotation = Quaternion.LookRotation(m_AimDirection, Vector3.up);
                var smoothedRotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSmoothing * Time.deltaTime));
                var speedTurnLimit = Mathf.Lerp(maxTurnDegreesPerSecond, highSpeedTurnDegreesPerSecond, Speed01);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    smoothedRotation,
                    speedTurnLimit * Time.deltaTime);
            }

            if (hasThrottle)
            {
                var usableThrottle = Mathf.InverseLerp(throttleDeadZone, 1f, m_TargetThrottle);
                var targetSpeed = Mathf.Lerp(minSpeed, maxSpeed, usableThrottle);
                var smoothedTarget = Mathf.Lerp(m_CurrentSpeed, targetSpeed, 1f - Mathf.Exp(-throttleSmoothing * Time.deltaTime));
                m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, smoothedTarget, accelerationMetersPerSecondSquared * Time.deltaTime);
            }
            else
            {
                m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, 0f, brakingMetersPerSecondSquared * Time.deltaTime);
            }

            transform.position += transform.forward * m_CurrentSpeed * Time.deltaTime;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!FlightEnabled || other.transform.IsChildOf(transform))
                return;

            m_RaceManager?.ReportCrash(other);
        }
    }
}
