using System.Text;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// Tree-sitter's C API operates on UTF-8 bytes: ts_parser_parse_string wants
/// a UTF-8 byte buffer plus a UTF-8 byte length (not a .NET string's UTF-16
/// char count), and ts_node_start_byte/end_byte return UTF-8 byte offsets.
/// AvaloniaEdit's CaretOffset/SelectionStart are UTF-16 char offsets. This
/// type encodes a source string to UTF-8 once and converts between the two
/// offset spaces on demand - correct for any content, not just ASCII (where
/// the two would coincidentally match).
/// </summary>
public sealed class Utf8Text
{
    private readonly string _text;

    public Utf8Text(string text)
    {
        _text = text;
        Bytes = Encoding.UTF8.GetBytes(text);
    }

    /// <summary>The UTF-8 encoded source, ready to pass to ts_parser_parse_string/ts_query_new.</summary>
    public byte[] Bytes { get; }

    public uint ByteLength => (uint)Bytes.Length;

    /// <summary>Converts a UTF-16 char offset (e.g. AvaloniaEdit's CaretOffset) to the corresponding UTF-8 byte offset (e.g. for ts_node_descendant_for_byte_range).</summary>
    public int CharOffsetToByteOffset(int charOffset) =>
        Encoding.UTF8.GetByteCount(_text.AsSpan(0, charOffset));

    /// <summary>Converts a UTF-8 byte offset (e.g. from ts_node_start_byte) to the corresponding UTF-16 char offset (e.g. for AvaloniaEdit's CaretOffset).</summary>
    public int ByteOffsetToCharOffset(int byteOffset) =>
        Encoding.UTF8.GetCharCount(Bytes, 0, byteOffset);
}
