using System.Text;

namespace Tdt.Core.Torrent;

/// <summary>
/// Decodes bencode strings, preferring UTF-8 and falling back to Latin-1 for
/// the older torrents that store platform code pages.
/// </summary>
internal static class TextDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string Decode(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
