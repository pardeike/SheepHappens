local fixtureSaveName = params.fixtureSaveName or "sheephappens_fixture_base"
local persistenceSaveName = params.persistenceSaveName or "sheephappens_fixture_mask_persistence"
local colonistPawnId = params.colonistPawnId or "Thing_Human1023"

rb.print("suite", "starting")

rb.call("rimworld/load_game", { saveName = fixtureSaveName })
rb.call("rimbridge/wait_for_game_loaded", {
  timeoutMs = 120000,
  pollIntervalMs = 250,
  waitForScreenFade = true,
  pauseIfNeeded = true
})

local disguiseSheep = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(disguiseSheep.result.count == 1, "Expected exactly one sheep for the disguise test.")
local disguiseTarget = disguiseSheep.result.sheep[1]

rb.call("rimworld/select_pawn", { pawnId = colonistPawnId })
local disguiseMenu = rb.call("rimworld/open_context_menu", {
  x = disguiseTarget.position.x,
  z = disguiseTarget.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})
rb.assert(disguiseMenu.result.optionCount == 2, "Expected disguise plus rope options when no hostile target exists.")
rb.call("rimworld/execute_context_menu_option", { optionIndex = -1, label = "creating disguise" })
rb.call("rimworld/close_context_menu", nil)
rb.call("rimworld/pause_game", { pause = false })

local disguiseHeld = rb.poll("sheephappens/get_pawn_state", { pawnId = disguiseTarget.pawnId }, {
  timeoutMs = 20000,
  pollIntervalMs = 100,
  condition = {
    all = {
      { path = "result.pawn.currentJob", equals = "Wait" }
    }
  }
})
local disguised = rb.poll("sheephappens/get_pawn_state", { pawnId = colonistPawnId }, {
  timeoutMs = 30000,
  pollIntervalMs = 200,
  condition = {
    all = {
      { path = "result.pawn.maskActive", equals = true }
    }
  }
})
local disguiseSheepGone = rb.poll("sheephappens/list_sheep", { playerOwnedOnly = false }, {
  timeoutMs = 30000,
  pollIntervalMs = 200,
  condition = {
    all = {
      { path = "result.count", equals = 0 }
    }
  }
})
rb.call("rimworld/pause_game", { pause = true })
rb.print("disguise_hold", disguiseHeld.result.pawn.currentJob)
rb.assert(disguised.result.pawn.maskUntilTick > 0, "Expected an active disguise timer after the disguise test.")
rb.print("disguise_success", disguised.result.pawn.maskUntilTick)

rb.call("rimworld/save_game", { saveName = persistenceSaveName })
rb.call("rimworld/load_game", { saveName = persistenceSaveName })
rb.call("rimbridge/wait_for_game_loaded", {
  timeoutMs = 120000,
  pollIntervalMs = 250,
  waitForScreenFade = true,
  pauseIfNeeded = true
})
local persisted = rb.call("sheephappens/get_pawn_state", { pawnId = colonistPawnId })
rb.assert(persisted.result.pawn.maskActive == true, "Expected disguise state to persist across save/load.")
rb.assert(persisted.result.pawn.maskUntilTick > 0, "Expected disguise timer to persist across save/load.")
rb.print("mask_persistence", persisted.result.pawn.maskUntilTick)

local followSheep = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(followSheep.result.count == 0, "Expected the mask persistence fixture to start without sheep before the follow test.")

local spawnedFollowSheep = rb.call("sheephappens/spawn_sheep", {
  x = 123,
  z = 115,
  playerOwned = true
})
local followSheepPawnId = spawnedFollowSheep.result.pawn.pawnId
local followStart = spawnedFollowSheep.result.pawn.position
rb.assert(followSheepPawnId ~= nil, "Expected a spawned sheep id for the follow test.")

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

local followingSheep = rb.poll("sheephappens/get_pawn_state", { pawnId = followSheepPawnId }, {
  timeoutMs = 10000,
  pollIntervalMs = 100,
  condition = {
    all = {
      { path = "result.pawn.currentJob", equals = "FollowClose" }
    }
  }
})
local levyMovedAway = rb.poll("sheephappens/get_pawn_state", { pawnId = colonistPawnId }, {
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

local followSheepAfter = rb.call("sheephappens/get_pawn_state", { pawnId = followSheepPawnId })
rb.assert(
  followSheepAfter.result.pawn.position.x ~= followStart.x or followSheepAfter.result.pawn.position.z ~= followStart.z,
  "Expected the sheep to move once masked Levy walked away."
)
rb.assert(levyClosedDistance.result.pawn.position.x == 119 and levyClosedDistance.result.pawn.position.z == 115, "Expected Levy to first move close enough to the sheep before walking away.")
rb.print("mask_follow", followingSheep.result.pawn.currentJob)

rb.call("rimworld/load_game", { saveName = fixtureSaveName })
rb.call("rimbridge/wait_for_game_loaded", {
  timeoutMs = 120000,
  pollIntervalMs = 250,
  waitForScreenFade = true,
  pauseIfNeeded = true
})

local visibilitySheep = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(visibilitySheep.result.count == 1, "Expected exactly one sheep for the ignite visibility test.")
local visibilityTarget = visibilitySheep.result.sheep[1]

rb.call("rimworld/select_pawn", { pawnId = colonistPawnId })
local withoutHostile = rb.call("rimworld/open_context_menu", {
  x = visibilityTarget.position.x,
  z = visibilityTarget.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})
rb.assert(withoutHostile.result.optionCount == 2, "Expected the ignite option to be hidden without a hostile.")
rb.call("rimworld/close_context_menu", nil)

local visibilityHostile = rb.call("sheephappens/spawn_hostile_human", {
  x = 130,
  z = 114
})
local withHostile = rb.call("rimworld/open_context_menu", {
  x = visibilityTarget.position.x,
  z = visibilityTarget.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})
rb.assert(withHostile.result.optionCount == 3, "Expected the ignite option to appear with a hostile target.")
rb.assert(withHostile.result.options[2].label == "Prioritize igniting Ram 1", "Expected the ignite option to appear as the second enabled action.")
rb.call("rimworld/close_context_menu", nil)
rb.print("ignite_visibility", visibilityHostile.result.pawn.pawnId)

rb.call("rimworld/load_game", { saveName = fixtureSaveName })
rb.call("rimbridge/wait_for_game_loaded", {
  timeoutMs = 120000,
  pollIntervalMs = 250,
  waitForScreenFade = true,
  pauseIfNeeded = true
})

local igniteHoldSheep = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(igniteHoldSheep.result.count == 1, "Expected exactly one sheep for the ignite hold test.")
local igniteHoldTarget = igniteHoldSheep.result.sheep[1]

rb.call("rimworld/select_pawn", { pawnId = colonistPawnId })
local igniteHoldHostile = rb.call("sheephappens/spawn_hostile_human", {
  x = 130,
  z = 114
})
local igniteHoldMenu = rb.call("rimworld/open_context_menu", {
  x = igniteHoldTarget.position.x,
  z = igniteHoldTarget.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})
rb.assert(igniteHoldMenu.result.optionCount == 3, "Expected the ignite option to be available for the hold test.")
rb.call("rimworld/execute_context_menu_option", { optionIndex = -1, label = "igniting" })
rb.call("rimworld/close_context_menu", nil)
rb.call("rimworld/pause_game", { pause = false })

local igniteHolding = rb.poll("sheephappens/get_pawn_state", { pawnId = igniteHoldTarget.pawnId }, {
  timeoutMs = 20000,
  pollIntervalMs = 50,
  condition = {
    all = {
      { path = "result.pawn.currentJob", equals = "Wait" }
    }
  }
})
local levyDuringIgnite = rb.call("sheephappens/get_pawn_state", { pawnId = colonistPawnId })
local igniteDx = levyDuringIgnite.result.pawn.position.x - igniteHolding.result.pawn.position.x
local igniteDz = levyDuringIgnite.result.pawn.position.z - igniteHolding.result.pawn.position.z
if igniteDx < 0 then igniteDx = -igniteDx end
if igniteDz < 0 then igniteDz = -igniteDz end
rb.assert(igniteDx <= 1 and igniteDz <= 1, "Expected Levy to be adjacent to the sheep during the ignition hold.")
rb.call("rimworld/pause_game", { pause = true })
rb.print("ignite_hold", igniteHolding.result.pawn.currentJob)

local bombSheep = rb.call("sheephappens/list_sheep", { playerOwnedOnly = false })
rb.assert(bombSheep.result.count == 1, "Expected exactly one sheep for the bomb test.")
local bombTarget = bombSheep.result.sheep[1]

rb.call("rimworld/select_pawn", { pawnId = colonistPawnId })
local bombHostile = rb.call("sheephappens/spawn_hostile_human", {
  x = 130,
  z = 114
})
local bombHostilePawnId = bombHostile.result.pawn.pawnId

rb.call("rimworld/open_context_menu", {
  x = bombTarget.position.x,
  z = bombTarget.position.z,
  mode = "vanilla",
  button = "right",
  holdDurationMs = 0
})
rb.call("rimworld/execute_context_menu_option", { optionIndex = -1, label = "igniting" })
rb.call("rimworld/close_context_menu", nil)
rb.call("rimworld/pause_game", { pause = false })

local bombSheepGone = rb.poll("sheephappens/list_sheep", { playerOwnedOnly = false }, {
  timeoutMs = 30000,
  pollIntervalMs = 200,
  condition = {
    all = {
      { path = "result.count", equals = 0 }
    }
  }
})
local bombHostileSleeping = rb.poll("sheephappens/get_pawn_state", { pawnId = bombHostilePawnId }, {
  timeoutMs = 30000,
  pollIntervalMs = 200,
  condition = {
    all = {
      { path = "result.pawn.currentJob", equals = "LayDown" }
    }
  }
})
rb.call("rimworld/pause_game", { pause = true })
rb.print("ignite_with_hostile", bombHostileSleeping.result.pawn.currentJob)

return {
  disguise = {
    sheepCount = disguiseSheepGone.result.count,
    maskUntilTick = disguised.result.pawn.maskUntilTick
  },
  persistence = {
    maskUntilTick = persisted.result.pawn.maskUntilTick
  },
  visibility = {
    withoutHostileCount = withoutHostile.result.optionCount,
    withHostileCount = withHostile.result.optionCount
  },
  bomb = {
    sheepCount = bombSheepGone.result.count,
    hostileJob = bombHostileSleeping.result.pawn.currentJob
  }
}
