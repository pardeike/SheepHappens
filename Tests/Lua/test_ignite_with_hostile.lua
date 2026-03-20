local saveName = params.fixtureSaveName or "sheephappens_fixture_base"
local colonistPawnId = params.colonistPawnId or "Thing_Human1023"

rb.call("rimworld/load_game", { saveName = saveName })
rb.call("rimbridge/wait_for_game_loaded", {
  timeoutMs = 120000,
  pollIntervalMs = 250,
  waitForScreenFade = true,
  pauseIfNeeded = true
})

rb.call("rimworld/select_pawn", { pawnId = colonistPawnId })

local hostile = rb.call("sheephappens/spawn_hostile_human", {
  x = 130,
  z = 114
})

local hostilePawnId = hostile.result.pawn.pawnId
rb.assert(hostilePawnId ~= nil, "Expected a hostile pawn id from the deterministic hostile spawn tool.")

local sheepSnapshot = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(sheepSnapshot.result.count == 1, "Expected exactly one player-owned sheep in the base fixture.")

local sheepPawn = sheepSnapshot.result.sheep[1]

rb.call("rimworld/open_context_menu", {
  x = sheepPawn.position.x,
  z = sheepPawn.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})
rb.call("rimworld/execute_context_menu_option", { optionIndex = -1, label = "igniting" })
rb.call("rimworld/close_context_menu", nil)
rb.call("rimworld/pause_game", { pause = false })

local sheepGone = rb.poll("sheephappens/list_sheep", { playerOwnedOnly = false }, {
  timeoutMs = 30000,
  pollIntervalMs = 200,
  condition = {
    all = {
      { path = "result.count", equals = 0 }
    }
  }
})

local hostileSleeping = rb.poll("sheephappens/get_pawn_state", { pawnId = hostilePawnId }, {
  timeoutMs = 30000,
  pollIntervalMs = 200,
  condition = {
    all = {
      { path = "result.pawn.currentJob", equals = "LayDown" }
    }
  }
})

rb.call("rimworld/pause_game", { pause = true })

return {
  sheepCount = sheepGone.result.count,
  hostile = hostileSleeping.result.pawn
}
