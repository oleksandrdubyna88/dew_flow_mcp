using System.Text;

namespace Mcp.Contracts;

/// <summary>Cuts a payload to a byte budget and says exactly how much it dropped.
/// <para>
/// It lives in the contracts — the one project every other one can see — because two unrelated
/// modules need exactly this: <c>ToolCatalog</c> bounds what telemetry STORES, and the sandboxed
/// reader bounds what one read HANDS BACK. Neither may reference the other, so the shared half
/// belongs in their common ancestor rather than being written twice and drifting.
/// </para>
/// <para>
/// The budget is applied at EMIT, not by a later clean-up job: full arguments and full responses
/// across all traffic is the largest table in any system that keeps them, and a retention policy
/// written after the first write is a policy about deleting data somebody already has.
/// </para>
/// <para>
/// Bytes, not characters, because the budget is about storage; and the cut never splits a surrogate
/// pair, because half a pair is not a character and a consumer decoding it gets a replacement glyph
/// instead of a truncated string.
/// </para></summary>
public static class PayloadBudget
{
    public static (string Text, int TruncatedBytes) Apply(string text, int budgetBytes)
    {
        if (string.IsNullOrEmpty(text) || budgetBytes <= 0)
        {
            return (string.Empty, budgetBytes <= 0 ? Encoding.UTF8.GetByteCount(text ?? string.Empty) : 0);
        }

        var total = Encoding.UTF8.GetByteCount(text);
        if (total <= budgetBytes)
        {
            return (text, 0);
        }

        var kept = LongestPrefixWithin(text, budgetBytes);
        return (kept, total - Encoding.UTF8.GetByteCount(kept));
    }

    /// <summary>The longest prefix whose UTF-8 encoding fits the budget. Binary search over CHARACTERS
    /// rather than a byte-by-byte walk: an oversized argument object is the normal case here, and the
    /// walk is linear in a payload we already decided is too big to keep.</summary>
    private static string LongestPrefixWithin(string text, int budgetBytes)
    {
        var low = 0;
        var high = text.Length;

        while (low < high)
        {
            var middle = WholeCharacterBoundary(text, low + ((high - low + 1) / 2));
            if (middle <= low)
            {
                break;
            }

            if (Encoding.UTF8.GetByteCount(text.AsSpan(0, middle)) <= budgetBytes)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return text[..WholeCharacterBoundary(text, low)];
    }

    /// <summary>Steps back off the high half of a surrogate pair.</summary>
    private static int WholeCharacterBoundary(string text, int index) =>
        index > 0 && index < text.Length && char.IsHighSurrogate(text[index - 1]) ? index - 1 : index;
}
