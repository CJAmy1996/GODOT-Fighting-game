using System;
using Godot;
using ModularFighter.Core;

namespace ModularFighter.Tests;

public partial class ChainResolverRegressionTest : Node
{
	public override void _Ready()
	{
		try
		{
			ChainResolutionContext allowedNormal = BaseContext with
			{
				CurrentMoveHasHit = true,
				IsInsideCurrentMoveWindow = true,
				AuthoredNormalTargetAllowed = true
			};
			Expect(ChainResolver.CanChain(allowedNormal), "authored on-hit normal chain was rejected");
			Expect(!ChainResolver.CanChain(allowedNormal with { IsShortHopNormalChain = true }),
				"short-hop normal chain bypassed its route veto");
			Expect(!ChainResolver.CanChain(allowedNormal with { NextMoveMaximumUses = 1, NextMoveUseCount = 1 }),
				"normal-use cap was ignored");
			Expect(ChainResolver.CanChain(BaseContext with { IsRekkaFollowup = true, RekkaStartupComplete = true }),
				"legal rekka followup was rejected");
			Expect(ChainResolver.CanChain(BaseContext with { IsCommandRunFollowup = true }),
				"command-run followup was rejected");
			Expect(ChainResolver.CanChain(BaseContext with
			{
				NextMoveIsSpecial = true,
				CurrentMoveCanChainToSpecial = true,
				CurrentMoveHasHit = true,
				IsInsideCurrentMoveWindow = true
			}), "authored special chain was rejected");
			Expect(ChainResolver.CanChain(BaseContext with
			{
				NextMoveIsSpecial = true,
				GlobalSpecialCancelAllowed = true
			}), "global special cancel fallback was rejected");
			Expect(!ChainResolver.CanChain(allowedNormal with { CurrentMoveHasHit = false }),
				"contact-required normal chain was allowed on whiff");
			GD.Print("CHAIN_RESOLVER_TEST_PASS short_hop=blocked use_cap=blocked rekka=preserved command_run=preserved normal_on_hit=allowed special_fallback=preserved");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"CHAIN_RESOLVER_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static ChainResolutionContext BaseContext => new(
		IsShortHopNormalChain: false,
		NextMoveMaximumUses: 0,
		NextMoveUseCount: 0,
		IsRekkaFollowup: false,
		RekkaStartupComplete: false,
		IsCommandRunFollowup: false,
		NextMoveIsSpecial: false,
		CurrentMoveCanChainToSpecial: false,
		ChainRequiresContact: true,
		CurrentMoveHasHit: false,
		IsInsideCurrentMoveWindow: false,
		GlobalSpecialCancelAllowed: false,
		AuthoredNormalTargetAllowed: false);

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
