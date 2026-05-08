using UnityEngine;

namespace AirRace
{
    public class AirRaceAudio : MonoBehaviour
    {
        public bool audioEnabled = true;
        public float motorBasePitch = 0.85f;
        public float motorFastPitch = 2.15f;
        public float motorBaseVolume = 0.45f;
        public float motorFastVolume = 1f;
        public float minimumAudibleMotorSpeed = 0.18f;
        public string motorClipResourcePath = "Audio/enginesound";
        public bool playStartupTestTone = false;

        AudioSource m_MotorSource;
        AudioSource m_EffectsSource;
        AudioClip m_MotorClip;
        AudioClip m_CountdownClip;
        AudioClip m_CheckpointClip;
        AudioClip m_CrashClip;
        AudioClip m_FinishClip;
        DroneFlightController m_Flight;
        GameObject m_SourceObject;

        public void Configure(DroneFlightController flight, Transform listenerParent)
        {
            m_Flight = flight;

            EnsureAudioListener(listenerParent);
            AudioListener.pause = false;
            AudioListener.volume = 1f;

            m_SourceObject = new GameObject("Air Race Audio Sources");
            m_SourceObject.transform.SetParent(listenerParent != null ? listenerParent : transform, false);
            m_SourceObject.transform.localPosition = Vector3.zero;
            m_SourceObject.transform.localRotation = Quaternion.identity;

            m_MotorSource = m_SourceObject.AddComponent<AudioSource>();
            m_EffectsSource = m_SourceObject.AddComponent<AudioSource>();

            m_MotorSource.loop = true;
            m_MotorSource.spatialBlend = 0f;
            m_MotorSource.playOnAwake = false;
            m_MotorSource.volume = motorBaseVolume;
            m_MotorSource.ignoreListenerPause = true;
            m_MotorSource.priority = 32;

            m_EffectsSource.loop = false;
            m_EffectsSource.spatialBlend = 0f;
            m_EffectsSource.playOnAwake = false;
            m_EffectsSource.volume = 1f;
            m_EffectsSource.ignoreListenerPause = true;

            m_MotorClip = Resources.Load<AudioClip>(motorClipResourcePath);
            if (m_MotorClip == null)
                Debug.LogError($"AirRace: Missing engine sound at Resources/{motorClipResourcePath}.");

            m_CountdownClip = CreateToneClip("Countdown Beep", 880f, 0.18f, 0.95f);
            m_CheckpointClip = CreateTwoToneClip("Checkpoint Chime", 660f, 990f, 0.32f, 0.9f);
            m_CrashClip = CreateNoiseClip("Crash Noise", 0.42f, 0.95f);
            m_FinishClip = CreateFinishClip();

            m_MotorSource.clip = m_MotorClip;
            if (audioEnabled && m_MotorClip != null)
            {
                m_MotorSource.Play();
                if (playStartupTestTone)
                    Invoke(nameof(PlayStartupTest), 0.3f);
            }
        }

        void Update()
        {
            if (!audioEnabled || m_MotorSource == null || m_Flight == null)
                return;

            var speed01 = m_Flight.Speed01;
            speed01 = Mathf.Max(speed01, minimumAudibleMotorSpeed);
            m_MotorSource.pitch = Mathf.Lerp(motorBasePitch, motorFastPitch, speed01);
            m_MotorSource.volume = Mathf.Lerp(motorBaseVolume, motorFastVolume, speed01);

            if (!m_MotorSource.isPlaying)
                m_MotorSource.Play();
        }

        public void PlayCountdownBeep()
        {
            PlayEffect(m_CountdownClip, 0.7f);
        }

        public void PlayCheckpoint()
        {
            PlayEffect(m_CheckpointClip, 0.8f);
        }

        public void PlayCrash()
        {
            PlayEffect(m_CrashClip, 0.9f);
        }

        public void PlayFinish()
        {
            PlayEffect(m_FinishClip, 0.9f);
        }

        void PlayStartupTest()
        {
            PlayEffect(m_CountdownClip, 0.85f);
        }

        void PlayEffect(AudioClip clip, float volume)
        {
            if (!audioEnabled || m_EffectsSource == null || clip == null)
                return;

            m_EffectsSource.PlayOneShot(clip, volume);
        }

        static void EnsureAudioListener(Transform listenerParent)
        {
            if (FindAnyObjectByType<AudioListener>() != null)
                return;

            var listenerObject = new GameObject("Air Race Audio Listener");
            if (listenerParent != null)
                listenerObject.transform.SetParent(listenerParent, false);

            listenerObject.AddComponent<AudioListener>();
        }

        static AudioClip CreateToneClip(string name, float frequency, float duration, float volume)
        {
            const int sampleRate = 44100;
            var samples = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[samples];

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = Mathf.Clamp01(1f - t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateTwoToneClip(string name, float firstFrequency, float secondFrequency, float duration, float volume)
        {
            const int sampleRate = 44100;
            var samples = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[samples];

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var frequency = t < duration * 0.5f ? firstFrequency : secondFrequency;
                var envelope = Mathf.Clamp01(1f - t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateNoiseClip(string name, float duration, float volume)
        {
            const int sampleRate = 44100;
            var samples = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[samples];
            var seed = 17u;

            for (var i = 0; i < samples; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                var random = ((seed >> 8) / 16777215f) * 2f - 1f;
                var t = i / (float)sampleRate;
                var envelope = Mathf.Clamp01(1f - t / duration);
                data[i] = random * envelope * volume;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip CreateFinishClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.75f;
            var samples = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[samples];
            var tones = new[] { 523.25f, 659.25f, 783.99f, 1046.5f };

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)sampleRate;
                var toneIndex = Mathf.Min(tones.Length - 1, Mathf.FloorToInt(t / (duration / tones.Length)));
                var envelope = Mathf.Clamp01(1f - t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * tones[toneIndex] * t) * envelope * 0.9f;
            }

            var clip = AudioClip.Create("Finish Fanfare", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
