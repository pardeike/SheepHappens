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
rb.assert(hostile.result.pawn.pawnId ~= nil, "Expected a hostile pawn id for the ignite hold test.")

local sheepSnapshot = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(sheepSnapshot.result.count == 1, "Expected exactly one player-owned sheep in the base fixture.")

local sheepPawn = sheepSnapshot.result.sheep[1]
local sheepPawnId = sheepPawn.pawnId

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

local sheepWaiting = rb.poll("sheephappens/get_pawn_state", { pawnId = sheepPawnId }, {
  timeoutMs = 20000,
  pollIntervalMs = 50,
  condition = {
    all = {
      { path = "result.pawn.currentJob", equals = "Wait" }
    }
  }
})

local colonistDuringIgnite = rb.call("sheephappens/get_pawn_state", { pawnId = colonistPawnId })
local dx = colonistDuringIgnite.result.pawn.position.x - sheepWaiting.result.pawn.position.x
local dz = colonistDuringIgnite.result.pawn.position.z - sheepWaiting.result.pawn.position.z
if dx < 0 then dx = -dx end
if dz < 0 then dz = -dz end
rb.assert(dx <= 1 and dz <= 1, "Expected Levy to be adjacent to the sheep during the ignition hold.")

rb.call("rimworld/pause_game", { pause = true })

return {
  colonist = colonistDuringIgnite.result.pawn,
  hostile = hostile.result.pawn,
  sheep = sheepWaiting.result.pawn
}
