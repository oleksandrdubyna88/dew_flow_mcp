using System.Text;
using FluentAssertions;
using Mcp.Application;
using Xunit;

namespace Mcp.Tests;

/// <summary>The budget is applied at EMIT, so these are the rules that decide what a spool can ever
/// contain. A budget enforced later is a policy about deleting data somebody already has.</summary>
public sealed class PayloadBudgetTests
{
    [Fact]
    public void A_payload_within_the_budget_is_kept_whole_and_reports_no_loss()
    {
        var (text, dropped) = PayloadBudget.Apply("small enough", 4096);

        text.Should().Be("small enough");
        dropped.Should().Be(0);
    }

    [Fact]
    public void A_payload_exactly_on_the_budget_is_not_marked_truncated()
    {
        var exact = new string('x', 64);

        var (text, dropped) = PayloadBudget.Apply(exact, 64);

        // The boundary matters: an off-by-one here would flag every full-size payload as lossy and
        // make "truncated" a field nobody trusts.
        text.Should().Be(exact);
        dropped.Should().Be(0);
    }

    [Fact]
    public void An_oversized_payload_is_cut_to_the_budget_and_the_loss_is_exact()
    {
        var big = new string('y', 500);

        var (text, dropped) = PayloadBudget.Apply(big, 100);

        Encoding.UTF8.GetByteCount(text).Should().BeLessThanOrEqualTo(100);
        (Encoding.UTF8.GetByteCount(text) + dropped).Should().Be(500, "every byte is either kept or counted");
    }

    [Fact]
    public void Multi_byte_text_is_measured_in_bytes_not_characters()
    {
        // 50 characters, 100 bytes in UTF-8 — a character-counted budget would keep twice the storage
        // it promised, which is the whole reason the budget is a byte budget.
        var cyrillic = new string('я', 50);

        var (text, dropped) = PayloadBudget.Apply(cyrillic, 60);

        Encoding.UTF8.GetByteCount(text).Should().BeLessThanOrEqualTo(60);
        dropped.Should().BeGreaterThan(0);
    }

    [Fact]
    public void The_cut_never_splits_a_surrogate_pair()
    {
        // Each emoji is one surrogate PAIR and four UTF-8 bytes. A budget landing mid-pair must step
        // back: half a pair is not a character, and a consumer decoding it gets a replacement glyph
        // rather than a shorter string.
        var emoji = string.Concat(Enumerable.Repeat("😀", 10));

        var (text, _) = PayloadBudget.Apply(emoji, 10);

        text.Should().NotBeEmpty();
        char.IsHighSurrogate(text[^1]).Should().BeFalse("a lone high surrogate would end the string mid-character");
        Encoding.UTF8.GetByteCount(text).Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public void A_zero_budget_keeps_nothing_and_still_counts_what_it_dropped()
    {
        var (text, dropped) = PayloadBudget.Apply("anything at all", 0);

        // Turning capture off must not also turn the accounting off — otherwise a host configured to
        // store no payloads is indistinguishable from a host receiving no payloads.
        text.Should().BeEmpty();
        dropped.Should().Be("anything at all".Length);
    }
}
