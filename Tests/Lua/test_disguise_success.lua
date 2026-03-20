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

rb.call("rimworld/select_pawn", { pawnId = colonistPawnId })

local menu = rb.call("rimworld/open_context_menu", {
  x = sheepPawn.position.x,
  z = sheepPawn.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})

rb.assert(menu.result.optionCount == 2, "Expected disguise plus rope options when no hostile target exists.")
rb.call("rimworld/execute_context_menu_option", { optionIndex = -1, label = "creating disguise" })
rb.call("rimworld/close_context_menu", nil)
rb.call("rimworld/pause_game", { pause = false })

local disguised = rb.poll("sheephappens/get_pawn_state", { pawnId = colonistPawnId }, {
  timeoutMs = 30000,
  pollIntervalMs = 200,
  condition = {
    all = {
      { path = "result.pawn.maskActive", equals = true }
    }
  }
})

local sheepGone = rb.poll("sheephappens/list_sheep", { playerOwnedOnly = false }, {
  timeoutMs = 30000,
  pollIntervalMs = 200,
  condition = {
    all = {
      { path = "result.count", equals = 0 }
    }
  }
})

rb.call("rimworld/pause_game", { pause = true })
rb.assert(disguised.result.pawn.maskUntilTick > 0, "Expected an active disguise timer after the sacrifice.")

return {
  colonist = disguised.result.pawn,
  sheepCount = sheepGone.result.count
}
