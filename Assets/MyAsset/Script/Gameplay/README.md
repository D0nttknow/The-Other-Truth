# Gameplay Features - Wiring Instructions

This document provides detailed instructions for integrating the new gameplay features into your existing game systems.

## Overview

The gameplay feature set includes:
1. **PlayerLevel** - Leveling system with XP and stat points
2. **ItemBase & Consumables** - Item system with HP/ATK/DEF potions
3. **InventoryManager** - Inventory management system
4. **WeaponDefinition & WeaponHandler** - Weapon system with temporary buffs
5. **PartyAutoAttack** - Auto-attack for party members
6. **LootGenerator** - Random item drop system

## File Locations

All files are located in: `Assets/MyAsset/Script/Gameplay/`

- PlayerLevel.cs
- ItemBase.cs
- ConsumableHeal.cs
- ConsumableAtk.cs
- ConsumableDef.cs
- InventoryManager.cs
- WeaponDefinition.cs
- WeaponHandler.cs
- PartyAutoAttack.cs
- LootGenerator.cs

---

## 1. PlayerLevel Integration

### Setup
1. Attach `PlayerLevel` component to your main character GameObject (the one with CharacterEquipment/PlayerStat)
2. Configure in Inspector:
   - `maxLevel`: Maximum level (default: 20)
   - `pointsPerLevel`: Stat points awarded per level (default: 1)
   - `expBase`: Base XP for level 2 (default: 100)
   - `expGrowth`: XP growth multiplier (default: 1.2)
   - `autoAllocate`: Enable auto stat allocation (optional)
   - Priority sliders: Set HP/ATK/DEF allocation weights

### Integration with Monster Death
In your monster's death logic (e.g., `EnemyStats.OnDeath()` or similar):

```csharp
// Find the main character
var mainCharacter = /* your method to get main character GameObject */;
var playerLevel = mainCharacter.GetComponent<PlayerLevel>();

if (playerLevel != null)
{
    int xpReward = 50; // or get from monster's expValue field
    playerLevel.AddExp(xpReward);
}
```

### Manual Stat Allocation
```csharp
playerLevel.AllocatePoint("HP");  // Allocate to HP
playerLevel.AllocatePoint("ATK"); // Allocate to ATK
playerLevel.AllocatePoint("DEF"); // Allocate to DEF
```

---

## 2. Consumable Items Setup

### Creating Consumable Items
1. In Unity Project window, right-click and select:
   - `Create > Gameplay/Consumable Heal` → Save as "HealPotion"
   - `Create > Gameplay/Consumable ATK` → Save as "AtkPotion"
   - `Create > Gameplay/Consumable DEF` → Save as "DefPotion"

2. Configure each item:
   - Set `itemID` (unique, e.g., "potion_heal_small")
   - Set `displayName` (e.g., "Small Heal Potion")
   - Set `icon` (optional Sprite)
   - Set `description`
   - Configure min/max values (default: 1-3)

---

## 3. InventoryManager Setup

### Scene Setup
1. Create an empty GameObject in your scene named "InventoryManager"
2. Attach the `GameplayFeatures.InventoryManager` script
3. Configure `maxSlots` (default: 20)
4. The GameObject will persist across scenes (DontDestroyOnLoad)

### Usage in Code
```csharp
using GameplayFeatures;

// Add item to inventory
var healPotion = /* your ConsumableHeal ScriptableObject */;
InventoryManager.Instance.AddItem(healPotion);

// Use item on a character
CharacterEquipment target = /* your character */;
int itemIndex = 0; // First item in inventory
InventoryManager.Instance.UseItem(itemIndex, target);

// Subscribe to inventory changes (for UI updates)
InventoryManager.Instance.OnInventoryChanged += UpdateInventoryUI;
```

---

## 4. Weapon System Integration

### Creating Weapon Definitions
1. In Unity Project window, right-click:
   - `Create > Gameplay/Weapon Definition` → Save as "Sword_Basic"
   
2. Configure weapon (Sword example):
   - `weaponType`: Sword
   - `displayName`: "Sword"
   - `damageMultiplier`: 1.25
   - `speedModPercent`: 25
   - `durationTurns`: 1

3. Repeat for Cudgel/Hammer:
   - `weaponType`: Cudgel
   - `damageMultiplier`: 2.5
   - `speedModPercent`: -25
   - `durationTurns`: 1

### Adding WeaponHandler
1. Select your character GameObject (with CharacterEquipment)
2. Add `WeaponHandler` component
3. Assign a `WeaponDefinition` in the Inspector

### Integration with Combat System

**In PerCharacterUIController.cs** (or your combat controller):

Before calling `DoNormalAttack()` or `UseSkill()`:

```csharp
// In OnNormalClicked() method, before calling DoNormalAttack:
var weaponHandler = playerEquipment.GetComponent<WeaponHandler>();
if (weaponHandler != null)
{
    weaponHandler.OnUse();
}

// Then proceed with attack
playerEquipment.DoNormalAttack(targetMonster, OnAttackComplete);
```

**Reading damage multiplier in attack calculation**:

```csharp
// In your damage calculation code:
var weaponHandler = attacker.GetComponent<WeaponHandler>();
float damageMultiplier = weaponHandler != null ? weaponHandler.GetEffectiveDamageMultiplier() : 1.0f;

int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier);
```

---

## 5. Loot Generator Setup

### Scene Setup
1. Create an empty GameObject named "LootGenerator"
2. Attach the `LootGenerator` script
3. Assign consumable items to the lists:
   - `healPotions`: Add your ConsumableHeal assets
   - `atkPotions`: Add your ConsumableAtk assets
   - `defPotions`: Add your ConsumableDef assets
4. Configure drop rates and weights

### Integration with Monster Death

In your monster's `OnDeath()` method:

```csharp
using GameplayFeatures;

void OnDeath()
{
    Debug.Log($"{gameObject.name} died!");
    
    // Generate loot
    var lootItem = LootGenerator.GenerateRandomConsumable();
    if (lootItem != null && InventoryManager.Instance != null)
    {
        InventoryManager.Instance.AddItem(lootItem);
        Debug.Log($"Dropped: {lootItem.displayName}");
    }
    
    // Grant XP to player
    var mainCharacter = /* find main character */;
    var playerLevel = mainCharacter?.GetComponent<PlayerLevel>();
    if (playerLevel != null)
    {
        playerLevel.AddExp(expValue); // expValue from IMonsterStat
    }
    
    // Existing death logic...
    Destroy(gameObject);
}
```

**For multiple drops** (e.g., boss monsters):

```csharp
var loots = LootGenerator.GenerateRandomConsumables(3);
foreach (var item in loots)
{
    InventoryManager.Instance.AddItem(item);
}
```

---

## 6. Party Auto-Attack Setup

### Scene Setup
1. Select your TurnBaseSystem GameObject
2. Add `PartyAutoAttack` component
3. Configure:
   - `autoAttackEnabled`: Enable auto-attack
   - `autoLevelUpEnabled`: Enable auto stat allocation on level up
   - `playerLevel`: Assign the main character's PlayerLevel component

### Integration with TurnBaseSystem

**Option A: Manual integration in turn logic**

In your `TurnBaseSystem` when processing a party member's turn:

```csharp
var autoAttack = GetComponent<PartyAutoAttack>();
if (autoAttack != null && !IsPlayerControlled(currentBattler))
{
    autoAttack.ProcessPartyMemberTurn(currentBattler);
}
```

**Option B: Automatic (if your system supports turn events)**

The `PartyAutoAttack` component can subscribe to turn events if you expose them from TurnBaseSystem.

---

## 7. Testing in Unity Editor

### Test PlayerLevel
1. Select main character in Hierarchy
2. In PlayerLevel component, use context menu:
   - `Debug: Level Up` - Grant one level
   - `Debug: Add 5 Levels` - Grant five levels
3. Watch console for level up messages

### Test Inventory
1. In LootGenerator component, use context menu:
   - `Debug: Generate 1 Random Item`
   - `Debug: Generate 5 Random Items`
2. In InventoryManager component:
   - `Debug: Print Inventory` - Show all items
   - `Debug: Clear Inventory` - Remove all items

### Test Weapons
1. In WeaponHandler component, use context menu:
   - `Debug: Trigger Weapon Effect` - Test current weapon
   - `Debug: Equip Sword` - Equip runtime sword
   - `Debug: Equip Cudgel` - Equip runtime cudgel
2. Observe damage multiplier and speed changes in console

---

## 8. Minimal Changes to Existing Code

### Changes Required:

**None!** All features are designed to be non-invasive and optional.

### Optional Enhancements:

1. **PerCharacterUIController.cs**: Add weapon effect call before attacks (see section 4)
2. **Monster death logic**: Add XP grant and loot drop (see sections 1 and 5)
3. **TurnBaseSystem**: Call PartyAutoAttack for non-player party members (see section 6)

---

## 9. Event Subscriptions (Optional)

### PlayerLevel Events
```csharp
playerLevel.OnLevelUp += (newLevel) => {
    Debug.Log($"Player reached level {newLevel}!");
};

playerLevel.OnExpChanged += (currentExp, expToNext) => {
    UpdateExpUI(currentExp, expToNext);
};

playerLevel.OnStatAllocated += (statName, value) => {
    Debug.Log($"Allocated {value} point(s) to {statName}");
};
```

### InventoryManager Events
```csharp
InventoryManager.Instance.OnInventoryChanged += () => {
    RefreshInventoryUI();
};

InventoryManager.Instance.OnItemUsed += (item, success) => {
    if (success) {
        Debug.Log($"Successfully used {item.displayName}");
    }
};
```

---

## 10. Common Issues & Solutions

### Issue: Items don't affect stats
**Solution**: The consumable scripts use reflection to find stat fields. Ensure your PlayerStat has public or serialized fields named `hp`, `maxHp`, `atk`/`attack`, `def`/`defense`.

### Issue: WeaponHandler doesn't affect speed
**Solution**: Ensure PlayerStat has a `speed` field. The handler uses reflection to modify it.

### Issue: No loot drops
**Solution**: 
- Check `dropChance` in LootGenerator (default: 50%)
- Ensure consumable items are assigned to lootPools
- Verify InventoryManager is in the scene

### Issue: Auto-attack doesn't work
**Solution**:
- Ensure TurnBaseSystem reference is valid
- Check that CharacterEquipment has `DoNormalAttack` method
- Enable `autoAttackEnabled` in PartyAutoAttack component

---

## 11. Namespace

All new scripts are in the `GameplayFeatures` namespace. To use them:

```csharp
using GameplayFeatures;
```

---

## 12. Performance Considerations

- **Reflection Usage**: Some scripts use reflection for flexibility. If performance is critical, replace reflection calls with direct property access.
- **Coroutines**: WeaponHandler uses coroutines for timed buffs. Consider using turn-based triggers instead for deterministic behavior.
- **Singleton Pattern**: InventoryManager and LootGenerator use singletons. Ensure only one instance exists.

---

## 13. Future Enhancements

Potential improvements:
- Save/Load system for PlayerLevel and Inventory
- UI panels for inventory and stat allocation
- More weapon types and effects
- Stackable consumables
- Equipment slots (weapon, armor, accessories)
- Skill trees or talent systems

---

## Questions?

All scripts include detailed inline comments. Check the individual files for more implementation details.

Good luck with your game development!
