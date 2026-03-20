local saveName = params.fixtureSaveName or "sheephappens_fixture_base"
local colonistPawnId = params.colonistPawnId or "Thing_Human1023"

rb.call("rimworld/load_game", { saveName = saveName })
rb.call("rimbridge/wait_for_game_loaded", {
  timeoutMs = 120000,
  pollIntervalMs = 250,
  waitForScreenFade = true,
  pauseIfNeeded = true
})

local sheepSnapshot = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(sheepSnapshot.result.count == 1, "Expected exactly one player-owned sheep in the base fixture.")

local sheepPawn = sheepSnapshot.result.sheep[1]
local sheepPawnId = sheepPawn.pawnId

rb.call("rimworld/select_pawn", { pawnId = colonistPawnId })
rb.call("rimworld/open_context_menu", {
  x = sheepPawn.position.x,
  z = sheepPawn.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})
rb.call("rimworld/execute_context_menu_option", { optionIndex = -1, label = "creating disguise" })
rb.call("rimworld/close_context_menu", nil)
rb.call("rimworld/pause_game", { pause = false })

local sheepHeld = rb.poll("sheephappens/get_pawn_state", { pawnId = sheepPawnId }, {
  timeoutMs = 20000,
  pollIntervalMs = 100,
  condition = {
    all = {
      { path = "result.pawn.currentJob", equals = "Wait" }
    }
  }
})

local ritual = rb.poll("sheephappens/get_pawn_state", { pawnId = colonistPawnId }, {
  timeoutMs = 20000,
  pollIntervalMs = 100,
  condition = {
    all = {
      { path = "result.pawn.currentJob", equals = "CreateSheepDisguise" }
    }
  }
})

rb.call("rimworld/pause_game", { pause = true })

return {
  sheep = sheepHeld.result.pawn,
  colonist = ritual.result.pawn
}
