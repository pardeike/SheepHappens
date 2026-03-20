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

local sheepSnapshot = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(sheepSnapshot.result.count == 1, "Expected exactly one player-owned sheep in the base fixture.")

local sheepPawn = sheepSnapshot.result.sheep[1]

local withoutHostile = rb.call("rimworld/open_context_menu", {
  x = sheepPawn.position.x,
  z = sheepPawn.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})

rb.assert(withoutHostile.result.optionCount == 2, "Expected the ignite option to be hidden when no hostile target exists.")
rb.call("rimworld/close_context_menu", nil)

local hostile = rb.call("sheephappens/spawn_hostile_human", {
  x = 130,
  z = 114
})

rb.assert(hostile.result.pawn.pawnId ~= nil, "Expected a hostile pawn id from the deterministic hostile spawn tool.")

local withHostile = rb.call("rimworld/open_context_menu", {
  x = sheepPawn.position.x,
  z = sheepPawn.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})

rb.assert(withHostile.result.optionCount == 3, "Expected the ignite option to appear once a hostile target exists.")
rb.assert(withHostile.result.options[2].label == "Prioritize igniting Ram 1", "Expected the ignite option to be the second enabled entry.")
rb.call("rimworld/close_context_menu", nil)

return {
  hostile = hostile.result.pawn,
  withoutHostile = withoutHostile.result.options,
  withHostile = withHostile.result.options
}
