# Gameplay Systems Wiring Guide

This document describes how to set up and connect the gameplay systems (loot generation, weapon handlers, player leveling, and auto-attack) in your Unity scenes.

## Overview

The gameplay systems have been wired together with minimal changes to existing code. The implementation uses reflection fallbacks for compatibility and includes extensive debug logging to help with testing and troubleshooting.

## Inspector Setup Requirements

### 1. TurnManager Configuration

The `TurnManager` component (in `TurnBaseSystem.cs`) needs the following setup:

- **poolOfConsumables**: Assign a list of `ItemBase` consumable assets in the Inspector
  - These items will be randomly dropped when monsters are defeated
  - Create consumable assets via `Create -> Gameplay -> ConsumableHeal/ConsumableAtk/ConsumableDef`
  - Drag the created assets into the `poolOfConsumables` list
- **lootDropCount**: Number of items to drop per defeated monster (default: 3)
  - Adjust this value to control how many items drop from each enemy

### 2. InventoryManager in Scene

- Ensure an `InventoryManager` GameObject exists in your scene
- The `InventoryManager` component should be attached and will auto-initialize as a singleton
- This is required to collect loot drops from defeated monsters
- If `InventoryManager.Instance` is null when a monster is defeated, a warning will be logged

### 3. PlayerLevel Component

- Attach the `PlayerLevel` component to your main character GameObject(s)
- This enables level-based experience gain with stat point allocation
- **Important**: The `TurnManager.AwardExpToPlayers()` method will:
  1. First try to use `PlayerLevel.AddExp()` if the component is present
  2. Fall back to `PlayerStat.AddExp()` via reflection if `PlayerLevel` is not found
  3. Log which method was used for debugging

### 4. WeaponHandler Component

- Attach the `WeaponHandler` component to character GameObjects that use weapons
- Set the `currentWeapon` field to a `WeaponDefinition` asset
- The `WeaponHandler` manages:
  - Damage multipliers (`CurrentDamageMultiplier`)
  - Speed modifiers (`CurrentSpeedModPercent`)
  - Weapon duration/buff timers
- **How it works**:
  - `OnUse()`: Called when attacking - applies weapon buffs for a number of turns
  - `OnTurnEnd()`: Called at the end of each turn - decrements buff duration and resets when expired
  - `CharacterEquipment.DoNormalAttack()` retrieves the multiplier and applies it if possible

### 5. PartyAutoAttack Component

- Attach the `PartyAutoAttack` component to the `TurnManager` GameObject (or a dedicated manager)
- Set `turnManager` reference in Inspector (or leave null to auto-assign from `TurnManager.Instance`)
- Configure behavior:
  - `autoAttackEnabled`: Enable/disable auto-attack for AI-controlled characters
  - `autoAllocateOnLevel`: Auto-allocate stat points when characters level up

**Integration Point**: 
- `TurnManager` should call `PartyAutoAttack.OnBattlerTurnStart(CurrentBattlerObject)` from its `StartTurn()` method
- This allows AI-controlled characters (those without `PlayerLevel`) to automatically attack

## Integration Points

### Turn Flow Integration

The following methods are called automatically during turn flow:

1. **Start of Turn** (`TurnManager.StartTurn()`):
   - `TryTickStatusForIndex()` → calls `CharacterEquipment.OnTurnStart()` → calls `WeaponController.OnTurnStart()`
   - **Optional**: Call `PartyAutoAttack.OnBattlerTurnStart(CurrentBattlerObject)` here for AI characters

2. **During Attack** (`PerCharacterUIController.OnNormalClicked()`):
   - Calls `WeaponHandler.OnUse()` before attacking (applies weapon buffs)
   - Falls back to reflection if `WeaponHandler` not found

3. **End of Turn** (`TurnManager.EndTurn()`):
   - Calls `WeaponHandler.OnTurnEnd()` on current battler (decrements buff duration)
   - Marks battler as acted and advances turn index

4. **Monster Defeated** (`TurnManager.RemoveBattler()`):
   - Generates loot drops using `LootGenerator.GenerateDrops(poolOfConsumables, lootDropCount)`
   - Adds items to `InventoryManager` via `InventoryManager.Instance.AddItem(item)`
   - Logs each dropped item

5. **Battle Victory** (`TurnManager.CheckGameEnd()` → `AwardExpToPlayers()`):
   - Awards experience to alive players
   - Prefers `PlayerLevel.AddExp()` over `PlayerStat.AddExp()`
   - Uses reflection fallback for compatibility

## Test Steps

### 1. Basic Setup Test
1. Create a test scene with:
   - TurnManager GameObject with poolOfConsumables assigned
   - InventoryManager GameObject in scene
   - At least one player character with PlayerLevel component
   - At least one monster enemy

2. Verify Inspector setup:
   - Check that poolOfConsumables has at least 3 ItemBase assets
   - Confirm InventoryManager exists and is active

### 2. Loot Drop Test
1. Start battle and defeat a monster
2. Check Console for debug logs:
   - `[TurnManager] Loot drop from [MonsterName]: [ItemName]`
3. Verify items were added to inventory:
   - `[Inventory] AddItem [ItemName] -> count=[N]`

### 3. Experience Award Test
1. Win a battle with at least one alive player
2. Check Console for one of:
   - `[TurnManager] Awarded [N] EXP to [PlayerName] via PlayerLevel.AddExp`
   - `[TurnManager] Awarded [N] EXP to [PlayerName] via PlayerStat.AddExp (reflection)`
3. If PlayerLevel is present, verify level up occurs when appropriate

### 4. Weapon Handler Test
1. Attach WeaponHandler to a character and assign a WeaponDefinition
2. Start a battle and click the Normal Attack button
3. Check Console for:
   - `[PerCharacterUI] WeaponHandler.OnUse() invoked on [CharacterName]`
   - `[WeaponHandler] OnUse applied dmgMult=[N] speedMod%=[N] for [N] turns`
   - `[CharacterEquipment] Using damage multiplier [N] from WeaponHandler on [CharacterName]`
4. At end of turn, check for:
   - `[TurnManager] Called WeaponHandler.OnTurnEnd() on [CharacterName]`
   - `[WeaponHandler] OnTurnEnd reduced remainingTurns -> [N]`

### 5. Auto-Attack Test (Optional)
1. Attach PartyAutoAttack to TurnManager
2. Add code to TurnManager.StartTurn() to call PartyAutoAttack.OnBattlerTurnStart() for AI characters
3. Start battle and let an AI character take their turn
4. Check Console for:
   - `[PartyAutoAttack] [CharacterName] auto-attacking [TargetName]`
   - `[PartyAutoAttack] [CharacterName] attack complete`

## Troubleshooting

### No loot drops appearing
- Verify `poolOfConsumables` is not empty in TurnManager Inspector
- Check that `InventoryManager.Instance` exists (warning logged if null)
- Ensure monsters are being marked with `isMonster = true` in battlers list

### Experience not being awarded
- Check that `PlayerLevel` or `PlayerStat` component exists on player GameObjects
- Look for debug logs indicating which method was used or failed
- Verify players are marked with `isMonster = false` in battlers list

### Weapon buffs not applying
- Confirm `WeaponHandler` is attached to character GameObject
- Set `currentWeapon` field to a valid `WeaponDefinition` asset
- Check debug logs for `OnUse()` and `OnTurnEnd()` calls
- Verify `CharacterEquipment` retrieves the multiplier (logged)

### Auto-attack not working
- Ensure `PartyAutoAttack` component exists and `autoAttackEnabled = true`
- Verify `turnManager` reference is set (or TurnManager.Instance is available)
- Check that OnBattlerTurnStart() is being called from TurnManager.StartTurn()
- AI characters should NOT have PlayerLevel component (they are skipped if present)

## Code Changes Summary

All changes were made with minimal invasiveness:

1. **TurnBaseSystem.cs (TurnManager)**:
   - Added `poolOfConsumables` inspector field
   - Modified `RemoveBattler()` to generate and add loot drops
   - Modified `AwardExpToPlayers()` to prefer PlayerLevel.AddExp with reflection fallback
   - Modified `EndTurn()` to call WeaponHandler.OnTurnEnd()

2. **PerCharacterUIController.cs**:
   - Modified `OnNormalClicked()` to call WeaponHandler.OnUse() before attacking
   - Added reflection fallback for OnUse method

3. **CharacterEquipment.cs**:
   - Modified `DoNormalAttack()` to retrieve and apply damage multiplier from WeaponHandler
   - Added reflection-based invocation of NormalAttack(GameObject, float) overload if available

4. **PartyAutoAttack.cs**:
   - Updated references from `TurnBaseSystem` to `TurnManager`
   - Added safety checks and auto-assignment from TurnManager.Instance

All changes include null checks, exception handling, and debug logging for ease of testing and troubleshooting.
