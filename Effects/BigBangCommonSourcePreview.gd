extends Node2D

@export_range(0, 999, 1) var source_section := 19
@export var autoplay := true

const MANIFEST_PATH := "res://Assets/Effects/BigBangCommon/common_source_effect_manifest.json"

var _drawings: Array = []
var _drawing_index := 0
var _drawing_tick := 0
var _playing := false

@onready var effect_sprite: Sprite2D = $EffectSprite


func _ready() -> void:
	var file := FileAccess.open(MANIFEST_PATH, FileAccess.READ)
	if file == null:
		push_error("Missing common-effect source manifest: %s" % MANIFEST_PATH)
		return
	var parsed: Variant = JSON.parse_string(file.get_as_text())
	if not parsed is Dictionary:
		push_error("Invalid common-effect source manifest")
		return
	var sections: Dictionary = parsed.get("sections", {})
	var section: Dictionary = sections.get(str(source_section), {})
	_drawings = section.get("drawings", [])
	if _drawings.is_empty():
		push_error("Common source section %d has no audited drawings" % source_section)
		return
	_show_drawing(0, 0)
	_playing = autoplay


func _physics_process(_delta: float) -> void:
	if not _playing or _drawings.is_empty():
		return
	_drawing_tick += 1
	var drawing: Dictionary = _drawings[_drawing_index]
	if _drawing_tick >= int(drawing.get("hold_ticks", 1)):
		_drawing_tick = 0
		_drawing_index += 1
		if _drawing_index >= _drawings.size():
			_drawing_index = _drawings.size() - 1
			_playing = false
	_show_drawing(_drawing_index, _drawing_tick)


func restart() -> void:
	_drawing_index = 0
	_drawing_tick = 0
	_playing = true
	_show_drawing(0, 0)


func _show_drawing(index: int, tick: int) -> void:
	var drawing: Dictionary = _drawings[index]
	var texture := load(String(drawing.get("resource_path", ""))) as Texture2D
	effect_sprite.texture = texture
	effect_sprite.centered = true
	effect_sprite.flip_h = bool(drawing.get("flip_x", false))
	var scale_x := float(drawing.get("scale_x", 1.0)) + float(drawing.get("growth_x_per_tick", 0.0)) * tick
	var scale_y := float(drawing.get("scale_y", 1.0)) + float(drawing.get("growth_y_per_tick", 0.0)) * tick
	effect_sprite.scale = Vector2(scale_x, scale_y)
	var width := float(texture.get_width())
	var height := float(texture.get_height())
	var origin_x := float(drawing.get("origin_x", 0))
	var origin_y := float(drawing.get("origin_y", 0))
	var center_x := origin_x - width * 0.5 if effect_sprite.flip_h else -origin_x + width * 0.5
	effect_sprite.position = Vector2(center_x * scale_x, (-origin_y + height * 0.5) * scale_y)
