using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DansToolbox.EditorTools.Audio
{
    internal static class EditorAudioPreviewService
    {
        private const string AudioUtilityTypeName = "UnityEditor.AudioUtil";
        private const string PlayMethodName = "PlayPreviewClip";
        private const string StopAllMethodName = "StopAllPreviewClips";
        private const string IsPlayingMethodName = "IsPreviewClipPlaying";
        private const string UpdateAudioMethodName = "UpdateAudio";

        private static readonly BindingFlags StaticMethodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type audioUtilityType;
        private static MethodInfo playPreviewMethod;
        private static MethodInfo stopAllPreviewMethod;
        private static MethodInfo isPreviewPlayingMethod;
        private static MethodInfo updateAudioMethod;

        /// <summary>Plays an in-memory clip through Unity's Editor audio-preview service.</summary>
        public static bool TryPlay(AudioClip clip, out string failureReason)
        {
            return TryPlay(clip, 0, false, out failureReason);
        }

        /// <summary>Plays an Editor preview from a sample offset, optionally looping the clip.</summary>
        public static bool TryPlay(
            AudioClip clip,
            int startSample,
            bool loop,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (clip == null || clip.samples == 0)
            {
                failureReason = "No audio clip was generated.";
                return false;
            }

            if (EditorUtility.audioMasterMute)
            {
                failureReason = "Unity Editor audio is muted. Turn off the speaker mute toggle in the Game view toolbar.";
                return false;
            }

            CacheMethods();
            if (playPreviewMethod == null)
            {
                failureReason = "Unity Editor's AudioUtil.PlayPreviewClip method was not found.";
                return false;
            }

            try
            {
                Stop();
                playPreviewMethod.Invoke(
                    null,
                    new object[] { clip, Mathf.Clamp(startSample, 0, clip.samples - 1), loop });
                updateAudioMethod?.Invoke(null, null);

                if (isPreviewPlayingMethod != null && !IsPlaying(clip))
                {
                    failureReason = "Unity accepted the clip but did not start Editor audio playback.";
                    return false;
                }

                return true;
            }
            catch (TargetInvocationException exception)
            {
                failureReason = exception.InnerException?.Message ?? exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
        }

        /// <summary>Stops any active Unity Editor audio preview.</summary>
        public static void Stop()
        {
            CacheMethods();
            try
            {
                stopAllPreviewMethod?.Invoke(null, null);
            }
            catch (TargetInvocationException)
            {
                // Stopping is cleanup only; a failed stop must not block a new preview.
            }
        }

        /// <summary>Checks whether Unity's preview service reports the clip as playing.</summary>
        public static bool IsPlaying(AudioClip clip)
        {
            CacheMethods();
            if (clip == null || isPreviewPlayingMethod == null)
            {
                return false;
            }

            try
            {
                object[] arguments = isPreviewPlayingMethod.GetParameters().Length == 0
                    ? null
                    : new object[] { clip };
                return (bool)isPreviewPlayingMethod.Invoke(null, arguments);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void CacheMethods()
        {
            if (audioUtilityType != null)
            {
                return;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                audioUtilityType = assembly.GetType(AudioUtilityTypeName);
                if (audioUtilityType != null)
                {
                    break;
                }
            }

            if (audioUtilityType == null)
            {
                return;
            }

            playPreviewMethod = audioUtilityType.GetMethod(PlayMethodName, StaticMethodFlags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            stopAllPreviewMethod = audioUtilityType.GetMethod(StopAllMethodName, StaticMethodFlags);
            isPreviewPlayingMethod =
                audioUtilityType.GetMethod(IsPlayingMethodName, StaticMethodFlags, null, Type.EmptyTypes, null) ??
                audioUtilityType.GetMethod(IsPlayingMethodName, StaticMethodFlags, null, new[] { typeof(AudioClip) }, null);
            updateAudioMethod = audioUtilityType.GetMethod(UpdateAudioMethodName, StaticMethodFlags, null, Type.EmptyTypes, null);
        }
    }
}
