using System;
using UnityEngine;

namespace Unity.Audio
{
    /// <summary>
    /// Utility class for converting between <see cref="AudioSpeakerMode"/>, <see cref="SoundFormat"/>, and channel count
    /// </summary>
    public struct ChannelEnumConverter
    {
        /// <summary>
        /// Convert an AudioSpeakerMode to a SoundFormat
        /// </summary>
        /// <param name="mode">The speaker mode to convert</param>
        /// <returns>The equivalent sound format</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="mode"/> is invalid or unknown"</exception>
        public static SoundFormat GetSoundFormatFromSpeakerMode(AudioSpeakerMode mode)
        {
            switch (mode)
            {
                case AudioSpeakerMode.Mono:
                    return SoundFormat.Mono;
                case AudioSpeakerMode.Stereo:
                    return SoundFormat.Stereo;
                case AudioSpeakerMode.Quad:
                    return SoundFormat.Quad;
                case AudioSpeakerMode.Surround:
                    return SoundFormat.Surround;
                case AudioSpeakerMode.Mode5point1:
                    return SoundFormat.FiveDot1;
                case AudioSpeakerMode.Mode7point1:
                    return SoundFormat.SevenDot1;
                case AudioSpeakerMode.Prologic:
                    return SoundFormat.Raw;
                default:
                    throw new ArgumentException($"Invalid speaker mode {mode}", nameof(mode));
            }
        }

        /// <summary>
        /// Get the channel count for a <see cref="SoundFormat"/>
        /// </summary>
        /// <param name="format">The sound format whose channel count is desired</param>
        /// <returns>The number of channels for the provided format</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="format"/> is invalid or unknown</exception>
        public static int GetChannelCountFromSoundFormat(SoundFormat format)
        {
            switch (format)
            {
                case SoundFormat.Raw:
                    return 2;
                case SoundFormat.Mono:
                    return 1;
                case SoundFormat.Stereo:
                    return 2;
                case SoundFormat.Quad:
                    return 4;
                case SoundFormat.Surround:
                    return 5;
                case SoundFormat.FiveDot1:
                    return 6;
                case SoundFormat.SevenDot1:
                    return 8;
                default:
                    throw new ArgumentException($"Invalid sound format {format}", nameof(format));
            }
        }
    }
}
