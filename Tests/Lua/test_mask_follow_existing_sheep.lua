local maskSaveName = params.maskSaveName or "sheephappens_fixture_mask_persistence"
local colonistPawnId = params.colonistPawnId or "Thing_Human1023"

rb.call("rimworld/load_game", { saveName = maskSaveName })
rb.call("rimbridge/wait_for_game_loaded", {
  timeoutMs = 120000,
  pollIntervalMs = 250,
  waitForScreenFade = true,
  pauseIfNeeded = true
})

local sheepSnapshot = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(sheepSnapshot.result.count == 0, "Expected the masked fixture to start without any sheep.")

local spawned = rb.call("sheephappens/spawn_sheep", {
  x = 123,
  z = 115,
  playerOwned = true
})

local sheepPawnId = spawned.result.pawn.pawnId
local spawnedPosition = spawned.result.pawn.position
rb.assert(sheepPawnId ~= nil, "Expected a spawned sheep id for the mask follow test.")

rb.call("rimworld/select_pawn", { pawnId = colonistPawnId })
rb.call("rimworld/set_draft", { pawnId = colonistPawnId, drafted = true })
rb.call("rimworld/right_click_cell", {
  x = 119,
  z = 115,
  button = "right",
  holdDurationMs = 0,
  modifiers = ""
})
rb.call("rimworld/pause_game", { pause = false })

local levyClosedDistance = rb.poll("sheephappens/get_pawn_state", { pawnId = colonistPawnId }, {
  timeoutMs = 10000,
  pollIntervalMs = 100,
  condition = {
    all = {
      { path = "result.pawn.position.x", equals = 119 },
      { path = "result.pawn.position.z", equals = 115 }
    }
  }
})

rb.call("rimworld/right_click_cell", {
  x = 107,
  z = 115,
  button = "right",
  holdDurationMs = 0,
  modifiers = ""
})
rb.call("rimworld/pause_game", { pause = false })

local following = rb.poll("sheephappens/get_pawn_state", { pawnId = sheepPawnId }, {
  timeoutMs = 10000,
  pollIntervalMs = 100,
  condition = {
    all = {
      { path = "result.pawn.currentJob", equals = "FollowClose" }
    }
  }
})

local levyArrived = rb.poll("sheephappens/get_pawn_state", { pawnId = colonistPawnId }, {
  timeoutMs = 20000,
  pollIntervalMs = 100,
  condition = {
    all = {
      { path = "result.pawn.position.x", equals = 107 },
      { path = "result.pawn.position.z", equals = 115 }
    }
  }
})

rb.call("rimworld/pause_game", { pause = true })

local sheepAfter = rb.call("sheephappens/get_pawn_state", { pawnId = sheepPawnId })
rb.assert(
  sheepAfter.result.pawn.position.x ~= spawnedPosition.x or sheepAfter.result.pawn.position.z ~= spawnedPosition.z,
  "Expected the spawned sheep to move after Levy started walking in disguise."
)

return {
  spawned = spawned.result.pawn,
  levyClosedDistance = levyClosedDistance.result.pawn,
  following = following.result.pawn,
  levy = levyArrived.result.pawn,
  sheepAfter = sheepAfter.result.pawn
}
