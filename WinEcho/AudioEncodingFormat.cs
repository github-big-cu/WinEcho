using System;

namespace WinEcho
{
    public enum AudioEncodingFormat
    {
        Mp3,
        Mp4,
        Wma
    }

    public static class AudioEncodingFormatExtensions
    {
        public static string ToFileExtension(this AudioEncodingFormat encodingFormat)
        {
            switch (encodingFormat)
            {
                case AudioEncodingFormat.Mp3:
                    return ".mp3";
                case AudioEncodingFormat.Mp4:
                    return ".mp4";
                case AudioEncodingFormat.Wma:
                    return ".wma";
                default:
                    throw new ArgumentOutOfRangeException("encodingFormat");
            }
        }
    }
}
