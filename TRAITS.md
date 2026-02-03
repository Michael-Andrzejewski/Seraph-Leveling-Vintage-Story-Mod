# Simple Improving Traits - Detailed Documentation

## Vanilla Class Traits Reference

### Stat Modifiers

| Trait | Attribute | Value |
|-------|-----------|-------|
| Focused | Ranged damage | +20% |
| Focused | Ranged accuracy | +30% |
| Focused | Ranged distance | +20% |
| Resourceful | Animal loot | +10% |
| Resourceful | Harvesting speed | +25% |
| Fleetfooted | Walk speed | +10% |
| Forager | Foraging loot | +10% |
| Forager | Wild crop drop | +20% |
| Pilferer | Cracked vessel drops | +15% |
| Pilferer | Rusty gear drops | +10% |
| Pilferer | Vessel collection chance | +12% |
| Furtive | Animal seeking range | -35% |
| Precise | Damage vs mechanicals | +25% |
| Technical | Translocator gear cost | -1 |
| Soldier | Melee damage | +30% |
| Soldier | Armor durability | +15% |
| Soldier | Armor speed penalty | -25% |
| Hardy | Mining speed | +10% |
| Hardy | Health | +5 HP |
| Mender | Armor durability | +10% |
| Farsighted | Melee damage | -15% |
| Claustrophobic | Ore drop rate | -15% |
| Claustrophobic | Mining speed | -10% |
| Frail | Health | -2.5 HP |
| Frail | Ranged distance | -25% |
| Nervous | Melee damage | -15% |
| Ravenous | Hunger rate | +30% |
| Nearsighted | Ranged damage | -15% |
| Heavyhanded | Cracked vessel loot | -10% |
| Heavyhanded | Foraging loot | -15% |
| Heavyhanded | Wild crop drops | -20% |
| Civil | Foraging loot | -10% |
| Weak | Health | -2 HP |
| Weak | Mining speed | -10% |
| Kind | Animal loot | -10% |
| Kind | Harvesting speed | -25% |

### Exclusive Crafting Traits

| Trait | Class | Unlocks |
|-------|-------|---------|
| Bowyer | Hunter | Crude bow, arrows |
| Improviser | Malefactor | Sling |
| Tinkerer | Clockmaker | Tuning spear |
| Merciless | Blackguard | Shortsword, shield |
| Clothier | Tailor | Sewing kit, clothing |

---

## Mining Speed (Hardy)

**Max bonus**: 50% | **Base increment**: 100 points | **Scaling**: +100 per credit

### Per-Pickaxe Progress Tracking
Each pickaxe type tracks progress independently, encouraging use of many different pickaxes.

### Block Types
- **Stone** (`rock-`): 1 point each
- **Ore** (`ore-`): 5 points each (configurable)
- **Meteoric Iron** (`meteorite`, `meteoriciron`): 5 points each (same as ore)

### Data Persistence
- `TotalCredits`: 0-50
- `PickaxeProgress`: Per-pickaxe `BlocksInIncrement` and `CurrentIncrementSize`

### Commands
- `/trait mining` - View stats
- `/trait miningbase [value]` - Base points per increment
- `/trait mininglevel <level>` - Set credits
- `/trait miningmax [percent]` - Max bonus

---

## Melee Damage (Soldier)

**Max bonus**: 50% | **Base increment**: 100 damage | **Scaling**: +100 per credit

### Qualifying Weapons
- Swords (`sword-`, `blade-`, `longsword-`, `shortsword-`)
- Falx (`falx-`)
- Spears (`spear-`)
- Quarterstaves (`quarterstaff-`, `staff-`, `bo-`, `bo-staff`)
- Ancient Armory (`aa-blade-`, `aa-axe-`, `aa-club-`, `aa-knife-`, `aa-spear-`)

### Vanilla Interaction
- Soldier trait (Blackguard): can earn up to 20% more (50% cap)
- Others: can earn full 50%

### Commands
- `/trait melee` - View stats
- `/trait meleebase [value]` - Base damage per increment
- `/trait meleelevel <level>` - Set credits
- `/trait meleemax [percent]` - Max bonus

---

## Ranged Damage (Focused)

**Max bonuses**: 50% damage, 50% accuracy, 50% distance
**Base increment**: 100 damage | **Scaling**: +100 per credit

### Per-Weapon Tracking
Tracks bow+arrow combinations (e.g., `bow-crude+arrow-flint`).

### Qualifying Projectiles
- Arrows (`arrow-`)
- Sling stones (`stone-`)
- Thrown spears (`spear-`, `thrownspear`)

### Vanilla Interaction
- Focused trait (Hunter): +20% damage, +30% accuracy, +20% distance already
- Can earn remaining to reach 50% caps

### Commands
- `/trait ranged` - View stats
- `/trait rangedbase [value]` - Base damage per increment
- `/trait rangedlevel <level>` - Set credits
- `/trait rangedmax [percent]` - Max damage bonus
- `/trait rangedmaxacc [percent]` - Max accuracy bonus
- `/trait rangedmaxdist [percent]` - Max distance bonus

---

## Walking Speed (Fleetfooted)

**Max bonus**: 15% | **Base increment**: 1000 blocks | **Scaling**: +1000 per credit

Tracks 2D horizontal distance only (ignores Y-axis). Teleportation ignored (>10 blocks/tick).

### Vanilla Interaction
- Fleetfooted trait: +10% already, can earn 5% more
- Others: can earn full 15%

### Commands
- `/trait walking` - View stats
- `/trait walkingbase [value]` - Base blocks per increment
- `/trait walkinglevel <level>` - Set credits
- `/trait walkingmax [percent]` - Max bonus

---

## Hunger Rate

**Target rate**: 75% | **Base increment**: 300 seconds | **Scaling**: +60 per credit

Only time at exactly maximum saturation counts.

### Class-Specific Max Credits
- Non-Ravenous (100% base): 25 credits to reach 75%
- Ravenous/Blackguard (130% base): 55 credits to reach 75%

### Commands
- `/trait hunger` - View stats
- `/trait hungerbase [value]` - Base seconds per increment
- `/trait hungerlevel <level>` - Set credits
- `/trait hungermax [percent]` - Target reduction

---

## Armor Progression (Soldier)

Provides **Armor Durability** and **Walk Speed Penalty Reduction** (both max 50%).

### XP Sources

**First-Time Equip** (grants both durability + walk speed):
- Light (leather, gambeson, jerkin, improvised): +1% each
- Chain (chain, lamellar): +1% each
- Brigandine: +2% each
- Scale: +3% each
- Plate: +3% each

**Damage Blocked** (durability only):
- Base: 100 damage for first credit, +100 scaling
- Per-piece tracking, distributed by hit probability

**Armor Repairs** (durability only):
- Base: 1 repair for first credit, +1 scaling

### Optional Armor Features

**Hunger Rate Reduction** (disabled by default)
- Set `EnableArmorHungerReduction: true` in config
- Earns hunger reduction credits alongside walk speed from time worn
- Max 50% hunger rate reduction (configurable)

**Healing Effectiveness** (disabled by default)
- Set `EnableArmorHealingBonus: true` in config
- Earns healing credits alongside walk speed from time worn
- Max 25% healing effectiveness bonus (configurable)

### First-Equip Configuration

All first-equip bonuses are now configurable:
```json
{
  "ArmorFirstEquipLightDurability": 1,
  "ArmorFirstEquipChainDurability": 1,
  "ArmorFirstEquipBrigandineDurability": 2,
  "ArmorFirstEquipScaleDurability": 3,
  "ArmorFirstEquipPlateDurability": 3,
  "ArmorFirstEquipLightWalkSpeed": 1,
  "ArmorFirstEquipChainWalkSpeed": 1,
  "ArmorFirstEquipBrigandineWalkSpeed": 2,
  "ArmorFirstEquipScaleWalkSpeed": 3,
  "ArmorFirstEquipPlateWalkSpeed": 3
}
```

### Commands
- `/trait armor` - View stats
- `/trait armorlevel <level>` - Set durability credits
- `/trait armorwalkspeedlevel <level>` - Set walk speed credits
- `/trait armordurabilitymax [percent]` - Max durability bonus
- `/trait armorwalkspeedmax [percent]` - Max walk speed bonus

---

## Clothier (Unlock Trait)

**Requirement**: Wear 20 unique clothing items
**Effect**: Unlocks sewing kit crafting

### Clothing Detection
Items with: `clothes-`, `shirt-`, `trousers-`, `dress-`, `hat-`, `cape-`, `cloak-`, `jacket-`, `vest-`, `skirt-`, `gloves-`, `boots-`, `shoes-`, `headband-`, `mask-`, `scarf-`

### Blacklisted Starting Gear
All class starting outfits are excluded (31 items total across Hunter, Tailor, Malefactor, Blackguard, Clockmaker, Commoner).

### Commands
- `/trait clothier` - View progress
- `/trait clothierrequired [count]` - Required unique clothes

---

## Mender

**Max bonus**: 20% armor/clothing durability
**Base increment**: 5 repairs | **Scaling**: +1 per credit

Requires sewing kit repairs (tracked via Harmony patch).

### Commands
- `/trait mender` - View stats
- `/trait menderbase [value]` - Base repairs per increment
- `/trait menderlevel <level>` - Set credits
- `/trait mendermax [percent]` - Max bonus

---

## Pilferer

**Max bonus**: 20% (rusty gear, vessel contents, vessel collection)
**Base increment**: 10 points | **Scaling**: +10 per credit

### Point Values
- Opening collapsed chest (first time): 1 point
- Breaking vessel: 2 points

### Qualifying Blocks
`vessel-`, `storagevessel`, `crackedvessel`, `urn-`

### Commands
- `/trait pilferer` - View stats
- `/trait pilfererbase [value]` - Base points per increment
- `/trait pilfererlevel <level>` - Set credits
- `/trait pilferermax [percent]` - Max bonus

---

## Resourceful

**Max bonuses**: 20% loot, 25% harvesting speed
**Base increment**: 10 animals | **Scaling**: +10 per credit

Harvesting animals counts (tracked via Harmony patch).

### Commands
- `/trait resourceful` - View stats
- `/trait resourcefulbase [value]` - Base animals per increment
- `/trait resourcefullevel <level>` - Set credits
- `/trait resourcefulmax [percent]` - Max loot bonus

---

## Forager

**Max bonuses**: 20% foraging loot, 20% wild crop drops
**Base increment**: 10 crops | **Scaling**: +10 per credit

### Qualifying Blocks
`tallgrass`, `flower-`, `mushroom-`, `berry-`, `cattail`, `fern`, `wildvine`, `reeds`, `waterlily`, `seaweed`, or `crop-` + `wild`

### Commands
- `/trait forager` - View stats
- `/trait foragerbase [value]` - Base crops per increment
- `/trait foragerlevel <level>` - Set credits
- `/trait foragermax [percent]` - Max bonus

---

## Technical (Unlock Trait)

**Requirement**: Repair 5 translocators
**Effect**: Reduces translocator gear cost by 1 (3→2 gears)

### Commands
- `/trait technical` - View progress
- `/trait technicalunlock <true/false>` - Manual unlock/lock

---

## Furtive

**Max bonus**: 35% animal detection range reduction
**Base increment**: 100 blocks sneaked | **Scaling**: +100 per credit

Tracks 2D horizontal distance while sneaking.

### Commands
- `/trait furtive` - View stats
- `/trait furtivebase [value]` - Base blocks per increment
- `/trait furtivelevel <level>` - Set credits
- `/trait furtivemax [percent]` - Max bonus

---

## Precise

**Max bonus**: 30% damage vs mechanicals
**Base increment**: 100 damage | **Scaling**: +100 per credit

Per-weapon tracking. Only damage to locusts, bells, and mechanical entities counts.

### Commands
- `/trait precise` - View stats
- `/trait precisebase [value]` - Base damage per increment
- `/trait preciselevel <level>` - Set credits
- `/trait precisemax [percent]` - Max bonus

---

## Hardy Health (Unlock Trait)

**Requirement**: 10% mining speed AND 10% armor durability
**Effect**: +5 HP bonus (displayed with Hardy trait)

### Commands
- `/trait hardyhealth` - View progress
- `/trait hardyhealthunlock <true/false>` - Manual unlock/lock

---

## Bowyer (Unlock Trait)

**Requirement**: 10% ranged damage AND 300 bow damage (simple/longbow only)
**Effect**: Unlocks crude bow and arrows crafting

### Commands
- `/trait bowyer` - View progress
- `/trait bowyerunlock <true/false>` - Manual unlock/lock

---

## Improviser (Unlock Trait)

**Requirement**: 300 damage with thrown rocks
**Effect**: Unlocks sling crafting

### Commands
- `/trait improviser` - View progress
- `/trait improviserunlock <true/false>` - Manual unlock/lock

---

## Tinkerer (Unlock Trait)

**Requirement**: Technical unlocked AND 10% Precise damage
**Effect**: Unlocks tuning spear crafting

### Commands
- `/trait tinkerer` - View progress
- `/trait tinkererunlock <true/false>` - Manual unlock/lock

---

## Merciless (Unlock Trait)

**Requirement**: 10% armor durability AND 15% melee damage
**Effect**: Unlocks Blackguard shortsword and shield crafting

### Commands
- `/trait merciless` - View progress
- `/trait mercilessunlock <true/false>` - Manual unlock/lock

---

## Negative Trait Cancellation

Negative traits decrease progressively as positive trait levels are earned. Removed from UI when penalty reaches 0.

| Negative Trait | Class | Effect | Cancelled By | Levels |
|----------------|-------|--------|--------------|--------|
| Civil | Tailor | -10% foraging | Forager | 10 |
| Weak | Tailor | -2 HP, -10% mining | Hardy | 10 |
| Kind | Tailor | -10% loot, -25% speed | Resourceful | 10/25 |
| Farsighted | Hunter | -15% melee | Soldier | 15 |
| Claustrophobic | Hunter | -15% ore, -10% mining | Hardy | 10 |
| Nervous | Malefactor, Clockmaker | -15% melee | Soldier | 15 |
| Frail | Malefactor, Clockmaker | -2.5 HP, -25% range | Focused | 25 |
| Nearsighted | Blackguard | -15% ranged | Focused | 15 |
| Heavyhanded | Blackguard | -10% vessel, -15% forage, -20% crop | Pilferer/Forager | 10/15/20 |
| Ravenous | Blackguard | +30% hunger | Hunger | 30 |

### Extended Max Levels
Classes with negative traits can earn extra levels to compensate and reach full positive bonuses.

---

## Combat Overhaul Compatibility

Auto-enabled when CO mod detected.

### Proficiencies

| Proficiency | Max | Earned By |
|-------------|-----|-----------|
| Bows | +0.5 | Bow damage |
| Crossbows | +0.5 | Crossbow damage |
| Firearms | +0.5 | Firearm damage |
| Slings | +0.3 | Sling damage |
| One-Handed Swords | +0.3 | 1H sword melee |
| Two-Handed Swords | +0.3 | 2H sword melee |
| Spears | +0.3 | Spear melee |
| Javelins | +0.3 | Javelin throws |
| Maces | +0.3 | Mace melee |
| Clubs | +0.3 | Club melee |
| Halberds | +0.3 | Halberd melee |
| Axes | +0.3 | Combat axe melee |
| Quarterstaff | +0.3 | Staff melee |
| Steady Aim | +0.5 | All ranged damage |

### Commands
- `/trait coproficiency` - View all CO progression
- `/trait coreset` - Reset all CO progression

---

## Ancient Armory Compatibility

Auto-enabled when AA mod detected. All AA weapons qualify for Soldier trait.

| Item Type | Variants | CO Proficiency |
|-----------|----------|----------------|
| aa-blade | gladius, arming, sabre, falchion | 1H Swords |
| aa-blade | claymore, longsword | 2H Swords |
| aa-axe | bearded, battle, bardiche | Axes |
| aa-club | flanged, morningstar, spiked, warhammer | Maces |
| aa-knife | dagger, stiletto, khanjar, baselard | 1H Swords |
| aa-spear | boar, fork, ranseur | Spears |
| aa-spear | voulge | Halberds |

---

## Skill Decay (Optional)

Skills can decay over time when not used. **Disabled by default.**

### Configuration

```json
{
  "EnableSkillDecay": false,
  "DecayGracePeriodDays": 1.0,
  "DecayBasePointsPerDay": 10,
  "DecayMaxPointsPerLogin": 100,
  "DecayExemptSkills": []
}
```

### How It Works

1. **Grace Period**: After last skill use, no decay occurs for `DecayGracePeriodDays` (in-game days)
2. **Triangular Decay**: After grace period, decay increases each day
   - Day 1 past grace: -10 credits (base)
   - Day 2 past grace: -20 credits
   - Day 3 past grace: -30 credits
   - Total after 3 days: 60 credits lost
3. **Max Per Login**: Capped at `DecayMaxPointsPerLogin` per session to prevent total loss after long absences
4. **Activity Updates**: Using a skill resets its decay timer

### Affected Skills

- `mining` - Hardy trait (mining speed)
- `melee` - Soldier trait (melee damage)
- `ranged` - Focused trait (ranged damage/accuracy/distance)

### Exempt Skills

Add skill names to `DecayExemptSkills` array to prevent decay:
```json
{
  "DecayExemptSkills": ["mining", "melee"]
}
```

### Notification

Players are notified on login if any skills decayed:
> "Some skills have decayed due to lack of use (-X total credits). Use them to regain your progress!"
