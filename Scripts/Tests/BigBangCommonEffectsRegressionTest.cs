using System;
using System.Linq;
using Godot;
using ModularFighter.Demo;

namespace ModularFighter.Tests;

public partial class BigBangCommonEffectsRegressionTest : Node2D
{
	public override void _Ready()
	{
		try
		{
			var layer = new HitSparkLayer { Name = "EffectsUnderTest" };
			AddChild(layer);

			Vector2 contact = new(310f, 205f);
			layer.Spawn(contact, heavy: false, facing: 1);
			AuthoredHitSpark weak = layer.GetChildren().OfType<AuthoredHitSpark>().LastOrDefault();
			Expect(weak != null, "weak hit did not instantiate the cropped authored impact");
			Expect(weak.GlobalPosition.IsEqualApprox(contact), "weak hit is not anchored to the collision point");
			Expect(weak.Animation == "hit_impact_062_069" && weak.SpriteFrames.GetFrameCount(weak.Animation) == 8,
				"weak hit is not using the complete cropped PNG 062-069 animation");
			Expect(weak.FlipH, "gameplay hit impact was not flipped horizontally");
			Expect(weak.Material is ShaderMaterial weakMaterial && weakMaterial.Shader.Code.Contains("dominant_green"),
				"cropped impact is missing its green-key material");

			layer.Spawn(contact, heavy: true, facing: -1);
			AuthoredHitSpark strong = layer.GetChildren().OfType<AuthoredHitSpark>().LastOrDefault();
			Expect(strong != null && strong != weak && strong.Animation == weak.Animation && strong.FlipH,
				"strong hit did not share the flipped cropped gameplay impact");

			layer.SpawnBlockShield(contact, defenderFacing: 1);
			BigBangCommonEffect guard = FindNewest(layer, BigBangCommonEffectKind.GuardImpact);
			Expect(guard != null && guard.CurrentSourceFrame == 192 && guard.TotalTicks == 22,
				"guard impact did not load BBB action 13");
			Expect(guard.CurrentTextureFrame == 237 && !guard.UsesAdditiveBlackKey,
				"guard did not resolve KIR 192 to solid PNG 237 with only green chroma removed");
			Expect(Mathf.IsEqualApprox(guard.CurrentAuthoredScale, 0.3f),
				$"guard impact began at {guard.CurrentAuthoredScale:P0}, expected 30%");
			for (int i = 0; i < 7; i++) guard.AdvanceOneTick();
			Expect(Mathf.IsEqualApprox(guard.CurrentAuthoredScale, 1f),
				$"guard impact reached {guard.CurrentAuthoredScale:P0}, expected 100% on source tick 7");
			Expect(guard.CurrentSourceFrame == 192, "guard opening drawing did not retain its eight-tick hold");
			guard.AdvanceOneTick();
			Expect(guard.CurrentSourceFrame == 193 && guard.CurrentTextureFrame == 238 &&
				Mathf.IsEqualApprox(guard.CurrentAuthoredScale, 1f),
				"guard did not enter drawing 193 at fixed 100% scale on source tick 8");

			PackedScene bStoneScene = GD.Load<PackedScene>("res://Effects/BigBangBStonePickup230To236.tscn");
			BigBangCommonEffect bStone = bStoneScene?.Instantiate<BigBangCommonEffect>();
			Expect(bStone != null, "B-Stone pickup scene did not load separately from Guard");
			AddChild(bStone);
			Expect(bStone.EffectKind == BigBangCommonEffectKind.BStonePickup230To236 &&
				bStone.CurrentSourceFrame == 185 && bStone.CurrentTextureFrame == 230 &&
				bStone.TotalTicks == 7 && bStone.UsesAdditiveBlackKey,
				"B-Stone pickup did not begin on KIR 185 / PNG 230 with legacy black keying");
			Expect(bStone.EffectSprite?.Material is ShaderMaterial bStoneMaterial &&
				bStoneMaterial.Shader.Code.Contains("blend_add") &&
				bStoneMaterial.Shader.Code.Contains("black_key_alpha"),
				"B-Stone particles retained opaque black spots instead of source additive treatment");

			PackedScene airDashParticleScene = GD.Load<PackedScene>("res://Effects/BigBangAirDashParticle272.tscn");
			BigBangCommonEffect airDashParticle = airDashParticleScene?.Instantiate<BigBangCommonEffect>();
			AddChild(airDashParticle);
			Expect(airDashParticle != null && airDashParticle.CurrentSourceFrame == 212 &&
				airDashParticle.CurrentTextureFrame == 272 && airDashParticle.TotalTicks == 30 &&
				airDashParticle.UsesAdditiveBlackKey &&
				Mathf.IsEqualApprox(airDashParticle.CurrentAuthoredScale, 0.7f),
				"air-dash particle did not preserve KIR 212 / PNG 272 for 30 ticks at 70% scale");

			PackedScene particleBScene = GD.Load<PackedScene>("res://Effects/BigBangParticleB273.tscn");
			BigBangCommonEffect particleB = particleBScene?.Instantiate<BigBangCommonEffect>();
			AddChild(particleB);
			Expect(particleB != null && particleB.CurrentSourceFrame == 213 &&
				particleB.CurrentTextureFrame == 273 && particleB.TotalTicks == 60 &&
				particleB.UsesAdditiveBlackKey,
				"Particle B did not preserve KIR 213 / PNG 273 for its 60-tick source hold");

			PackedScene unusedCoreScene = GD.Load<PackedScene>("res://Effects/BigBangUnusedCore274.tscn");
			BigBangCommonEffect unusedCore = unusedCoreScene?.Instantiate<BigBangCommonEffect>();
			AddChild(unusedCore);
			Expect(unusedCore != null && unusedCore.CurrentSourceFrame == 214 &&
				unusedCore.CurrentTextureFrame == 274 && unusedCore.UsesAdditiveBlackKey,
				"unreferenced KIR 214 / PNG 274 was not preserved separately");

			PackedScene energyRingScene = GD.Load<PackedScene>(
				"res://Effects/BigBangEnergyRingCandidate275To280.tscn");
			BigBangCommonEffect energyRing = energyRingScene?.Instantiate<BigBangCommonEffect>();
			AddChild(energyRing);
			Expect(energyRing != null && energyRing.CurrentSourceFrame == 215 &&
				energyRing.CurrentTextureFrame == 275 && energyRing.TotalTicks == 12 &&
				energyRing.UsesAdditiveBlackKey,
				"unassigned energy ring did not preserve KIR 215-220 / PNG 275-280 separately");

			layer.SpawnDust(new Vector2(500f, 400f), particleCount: 7, spread: 42f);
			BigBangCommonEffect[] dust = layer.GetChildren().OfType<BigBangCommonEffect>()
				.Where(effect => effect.EffectKind == BigBangCommonEffectKind.Dust).ToArray();
			Expect(dust.Length == 2, $"density 7 mapped to {dust.Length} smoke emitters instead of 2");
			Expect(dust.All(effect => effect.TotalTicks == 30),
				"dust is not using BBB smoke action 2's ten 3-tick drawings");
			BigBangCommonEffect runDust = dust.OrderBy(effect => effect.DelayTicks).First();
			Expect(runDust.CurrentSourceFrame == 85 && runDust.CurrentTextureFrame == 90,
				"immediate run dust did not resolve Smoke 2 KIR drawing 85 to PNG 090");
			for (int i = 0; i < 3; i++) runDust.AdvanceOneTick();
			Expect(runDust.CurrentSourceFrame == 86 && runDust.CurrentTextureFrame == 91,
				"run dust did not retain PNG 090 for three ticks before advancing to PNG 091");

			Vector2 jumpGround = new(420f, 320f);
			BigBangCommonEffect jumpStart = layer.SpawnJumpStart(jumpGround, fighterFacing: -1);
			Expect(jumpStart != null && jumpStart.EffectKind == BigBangCommonEffectKind.JumpStart,
				"ground jump did not instantiate common source section 2");
			Expect(jumpStart.GlobalPosition.IsEqualApprox(jumpGround + new Vector2(0f, 10f)),
				$"jump-start effect ignored the source O (0,10) offset: {jumpStart.GlobalPosition}");
			Expect(jumpStart.CurrentSourceFrame == 95 && jumpStart.TotalTicks == 8,
				$"jump-start source timeline is {jumpStart.CurrentSourceFrame}/{jumpStart.TotalTicks}, expected 95/8");
			for (int i = 0; i < 5; i++) jumpStart.AdvanceOneTick();
			Expect(jumpStart.CurrentSourceFrame == 100 && jumpStart.CurrentTextureFrame == 99,
				"missing source drawing 100 is not retaining the documented 099 placeholder texture");

			BigBangCommonEffect superJumpStart = layer.SpawnJumpStart(
				jumpGround, fighterFacing: 1, isSuperJump: true);
			Expect(superJumpStart != null &&
				superJumpStart.EffectKind == BigBangCommonEffectKind.SuperJump111To118,
				"super jump did not select the dedicated PNG 111-118 takeoff effect");
			Expect(superJumpStart.GlobalPosition.IsEqualApprox(jumpGround + new Vector2(0f, 10f)),
				$"super-jump effect ignored the source O (0,10) offset: {superJumpStart.GlobalPosition}");
			Expect(superJumpStart.CurrentSourceFrame == 95 &&
				superJumpStart.CurrentTextureFrame == 111 && superJumpStart.TotalTicks == 8,
				"super-jump effect did not begin on KIR 95 / PNG 111 with an eight-tick timeline");
			for (int texture = 112; texture <= 118; texture++)
			{
				superJumpStart.AdvanceOneTick();
				Expect(superJumpStart.CurrentSourceFrame == texture - 16 &&
					superJumpStart.CurrentTextureFrame == texture,
					$"super-jump effect did not advance to KIR {texture - 16} / PNG {texture}");
			}

			PackedScene numberedScene = GD.Load<PackedScene>(
				"res://Effects/BigBangNumbered000To015.tscn");
			BigBangCommonEffect numbered = numberedScene?.Instantiate<BigBangCommonEffect>();
			Expect(numbered != null, "numbered common animation scene did not load");
			AddChild(numbered);
			Expect(numbered.EffectKind == BigBangCommonEffectKind.Numbered000To015 &&
				numbered.CurrentSourceFrame == 0 && numbered.TotalTicks == 32,
				"numbered sequence did not begin at 000 with sixteen two-tick drawings");
			for (int frame = 1; frame <= 15; frame++)
			{
				numbered.AdvanceOneTick();
				Expect(numbered.CurrentSourceFrame == frame - 1,
					$"numbered drawing {frame - 1:D3} did not retain its second tick");
				numbered.AdvanceOneTick();
				Expect(numbered.CurrentSourceFrame == frame,
					$"numbered sequence skipped expected drawing {frame:D3}");
			}
			SpriteFrames numberedFrames = GD.Load<SpriteFrames>(
				"res://Assets/Effects/BigBangCommon/numbered_000_015_frames.tres");
			StringName numberedAnimation = "numbered_000_015";
			Expect(numberedFrames != null && numberedFrames.HasAnimation(numberedAnimation) &&
				numberedFrames.GetFrameCount(numberedAnimation) == 16 &&
				numberedFrames.GetAnimationLoop(numberedAnimation) &&
				Mathf.IsEqualApprox((float)numberedFrames.GetAnimationSpeed(numberedAnimation), 60f),
				"loopable numbered SpriteFrames resource is not 16 drawings at 60 Hz");
			for (int frame = 0; frame < 16; frame++)
			{
				Texture2D texture = numberedFrames.GetFrameTexture(numberedAnimation, frame);
				Expect(texture?.ResourcePath.EndsWith($"/{frame:D3}.png") == true &&
					Mathf.IsEqualApprox((float)numberedFrames.GetFrameDuration(numberedAnimation, frame), 2f),
					$"SpriteFrames slot {frame} is not numbered drawing {frame:D3} held for two ticks");
			}

			PackedScene blockCandidateScene = GD.Load<PackedScene>(
				"res://Effects/BigBangBlockCandidate039To045.tscn");
			BigBangCommonEffect blockCandidate = blockCandidateScene?.Instantiate<BigBangCommonEffect>();
			Expect(blockCandidate != null, "039-045 block candidate scene did not load");
			AddChild(blockCandidate);
			Expect(blockCandidate.EffectKind == BigBangCommonEffectKind.BlockCandidate039To045 &&
				blockCandidate.CurrentSourceFrame == 39 && blockCandidate.TotalTicks == 14,
				"039-045 block candidate did not begin with seven two-tick drawings");
			for (int frame = 40; frame <= 45; frame++)
			{
				blockCandidate.AdvanceOneTick();
				Expect(blockCandidate.CurrentSourceFrame == frame - 1,
					$"block candidate drawing {frame - 1:D3} did not retain its second tick");
				blockCandidate.AdvanceOneTick();
				Expect(blockCandidate.CurrentSourceFrame == frame,
					$"block candidate skipped expected drawing {frame:D3}");
			}

			PackedScene groundBounceScene = GD.Load<PackedScene>(
				"res://Effects/BigBangGroundBounce055To061.tscn");
			BigBangCommonEffect groundBounce = groundBounceScene?.Instantiate<BigBangCommonEffect>();
			Expect(groundBounce != null, "source ground-bounce scene did not load");
			AddChild(groundBounce);
			Expect(groundBounce.EffectKind == BigBangCommonEffectKind.GroundBounce055To061 &&
				groundBounce.CurrentSourceFrame == 55 && groundBounce.TotalTicks == 14 &&
				groundBounce.CurrentSourceOrigin.IsEqualApprox(new Vector2(0f, 24f)),
				"source ground bounce did not begin on 055 at its authored ground origin");
			for (int frame = 56; frame <= 61; frame++)
			{
				groundBounce.AdvanceOneTick();
				Expect(groundBounce.CurrentSourceFrame == frame - 1,
					$"ground-bounce drawing {frame - 1:D3} did not retain its second tick");
				groundBounce.AdvanceOneTick();
				Expect(groundBounce.CurrentSourceFrame == frame,
					$"ground-bounce sequence skipped expected drawing {frame:D3}");
			}
			Expect(groundBounce.CurrentSourceOrigin.IsEqualApprox(new Vector2(-4f, 28f)),
				"ground-bounce drawing 061 did not use its source-authored final origin");

			PackedScene burstScene = GD.Load<PackedScene>("res://Effects/BigBangBurst071To079.tscn");
			BigBangCommonEffect burst = burstScene?.Instantiate<BigBangCommonEffect>();
			Expect(burst != null, "source Burst scene did not load");
			AddChild(burst);
			Expect(burst.EffectKind == BigBangCommonEffectKind.Burst071To079 &&
				burst.CurrentSourceFrame == 66 && burst.CurrentTextureFrame == 71 &&
				burst.TotalTicks == 9 && burst.CurrentSourceOrigin.IsEqualApprox(new Vector2(-41f, 272f)),
				"source Burst did not begin on KIR 66 / PNG 071 at its authored origin");
			for (int texture = 72; texture <= 79; texture++)
			{
				burst.AdvanceOneTick();
				Expect(burst.CurrentSourceFrame == texture - 5 && burst.CurrentTextureFrame == texture,
					$"Burst did not advance to KIR {texture - 5} / PNG {texture:D3}");
			}
			Expect(burst.CurrentSourceOrigin.IsEqualApprox(new Vector2(-46f, 301f)),
				"Burst PNG 079 did not use its source-authored final origin");

			PackedScene smallDustScene = GD.Load<PackedScene>("res://Effects/BigBangSmallDust080To089.tscn");
			BigBangCommonEffect smallDust = smallDustScene?.Instantiate<BigBangCommonEffect>();
			Expect(smallDust != null, "source Smoke 1 scene did not load");
			AddChild(smallDust);
			Expect(smallDust.EffectKind == BigBangCommonEffectKind.SmallDust080To089 &&
				smallDust.CurrentSourceFrame == 75 && smallDust.CurrentTextureFrame == 80 &&
				smallDust.TotalTicks == 20 && smallDust.CurrentSourceOrigin.IsEqualApprox(new Vector2(0f, 4f)),
				"Smoke 1 did not begin on KIR 75 / PNG 080 at its authored origin");
			for (int texture = 81; texture <= 89; texture++)
			{
				smallDust.AdvanceOneTick();
				Expect(smallDust.CurrentTextureFrame == texture - 1,
					$"Smoke 1 PNG {texture - 1:D3} did not retain its second tick");
				smallDust.AdvanceOneTick();
				Expect(smallDust.CurrentSourceFrame == texture - 5 && smallDust.CurrentTextureFrame == texture,
					$"Smoke 1 did not advance to KIR {texture - 5} / PNG {texture:D3}");
			}
			Expect(smallDust.CurrentSourceOrigin.IsEqualApprox(new Vector2(-6f, 0f)),
				"Smoke 1 PNG 089 did not use its source-authored final origin");

			PackedScene unassignedScene = GD.Load<PackedScene>(
				"res://Effects/BigBangUnassigned120To158.tscn");
			BigBangCommonEffect unassigned = unassignedScene?.Instantiate<BigBangCommonEffect>();
			Expect(unassigned != null, "unassigned KIR 104-127 scene did not load");
			AddChild(unassigned);
			Expect(unassigned.EffectKind == BigBangCommonEffectKind.Unassigned120To158 &&
				unassigned.CurrentSourceFrame == 104 && unassigned.CurrentTextureFrame == 120 &&
				unassigned.TotalTicks == 48 && unassigned.UsesAdditiveBlackKey,
				"unassigned common animation did not begin on KIR 104 / PNG 120 for 48 ticks");
			int[] unassignedTextures = { 120, 122, 124, 126, 128, 130, 132, 134,
				136, 138, 140, 142, 144, 146, 148, 150, 151, 152, 153, 154, 155, 156, 157, 158 };
			for (int drawing = 1; drawing < unassignedTextures.Length; drawing++)
			{
				unassigned.AdvanceOneTick();
				Expect(unassigned.CurrentTextureFrame == unassignedTextures[drawing - 1],
					$"unassigned PNG {unassignedTextures[drawing - 1]} did not retain its second tick");
				unassigned.AdvanceOneTick();
				Expect(unassigned.CurrentSourceFrame == 104 + drawing &&
					unassigned.CurrentTextureFrame == unassignedTextures[drawing],
					$"unassigned effect skipped KIR {104 + drawing} / PNG {unassignedTextures[drawing]}");
			}

			PackedScene techniqueHitScene = GD.Load<PackedScene>(
				"res://Effects/BigBangTechniqueHitRing161To168.tscn");
			BigBangCommonEffect techniqueHit = techniqueHitScene?.Instantiate<BigBangCommonEffect>();
			Expect(techniqueHit != null, "technique/throw hit ring scene did not load");
			AddChild(techniqueHit);
			Expect(techniqueHit.EffectKind == BigBangCommonEffectKind.TechniqueHitRing161To168 &&
				techniqueHit.CurrentSourceFrame == 128 && techniqueHit.CurrentTextureFrame == 161 &&
				techniqueHit.TotalTicks == 8 && techniqueHit.UsesAdditiveBlackKey &&
				techniqueHit.CurrentSourceOrigin.IsEqualApprox(new Vector2(0f, 320f)),
				"technique/throw hit ring did not begin on KIR 128 / PNG 161 at source origin (0,320)");
			Expect(techniqueHit.EffectSprite?.Material is ShaderMaterial techniqueMaterial &&
				techniqueMaterial.Shader.Code.Contains("blend_add") &&
				techniqueMaterial.Shader.Code.Contains("black_key_alpha"),
				"technique/throw hit ring retained its opaque black backing");
			for (int texture = 162; texture <= 168; texture++)
			{
				techniqueHit.AdvanceOneTick();
				Expect(techniqueHit.CurrentSourceFrame == texture - 33 &&
					techniqueHit.CurrentTextureFrame == texture,
					$"technique/throw hit ring skipped KIR {texture - 33} / PNG {texture}");
			}

			PackedScene additiveImpactScene = GD.Load<PackedScene>(
				"res://Effects/BigBangAdditiveImpact181To186.tscn");
			BigBangCommonEffect additiveImpact = additiveImpactScene?.Instantiate<BigBangCommonEffect>();
			Expect(additiveImpact != null, "additive impact KIR 136-141 scene did not load");
			AddChild(additiveImpact);
			Expect(additiveImpact.EffectKind == BigBangCommonEffectKind.AdditiveImpact181To186 &&
				additiveImpact.CurrentSourceFrame == 136 && additiveImpact.CurrentTextureFrame == 181 &&
				additiveImpact.TotalTicks == 12 && additiveImpact.UsesAdditiveBlackKey &&
				additiveImpact.CurrentSourceOrigin.IsEqualApprox(new Vector2(52f, 34f)),
				"additive impact did not begin on centered KIR 136 / PNG 181 for 12 ticks");
			Expect(additiveImpact.EffectSprite?.Material is ShaderMaterial additiveImpactMaterial &&
				additiveImpactMaterial.Shader.Code.Contains("blend_add") &&
				additiveImpactMaterial.Shader.Code.Contains("green_key") &&
				additiveImpactMaterial.Shader.Code.Contains("black_key_alpha"),
				"additive impact is not removing both green and black legacy backing");
			for (int texture = 182; texture <= 186; texture++)
			{
				additiveImpact.AdvanceOneTick();
				Expect(additiveImpact.CurrentTextureFrame == texture - 1,
					$"additive impact PNG {texture - 1} did not retain its second tick");
				additiveImpact.AdvanceOneTick();
				Expect(additiveImpact.CurrentSourceFrame == texture - 45 &&
					additiveImpact.CurrentTextureFrame == texture,
					$"additive impact skipped KIR {texture - 45} / PNG {texture}");
			}

			Vector2 wallContact = new(96f, 212f);
			BigBangCommonEffect wallJump = layer.SpawnWallJump(wallContact, launchDirection: 1);
			Expect(wallJump != null && wallJump.EffectKind == BigBangCommonEffectKind.WallJump189To196,
				"wall-jump API did not instantiate the reusable KIR 144-151 launch effect");
			Expect(wallJump.GlobalPosition.IsEqualApprox(wallContact) &&
				wallJump.CurrentSourceFrame == 144 && wallJump.CurrentTextureFrame == 189 &&
				wallJump.TotalTicks == 8 && wallJump.UsesAdditiveBlackKey &&
				wallJump.CurrentSourceOrigin.IsEqualApprox(new Vector2(6f, 12.5f)),
				"wall-jump launch did not begin at its exact wall contact on KIR 144 / PNG 189");
			Expect(wallJump.EffectSprite?.Material is ShaderMaterial wallJumpMaterial &&
				wallJumpMaterial.Shader.Code.Contains("blend_add") &&
				wallJumpMaterial.Shader.Code.Contains("green_key") &&
				wallJumpMaterial.Shader.Code.Contains("black_key_alpha"),
				"wall-jump launch lost legacy additive green/black keying");
			for (int texture = 190; texture <= 196; texture++)
			{
				wallJump.AdvanceOneTick();
				Expect(wallJump.CurrentSourceFrame == texture - 45 && wallJump.CurrentTextureFrame == texture,
					$"wall-jump launch skipped KIR {texture - 45} / PNG {texture}");
			}

			Vector2 wallHitContact = new(512f, 206f);
			BigBangCommonEffect wallHit = layer.SpawnWallSplat(
				wallHitContact, wallDirection: 1, scale: 1.65f);
			Expect(wallHit != null && wallHit.EffectKind == BigBangCommonEffectKind.WallHit198To204,
				"wall-splat API did not replace the procedural burst with BBB action 41");
			Expect(wallHit.GlobalPosition.IsEqualApprox(wallHitContact) &&
				wallHit.CurrentSourceFrame == 153 && wallHit.CurrentTextureFrame == 198 &&
				wallHit.TotalTicks == 14 && !wallHit.UsesAdditiveBlackKey &&
				wallHit.CurrentSourceOrigin.IsEqualApprox(new Vector2(0f, 80f)),
				"wall hit did not begin at the contact on KIR 153 / PNG 198 for 14 ticks");
			Expect(Mathf.IsEqualApprox(wallHit.PresentationScaleMultiplier, 1.65f) &&
				Mathf.IsEqualApprox(wallHit.Scale.X, -1.65f) && Mathf.IsEqualApprox(wallHit.Scale.Y, 1.65f),
				"right-wall hit was not mirrored inward at the requested impact scale");
			Expect(wallHit.EffectSprite?.Material is ShaderMaterial wallHitMaterial &&
				!wallHitMaterial.Shader.Code.Contains("blend_add") &&
				wallHitMaterial.Shader.Code.Contains("dominant_green"),
				"wall hit did not retain its solid purple/white art with only green keyed out");
			for (int texture = 199; texture <= 204; texture++)
			{
				wallHit.AdvanceOneTick();
				Expect(wallHit.CurrentTextureFrame == texture - 1,
					$"wall-hit PNG {texture - 1:D3} did not retain its second source tick");
				wallHit.AdvanceOneTick();
				Expect(wallHit.CurrentSourceFrame == texture - 45 && wallHit.CurrentTextureFrame == texture,
					$"wall hit skipped KIR {texture - 45} / PNG {texture}");
			}
			Expect(wallHit.CurrentSourceOrigin.IsEqualApprox(new Vector2(12f, 130f)),
				"wall-hit PNG 204 did not use its source-authored final wall origin");

			SpriteFrames wallHitFrames = GD.Load<SpriteFrames>(
				"res://Assets/Effects/BigBangCommon/wall_hit_198_204_frames.tres");
			StringName wallHitAnimation = "wall_hit_198_204";
			Expect(wallHitFrames != null && wallHitFrames.HasAnimation(wallHitAnimation) &&
				wallHitFrames.GetFrameCount(wallHitAnimation) == 7 &&
				!wallHitFrames.GetAnimationLoop(wallHitAnimation) &&
				Mathf.IsEqualApprox((float)wallHitFrames.GetAnimationSpeed(wallHitAnimation), 60f),
				"wall-hit SpriteFrames resource is not seven non-looping drawings at 60 Hz");

			Vector2 runDustGround = new(180f, 420f);
			BigBangCommonEffect selectedRunDust = layer.SpawnRunDust(runDustGround, facing: -1);
			Expect(selectedRunDust != null && selectedRunDust.EffectKind == BigBangCommonEffectKind.RunDust205To217,
				"run-dust API did not instantiate the assigned Smoke 3 action");
			Expect(selectedRunDust.GlobalPosition.IsEqualApprox(runDustGround) &&
				selectedRunDust.CurrentSourceFrame == 160 && selectedRunDust.CurrentTextureFrame == 205 &&
				selectedRunDust.TotalTicks == 26 && !selectedRunDust.UsesAdditiveBlackKey &&
				selectedRunDust.CurrentSourceOrigin.IsEqualApprox(new Vector2(-26f, 9f)) &&
				Mathf.IsEqualApprox(selectedRunDust.Scale.X, -1f),
				"run dust did not begin mirrored on KIR 160 / PNG 205 at its authored ground origin");
			for (int texture = 206; texture <= 217; texture++)
			{
				selectedRunDust.AdvanceOneTick();
				Expect(selectedRunDust.CurrentTextureFrame == texture - 1,
					$"run-dust PNG {texture - 1:D3} did not retain its second source tick");
				selectedRunDust.AdvanceOneTick();
				Expect(selectedRunDust.CurrentSourceFrame == texture - 45 && selectedRunDust.CurrentTextureFrame == texture,
					$"run dust skipped KIR {texture - 45} / PNG {texture}");
			}
			Expect(selectedRunDust.CurrentSourceOrigin.IsEqualApprox(new Vector2(-107f, -26f)),
				"run-dust PNG 217 did not complete the source-authored fade origin");

			SpriteFrames runDustFrames = GD.Load<SpriteFrames>(
				"res://Assets/Effects/BigBangCommon/run_dust_205_217_frames.tres");
			StringName runDustAnimation = "run_dust_205_217";
			Expect(runDustFrames != null && runDustFrames.HasAnimation(runDustAnimation) &&
				runDustFrames.GetFrameCount(runDustAnimation) == 13 &&
				!runDustFrames.GetAnimationLoop(runDustAnimation) &&
				Mathf.IsEqualApprox((float)runDustFrames.GetAnimationSpeed(runDustAnimation), 60f),
				"run-dust SpriteFrames resource is not thirteen non-looping drawings at 60 Hz");

			PackedScene superCancelScene = GD.Load<PackedScene>("res://Effects/BigBangSuperCancelEffect.tscn");
			BigBangSuperCancelEffect superCancel = superCancelScene?.Instantiate<BigBangSuperCancelEffect>();
			Expect(superCancel != null, "layered BBB super-cancel scene did not load");
			AddChild(superCancel);
			Expect(superCancel.CurrentInnerFrame == 0 && superCancel.CurrentOuterFrame == 8 &&
				superCancel.CurrentCoreFrame == 17 && superCancel.TotalTicks == 20,
				"super-cancel composite did not begin at lightning 000/008 and core 017");
			for (int tick = 0; tick < 16; tick++)
			{
				Expect(superCancel.LightningVisible, $"super-cancel lightning disappeared on source tick {tick}");
				superCancel.AdvanceOneTick();
			}
			Expect(!superCancel.LightningVisible && superCancel.CurrentCoreFrame == 33,
				"super-cancel lightning did not finish before the four-frame core dissolve");
			for (int tick = 16; tick < 19; tick++) superCancel.AdvanceOneTick();
			Expect(superCancel.CurrentCoreFrame == 36,
				$"super-cancel core ended on {superCancel.CurrentCoreFrame:D3}, expected 036");

			PackedScene bloodScene = GD.Load<PackedScene>("res://Effects/BigBangBloodHitSpark.tscn");
			BigBangCommonEffect blood = bloodScene?.Instantiate<BigBangCommonEffect>();
			AddChild(blood);
			Expect(blood != null && blood.EffectKind == BigBangCommonEffectKind.BloodBurst,
				"Mecha blood spark did not instantiate common visual 26");
			Expect(blood.CurrentSourceFrame == 200 && blood.CurrentTextureFrame == 260 && blood.TotalTicks == 33,
				$"blood source timeline is {blood.CurrentSourceFrame}/{blood.CurrentTextureFrame}/" +
				$"{blood.TotalTicks}, expected KIR200/PNG260/33t");
			Expect(blood.CurrentSpritePosition.IsEqualApprox(new Vector2(-4f, -36f)),
				$"blood source origin resolved to {blood.CurrentSpritePosition}");
			Expect(blood.BloodParticleCount == 14,
				$"blood spawned {blood.BloodParticleCount} droplets instead of the source-authored 14");

			GD.Print("BIGBANG_COMMON_EFFECTS_TEST_PASS weak+strong=cropped/PNG062-069/15t/flipped " +
				"guard=KIR192-199/PNG237-244/22t scale=30%-100% " +
				"bstone=KIR185-191/PNG230-236/7t additive-black-key " +
				"airdash_particle=KIR212/PNG272/30t particle_b=KIR213/PNG273/60t " +
				"unused_core=KIR214/PNG274 ring_candidate=KIR215-220/PNG275-280/12t " +
				"run_dust=KIR85-94/PNG090-099/30t " +
				"jump=95-102/8t super_jump=KIR95-102/PNG111-118/8t " +
				"blood=KIR200-210/PNG260-270+PNG271/14p numbered=000-015/32t " +
				"block_candidate=039-045/14t " +
				"ground_bounce=055-061/14t " +
				"burst=KIR66-74/PNG071-079/9t " +
				"small_dust=KIR75-84/PNG080-089/20t " +
				"unassigned=KIR104-127/PNG120-158/48t " +
				"technique_hit_ring=KIR128-135/PNG161-168/8t " +
				"additive_impact=KIR136-141/PNG181-186/12t green+black-key " +
				"wall_jump=KIR144-151/PNG189-196/8t " +
				"wall_hit=KIR153-159/PNG198-204/14t " +
				"run_dust_selected=KIR160-172/PNG205-217/26t " +
				"super_cancel=000-007+008-015+017-036/20t");
			GetTree().Quit();
		}
		catch (Exception exception)
		{
			GD.PushError($"BIGBANG_COMMON_EFFECTS_TEST_FAILED: {exception.Message}");
			GetTree().Quit(1);
		}
	}

	private static BigBangCommonEffect FindNewest(HitSparkLayer layer, BigBangCommonEffectKind kind) =>
		layer.GetChildren().OfType<BigBangCommonEffect>().LastOrDefault(effect => effect.EffectKind == kind);

	private static void Expect(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
