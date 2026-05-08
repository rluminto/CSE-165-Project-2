using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace AirRace
{
    public class HandFlightInput : MonoBehaviour
    {
        public float pinchOpenDistance = 0.1f;
        public float pinchClosedDistance = 0.025f;
        public float viewSwitchThreshold = 0.72f;
        public float viewSwitchReleaseThreshold = 0.28f;
        public float viewSwitchReleaseSeconds = 0.18f;
        public float viewSwitchCooldownSeconds = 0.35f;

        readonly List<XRHandSubsystem> m_Subsystems = new List<XRHandSubsystem>();

        Transform m_XrOrigin;
        DroneFlightController m_Flight;
        XRHandSubsystem m_Subsystem;
        bool m_ViewGestureArmed = true;
        float m_ViewGestureOpenSince = -1f;
        float m_LastViewSwitchTime = -999f;

        public bool HandsTracked { get; private set; }
        public float Throttle { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

        public void Configure(Transform xrOrigin, DroneFlightController flight)
        {
            m_XrOrigin = xrOrigin;
            m_Flight = flight;
        }

        void Update()
        {
            if (m_Subsystem == null || !m_Subsystem.running)
                FindHandSubsystem();

            if (m_Subsystem == null)
            {
                HandsTracked = false;
                Throttle = 0f;
                m_Flight?.SetInput(AimDirection, Throttle, false);
                return;
            }

            var rightPalm = Pose.identity;
            var rightValid = TryGetPose(m_Subsystem.rightHand, XRHandJointID.Palm, out rightPalm);
            var leftPinch = TryGetPinchAmount(m_Subsystem.leftHand, out var throttle);

            HandsTracked = rightValid && leftPinch;
            Throttle = leftPinch ? throttle : 0f;

            if (rightValid)
            {
                var aim = rightPalm.rotation * Vector3.forward;
                if (aim.sqrMagnitude < 0.0001f)
                    aim = transform.forward;

                AimDirection = aim.normalized;
            }

            m_Flight?.SetInput(AimDirection, Throttle, HandsTracked);
        }

        public bool ConsumeViewSwitchGesture()
        {
            if (m_Subsystem == null)
                return false;

            if (!TryGetPinchAmount(m_Subsystem.rightHand, XRHandJointID.ThumbTip, XRHandJointID.MiddleTip, out var pinch))
                return false;

            if (pinch <= viewSwitchReleaseThreshold)
            {
                if (m_ViewGestureOpenSince < 0f)
                    m_ViewGestureOpenSince = Time.unscaledTime;

                if (Time.unscaledTime - m_ViewGestureOpenSince >= viewSwitchReleaseSeconds)
                    m_ViewGestureArmed = true;

                return false;
            }

            m_ViewGestureOpenSince = -1f;

            if (pinch < viewSwitchThreshold || !m_ViewGestureArmed)
                return false;

            if (Time.unscaledTime - m_LastViewSwitchTime < viewSwitchCooldownSeconds)
                return false;

            m_ViewGestureArmed = false;
            m_LastViewSwitchTime = Time.unscaledTime;
            return true;
        }

        void FindHandSubsystem()
        {
            m_Subsystems.Clear();
            SubsystemManager.GetSubsystems(m_Subsystems);
            foreach (var subsystem in m_Subsystems)
            {
                if (subsystem.running)
                {
                    m_Subsystem = subsystem;
                    return;
                }
            }
        }

        bool TryGetPose(XRHand hand, XRHandJointID jointId, out Pose pose)
        {
            pose = Pose.identity;

            try
            {
                if (!hand.isTracked)
                    return false;

                var joint = hand.GetJoint(jointId);
                if (!joint.TryGetPose(out var localPose))
                    return false;

                pose = TransformPose(localPose);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        Pose TransformPose(Pose localPose)
        {
            if (m_XrOrigin == null)
                return localPose;

            return new Pose(
                m_XrOrigin.TransformPoint(localPose.position),
                m_XrOrigin.rotation * localPose.rotation);
        }

        bool TryGetPinchAmount(XRHand hand, out float amount)
        {
            return TryGetPinchAmount(hand, XRHandJointID.ThumbTip, XRHandJointID.IndexTip, out amount);
        }

        bool TryGetPinchAmount(XRHand hand, XRHandJointID a, XRHandJointID b, out float amount)
        {
            amount = 0f;
            if (!TryGetPose(hand, a, out var first) || !TryGetPose(hand, b, out var second))
                return false;

            var distance = Vector3.Distance(first.position, second.position);
            amount = Mathf.InverseLerp(pinchOpenDistance, pinchClosedDistance, distance);
            amount = Mathf.Clamp01(amount);
            return true;
        }
    }
}
