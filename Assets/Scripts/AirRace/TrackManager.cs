using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AirRace
{
    public class TrackManager : MonoBehaviour
    {
        public const float InchesToMeters = 0.0254f;

        public TextAsset defaultTrack;
        public string externalTrackPath = "";
        public float coordinateScale = InchesToMeters;
        public float checkpointRadiusMeters = 9.144f;
        public int maxCheckpoints = 100;

        readonly List<Vector3> m_Checkpoints = new List<Vector3>();

        public IReadOnlyList<Vector3> Checkpoints => m_Checkpoints;
        public string LoadedTrackSource { get; private set; } = "";
        public string LastError { get; private set; } = "";

        public IEnumerator LoadTrackRoutine()
        {
            m_Checkpoints.Clear();
            LastError = "";
            LoadedTrackSource = "";

            var fileCandidates = new List<string>();
            AddCandidate(fileCandidates, externalTrackPath);
            AddCandidate(fileCandidates, GetCommandLineTrackPath());
            AddCandidate(fileCandidates, Environment.GetEnvironmentVariable("AIR_RACE_TRACK"));
            AddCandidate(fileCandidates, Path.Combine(Application.persistentDataPath, "track.txt"));
            AddCandidate(fileCandidates, Path.Combine(Application.persistentDataPath, "competition.xyz"));
            AddRemovableDriveCandidates(fileCandidates);

            foreach (var candidate in fileCandidates)
            {
                if (TryLoadFileCandidate(candidate))
                    yield break;
            }

            var streamingTrack = Path.Combine(Application.streamingAssetsPath, "track.txt");
            yield return LoadStreamingTrack(streamingTrack);
            if (m_Checkpoints.Count > 0)
                yield break;

            if (defaultTrack != null && TryParse(defaultTrack.text, "Resources/DefaultTrack.txt"))
                yield break;

            LastError = "No valid track file could be loaded.";
            Debug.LogError($"AirRace: {LastError}");
        }

        static void AddCandidate(List<string> candidates, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            candidates.Add(value.Trim().Trim('"'));
        }

        static string GetCommandLineTrackPath()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-track", StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return "";
        }

        static void AddRemovableDriveCandidates(List<string> candidates)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Removable)
                        continue;

                    AddCandidate(candidates, Path.Combine(drive.RootDirectory.FullName, "track.txt"));
                    AddCandidate(candidates, Path.Combine(drive.RootDirectory.FullName, "competition.xyz"));

                    foreach (var xyz in Directory.GetFiles(drive.RootDirectory.FullName, "*.xyz", SearchOption.TopDirectoryOnly))
                        AddCandidate(candidates, xyz);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AirRace: Could not scan removable drives for track files: {ex.Message}");
            }
#endif
        }

        bool TryLoadFileCandidate(string candidate)
        {
            try
            {
                if (Directory.Exists(candidate))
                {
                    var track = Path.Combine(candidate, "track.txt");
                    if (File.Exists(track))
                        return TryParse(File.ReadAllText(track), track);

                    var xyzFiles = Directory.GetFiles(candidate, "*.xyz", SearchOption.TopDirectoryOnly);
                    if (xyzFiles.Length > 0)
                        return TryParse(File.ReadAllText(xyzFiles[0]), xyzFiles[0]);
                }

                if (File.Exists(candidate))
                    return TryParse(File.ReadAllText(candidate), candidate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AirRace: Failed to read track candidate '{candidate}': {ex.Message}");
            }

            return false;
        }

        IEnumerator LoadStreamingTrack(string path)
        {
            if (path.Contains("://") || path.Contains(":///"))
            {
                using (var request = UnityWebRequest.Get(path))
                {
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                        TryParse(request.downloadHandler.text, path);
                }
            }
            else if (File.Exists(path))
            {
                TryParse(File.ReadAllText(path), path);
            }
        }

        bool TryParse(string content, string source)
        {
            var parsed = new List<Vector3>();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < lines.Length && parsed.Count < maxCheckpoints; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                {
                    Debug.LogWarning($"AirRace: Ignoring invalid track line {i + 1} in {source}: '{line}'");
                    continue;
                }

                if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                    !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
                    !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                {
                    Debug.LogWarning($"AirRace: Ignoring unparsable track line {i + 1} in {source}: '{line}'");
                    continue;
                }

                parsed.Add(new Vector3(x, y, z) * coordinateScale);
            }

            if (parsed.Count == 0)
                return false;

            m_Checkpoints.Clear();
            m_Checkpoints.AddRange(parsed);
            LoadedTrackSource = source;
            Debug.Log($"AirRace: Loaded {m_Checkpoints.Count} checkpoints from {source}");
            return true;
        }
    }
}
