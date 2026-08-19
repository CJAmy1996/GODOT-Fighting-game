extends Node2D
@export var mute: bool = false

const MUSIC_NORMAL_DB := -4.0
const MUSIC_HYPER_DUCK_DB := -17.0

var _last_scene: Node
var _music_volume_tween: Tween

func _ready() -> void:
	# The autoload survives scene changes, so it owns music state instead of
	# relying on a freshly compiled scene script to start or stop playback.
	AudioServer.set_bus_mute(AudioServer.get_bus_index("Master"), false)
	set_process(true)
	_sync_music_to_scene()

func _process(_delta: float) -> void:
	var current_scene := get_tree().current_scene
	if current_scene == _last_scene:
		return
	_last_scene = current_scene
	_sync_music_to_scene()

func _sync_music_to_scene() -> void:
	var current_scene := get_tree().current_scene
	if current_scene != null and current_scene.name == "Arena":
		play_music()
	else:
		stop_music()

func play_music() -> void:
	if not mute and not $Music.playing:
		$Music.play()

func stop_music() -> void:
	if _music_volume_tween != null:
		_music_volume_tween.kill()
		_music_volume_tween = null
	$Music.volume_db = MUSIC_NORMAL_DB
	$Music.stop()

func duck_music_for_hyper_finish() -> void:
	if mute:
		return
	if _music_volume_tween != null:
		_music_volume_tween.kill()
	_music_volume_tween = create_tween()
	_music_volume_tween.set_pause_mode(Tween.TWEEN_PAUSE_PROCESS)
	_music_volume_tween.tween_property($Music, "volume_db", MUSIC_HYPER_DUCK_DB, 0.12)

func restore_music_after_hyper_finish() -> void:
	if _music_volume_tween != null:
		_music_volume_tween.kill()
	_music_volume_tween = create_tween()
	_music_volume_tween.set_pause_mode(Tween.TWEEN_PAUSE_PROCESS)
	_music_volume_tween.tween_property($Music, "volume_db", MUSIC_NORMAL_DB, 1.35)

func play_hit(attack_name: String = "LIGHT PUNCH", is_super: bool = false) -> void:
	if not mute:
		if attack_name == "MECHA ELECTRICITY":
			$Electrocuted.play(0.0)
			return
		if is_super:
			$KnockAway.play(0.0)
			return
		var heavy := attack_name.contains("HEAVY")
		var kick := attack_name.contains("KICK")
		var player: AudioStreamPlayer
		if kick:
			player = $HeavyPunchHit if heavy else $LightKickHit
		else:
			player = $HeavyPunchHit if heavy else $LightPunchHit
		player.play(0.0)

func play_knock_away() -> void:
	_play_effect($KnockAway)

func play_mecha_boost() -> void:
	_play_effect($MechaBoost)

func play_whiff(attack_name: String) -> void:
	var heavy := attack_name.contains("HEAVY")
	var medium := attack_name.contains("MEDIUM")
	var kick := attack_name.contains("KICK")
	if kick:
		_play_effect($HeavyKickWhiff if heavy else $KickWhiff)
	elif heavy:
		_play_effect($HeavyWhiff)
	elif medium:
		_play_effect($MediumWhiff)
	else:
		_play_effect($LightWhiff)

func play_footstep(running: bool = false) -> void:
	_play_effect($RunFootstep if running else $Footstep)

func play_cursor() -> void:
	_play_effect($Cursor)

func play_select() -> void:
	_play_effect($Select)

func play_blue_cancel() -> void:
	_play_effect($BlueCancel)

func play_block(block_strength: int, instant_block: bool = false) -> void:
	match block_strength:
		2: _play_effect($MediumBlock)
		3: _play_effect($StrongBlock)
		4: _play_effect($SpecialStrongBlock)
		_: _play_effect($MediumBlock)
	if instant_block:
		_play_effect($PerfectBlock)

func play_super_activation() -> void:
	_play_effect($SuperActivation)

func play_rocket() -> void:
	_play_effect($Rocket)

func play_explosion() -> void:
	_play_effect($Explosion)

func play_burning() -> void:
	_play_effect($Burning)

func play_electricity() -> void:
	_play_effect($Electricity)

func _play_effect(player: AudioStreamPlayer) -> void:
	if not mute:
		player.play(0.0)
