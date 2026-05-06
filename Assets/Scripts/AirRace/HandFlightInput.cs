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

        readonly List<XRHandSubsystem> m_Subsystems = new List<XRHandSubsystem>();

        Transform m_XrOrigin;
        DroneFlightController m_Flight;
        XRHandSubsystem m_Subsystem;
        bool m_ViewGestureWasHeld;

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

            var amount = TryGetPinchAmount(m_Subsystem.rightHand, XRHandJointID.ThumbTip, XRHandJointID.MiddleTip, out var pinch)
                ? pinch
                : 0f;
            var held = amount >= viewSwitchThreshold;
            var pressed = held && !m_ViewGestureWasHeld;
            m_ViewGestureWasHeld = held;
            return pressed;
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
