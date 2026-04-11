extends Node

func _ready() -> void:
	var main_scene: Node = load("res://Scenes/Main.tscn").instantiate()
	add_child(main_scene)

	await get_tree().process_frame

	var game_manager := get_node("/root/GameManager")
	var save_ok: bool = game_manager.SaveGame()
	var load_ok: bool = game_manager.LoadGame()

	print("[SaveLoadSmokeTest] save_ok=", save_ok, " load_ok=", load_ok)
	get_tree().quit(0 if save_ok and load_ok else 1)
