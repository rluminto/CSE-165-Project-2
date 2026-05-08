using System.Collections;
using UnityEngine;

namespace AirRace
{
    public enum RaceState
    {
        Idle,
        Countdown,
        Racing,
        Crashed,
        Finished
    }

    public class RaceManager : MonoBehaviour
    {
        public float initialCountdownSeconds = 3f;

        TrackManager m_Track;
        DroneFlightController m_Flight;
        WayfindingDisplay m_Wayfinding;
        AirRaceHud m_Hud;
        AirRaceAudio m_Audio;
        Transform m_Drone;

        int m_LastClearedIndex;
        int m_TargetIndex;
        float m_ElapsedTime;
        float m_CountdownRemaining;
        bool m_Ready;

        public RaceState State { get; private set; } = RaceState.Idle;
        public int TargetIndex => m_TargetIndex;
        public float ElapsedTime => m_ElapsedTime;

        public void Configure(
            TrackManager track,
            DroneFlightController flight,
            WayfindingDisplay wayfinding,
            AirRaceHud hud,
            AirRaceAudio audio,
            Transform drone)
        {
            m_Track = track;
            m_Flight = flight;
            m_Wayfinding = wayfinding;
            m_Hud = hud;
            m_Audio = audio;
            m_Drone = drone;
        }

        IEnumerator Start()
        {
            State = RaceState.Idle;
            m_Flight.SetFlightEnabled(false);
            m_Hud.SetNotice("Loading track...");

            yield return m_Track.LoadTrackRoutine();
            if (m_Track.Checkpoints.Count == 0)
            {
                m_Hud.SetNotice($"Track load failed: {m_Track.LastError}");
                yield break;
            }

            m_Wayfinding.Initialize(m_Track);
            BeginRaceAtStart();
            m_Ready = true;
            yield return RunCountdown(initialCountdownSeconds, "Starting");
            State = RaceState.Racing;
            m_Flight.SetFlightEnabled(true);
            m_Hud.SetNotice("Go!");
        }

        void Update()
        {
            if (!m_Ready)
                return;

            if (State == RaceState.Racing || State == RaceState.Crashed)
                m_ElapsedTime += Time.deltaTime;

            m_Hud.UpdateHud(
                m_ElapsedTime,
                State,
                m_TargetIndex,
                m_Track.Checkpoints.Count,
                m_CountdownRemaining,
                GetTargetDirection(),
                GetTargetDistance());

            m_Wayfinding.UpdateTarget(m_TargetIndex, m_LastClearedIndex);

            if (State != RaceState.Racing || m_TargetIndex >= m_Track.Checkpoints.Count)
                return;

            if (Vector3.Distance(m_Drone.position, m_Track.Checkpoints[m_TargetIndex]) <= m_Track.checkpointRadiusMeters)
                ClearCurrentCheckpoint();
        }

        public void ReportCrash(Collider hit)
        {
            if (State != RaceState.Racing)
                return;

            StartCoroutine(HandleCrash(hit));
        }

        void BeginRaceAtStart()
        {
            m_ElapsedTime = 0f;
            m_LastClearedIndex = 0;
            m_TargetIndex = m_Track.Checkpoints.Count > 1 ? 1 : 0;
            MoveDroneToCheckpoint(m_LastClearedIndex);
            FaceTarget();
            m_Wayfinding.UpdateTarget(m_TargetIndex, m_LastClearedIndex);
            m_Hud.SetNotice($"Start at checkpoint 1. Fly to checkpoint {m_TargetIndex + 1}.");
        }

        void ClearCurrentCheckpoint()
        {
            m_LastClearedIndex = m_TargetIndex;
            m_Hud.SetNotice($"Checkpoint {m_TargetIndex + 1} reached");
            m_Audio?.PlayCheckpoint();

            if (m_TargetIndex >= m_Track.Checkpoints.Count - 1)
            {
                State = RaceState.Finished;
                m_Flight.SetFlightEnabled(false);
                m_Hud.SetNotice($"Finished in {FormatTime(m_ElapsedTime)}");
                m_Audio?.PlayFinish();
                return;
            }

            m_TargetIndex++;
            m_Wayfinding.PulseTarget(m_TargetIndex);
        }

        IEnumerator HandleCrash(Collider hit)
        {
            State = RaceState.Crashed;
            m_Flight.SetFlightEnabled(false);
            m_Hud.SetNotice($"Crash: {hit.name}");
            m_Audio?.PlayCrash();
            MoveDroneToCheckpoint(m_LastClearedIndex);
            FaceTarget();

            yield return RunCountdown(Mathf.Max(3f, m_Flight.crashCooldownSeconds), "Crash reset");

            if (State == RaceState.Crashed)
            {
                State = RaceState.Racing;
                m_Flight.SetFlightEnabled(true);
                m_Hud.SetNotice("Continue");
            }
        }

        IEnumerator RunCountdown(float seconds, string label)
        {
            State = State == RaceState.Crashed ? RaceState.Crashed : RaceState.Countdown;
            var remaining = seconds;
            while (remaining > 0f)
            {
                var currentWholeSecond = Mathf.CeilToInt(remaining);
                m_CountdownRemaining = remaining;
                m_Hud.SetNotice($"{label}: {currentWholeSecond}");
                m_Audio?.PlayCountdownBeep();

                var nextBeepThreshold = currentWholeSecond - 1f;
                while (remaining > nextBeepThreshold && remaining > 0f)
                {
                    yield return null;
                    remaining -= Time.deltaTime;
                    m_CountdownRemaining = remaining;
                }
            }

            m_CountdownRemaining = 0f;
        }

        void MoveDroneToCheckpoint(int checkpointIndex)
        {
            if (checkpointIndex < 0 || checkpointIndex >= m_Track.Checkpoints.Count)
                return;

            m_Drone.position = m_Track.Checkpoints[checkpointIndex];
        }

        void FaceTarget()
        {
            var direction = GetTargetDirection();
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                m_Drone.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        Vector3 GetTargetDirection()
        {
            if (m_TargetIndex < 0 || m_TargetIndex >= m_Track.Checkpoints.Count)
                return m_Drone.forward;

            return (m_Track.Checkpoints[m_TargetIndex] - m_Drone.position).normalized;
        }

        float GetTargetDistance()
        {
            if (m_TargetIndex < 0 || m_TargetIndex >= m_Track.Checkpoints.Count)
                return 0f;

            return Vector3.Distance(m_Drone.position, m_Track.Checkpoints[m_TargetIndex]);
        }

        static string FormatTime(float seconds)
        {
            var minutes = Mathf.FloorToInt(seconds / 60f);
            var secs = seconds - minutes * 60f;
            return $"{minutes:00}:{secs:00.00}";
        }
    }
}
