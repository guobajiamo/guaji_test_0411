extends Node

func _ready() -> void:
	var main_scene: Node = load("res://Scenes/Main.tscn").instantiate()
	add_child(main_scene)

	await get_tree().process_frame
	await get_tree().process_frame

	var game_manager := get_node("/root/GameManager")
	main_scene.StartDefaultStory()

	await get_tree().process_frame
	await get_tree().process_frame

	var main_ui: Node = main_scene.find_child("MainUI", true, false)
	var main_tabs: Control = main_ui.find_child("MainTabs", true, false)
	var has_system_tab := false
	for child in main_tabs.get_children():
		if child.name == "系统":
			has_system_tab = true
			break

	var story_event_count: int = game_manager.GetRegisteredEventCount()
	var slot_path: String = game_manager.GetStorySaveSlotPath(1)
	var save_ok: bool = game_manager.SaveGameToPath(slot_path)

	main_scene.StartDefaultStory()
	await get_tree().process_frame
	await get_tree().process_frame

	var load_ok: bool = game_manager.LoadGameFromPath(slot_path)

	print("[SaveLoadSmokeTest] has_system_tab=", has_system_tab, " story_event_count=", story_event_count, " save_ok=", save_ok, " load_ok=", load_ok)
	get_tree().quit(0 if has_system_tab and story_event_count == 0 and save_ok and load_ok else 1)
