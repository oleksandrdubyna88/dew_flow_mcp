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

        // The cheap accept, BEFORE any counting — this is the whole hot path. Called twice per tool
        // call for payloads that almost always fit, a full GetByteCount just to learn "yes it fits" is
        // work paid on every call for an answer the length already implies.
        if (CertainlyFits(text.Length, budgetBytes))
        {
            return (text, 0);
        }

        var total = Encoding.UTF8.GetByteCount(text);
        if (total <= budgetBytes)
        {
            return (text, 0);
        }

        var (kept, keptBytes) = LongestPrefixWithin(text, budgetBytes);
        return (kept, total - keptBytes);
    }

    /// <summary>Whether the budget is provably safe without counting a single byte.
    /// <para>The bound has to be the UPPER one, and naming the wrong one is an easy mistake: characters
    /// bound bytes from BELOW (<c>bytes >= chars</c>), which proves only the reject case. What proves
    /// "fits" is that UTF-8 never spends more than <b>3</b> bytes per <c>char</c> — a 4-byte codepoint
    /// arrives as a surrogate PAIR, so it is 2 bytes per char, not 4.</para></summary>
    private static bool CertainlyFits(int characters, int budgetBytes) =>
        (long)characters * MaxBytesPerChar <= budgetBytes;

    private const int MaxBytesPerChar = 3;

    /// <summary>The longest prefix whose UTF-8 encoding fits the budget, and how many bytes that is.
    /// <para>One forward pass, accumulating as it goes. It replaces a binary search that called
    /// <see cref="Encoding.UTF8"/>'s counter afresh over a growing prefix at every step — O(n log n) to
    /// answer a question a single O(n) walk answers, and then a THIRD full count to report the loss.
    /// The walk carries its own total, so the count is paid once.</para>
    /// <para>It never splits a surrogate pair, structurally rather than by a correction afterwards: the
    /// pair is consumed as one unit or not at all. Half a pair is not a character, and a consumer
    /// decoding it gets a replacement glyph instead of a truncated string.</para></summary>
    private static (string Text, int Bytes) LongestPrefixWithin(string text, int budgetBytes)
    {
        var bytes = 0;
        var index = 0;

        while (index < text.Length)
        {
            var characters = char.IsHighSurrogate(text[index]) && index + 1 < text.Length ? 2 : 1;
            var cost = Utf8Bytes(text[index], characters);
            if (bytes + cost > budgetBytes)
            {
                break;
            }

            bytes += cost;
            index += characters;
        }

        return (text[..index], bytes);
    }

    /// <summary>What one codepoint costs in UTF-8. A lone surrogate — which is not a codepoint at all —
    /// falls into the 3-byte arm, which is what the encoder charges for the replacement character it
    /// substitutes.</summary>
    private static int Utf8Bytes(char first, int characters) => characters == 2
        ? 4
        : first switch
        {
            < (char)0x80 => 1,
            < (char)0x800 => 2,
            _ => 3,
        };
}
