# Seraph Leveling Mod - Test Specification

## Overview

This document specifies the test cases for the Simple Improving Traits mod. The mod contains 14+ progression systems, negative trait cancellation, exclusive trait unlocks, and comprehensive configuration options.

**Test Framework:** NUnit or xUnit (to be determined)
**Mocking Strategy:** Mock Vintage Story API interfaces where needed

---

## 1. Mining Progression System

### 1.1 Credit Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MINE-001 | First credit at base increment | 100 stone blocks mined | 1 credit, 1% bonus |
| MINE-002 | Second credit requires more blocks | 200 additional blocks | 2 credits total, 2% bonus |
| MINE-003 | Ore multiplier applied | 20 ore blocks (5x = 100 points) | 1 credit |
| MINE-004 | Mixed block types | 50 stone + 10 ore (50+50=100) | 1 credit |
| MINE-005 | Max credits cap at 50 | Credits set to 60 | Clamped to 50 |
| MINE-006 | Zero blocks yields no credit | 0 blocks mined | 0 credits |
| MINE-007 | Partial progress preserved | 50 blocks mined | 0 credits, 50 blocks in increment |

### 1.2 Per-Pickaxe Tracking Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MINE-010 | Fresh pickaxe starts at base increment | New copper pickaxe | IncrementSize = 100 |
| MINE-011 | Increment increases after credit | 100 blocks with copper | IncrementSize → 200 |
| MINE-012 | Different pickaxes track separately | 100 copper, then 100 tin-bronze | 2 credits total |
| MINE-013 | Returning to pickaxe remembers progress | Copper (50 blocks), tin-bronze (100), copper (50 more) | 1 credit copper, 1 credit tin-bronze |
| MINE-014 | Returning pickaxe remembers increment size | Copper gets credit, switch to tin-bronze, switch back | Copper needs 200 for next |
| MINE-015 | Non-pickaxe tool yields no progress | Break stone with axe | No mining progress |

### 1.3 Block Type Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MINE-020 | Stone blocks detected | Block code `rock-granite` | 1 point |
| MINE-021 | Ore blocks detected | Block code `ore-copper-granite` | 5 points (default multiplier) |
| MINE-022 | Non-qualifying blocks ignored | Block code `soil-low` | 0 points |
| MINE-023 | Custom ore multiplier applied | OreMultiplier=10, break ore | 10 points |

### 1.4 Vanilla Hardy Trait Interaction Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MINE-030 | Commoner can earn full 50% | Commoner earns 50 credits | 50% bonus |
| MINE-031 | Hardy class has 10% base | Player with Hardy trait | Starts with 10% |
| MINE-032 | Hardy class max effective bonus | Hardy + 40 earned credits | 50% total (10% vanilla + 40% earned) |

### 1.5 Data Persistence Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MINE-040 | Save and load credits | Save with 25 credits, reload | 25 credits restored |
| MINE-041 | Save and load per-pickaxe progress | Partial progress on 3 pickaxes | All 3 pickaxe states restored |
| MINE-042 | v1 format migration to v3 | Load v1 format data | Converted to v3 without data loss |
| MINE-043 | v2 format migration to v3 | Load v2 format data | Converted to v3 without data loss |
| MINE-044 | Corrupt data handling | Invalid magic bytes | Graceful failure, reset to defaults |

---

## 2. Melee Damage Progression System

### 2.1 Credit Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MELEE-001 | First credit at 100 damage | 100 damage dealt | 1 credit, 1% bonus |
| MELEE-002 | Scaling increment | 200 additional damage | 2 credits total |
| MELEE-003 | Fractional damage accumulated | 33.33 + 33.33 + 33.34 damage | Accumulates to 100 |
| MELEE-004 | Max credits cap at 50 | 51+ credits earned | Clamped to 50 |
| MELEE-005 | Zero damage yields no credit | 0 damage dealt | 0 credits |

### 2.2 Per-Weapon Tracking Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MELEE-010 | Sword damage tracked | 100 damage with sword-copper | 1 credit |
| MELEE-011 | Falx damage tracked | 100 damage with falx-copper | 1 credit |
| MELEE-012 | Spear damage tracked | 100 damage with spear-copper | 1 credit |
| MELEE-013 | Different weapons track separately | 100 damage each with 3 weapons | 3 credits |
| MELEE-014 | Non-melee weapon ignored | Damage with bow (melee hit) | No melee progress |
| MELEE-015 | Weapon returns to previous increment | Sword-copper earns credit, switch, return | Needs 200 for next |

### 2.3 Weapon Type Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MELEE-020 | Sword detected | Item code `game:sword-copper` | Valid melee weapon |
| MELEE-021 | Blade detected | Item code `game:blade-copper` | Valid melee weapon |
| MELEE-022 | Longsword detected | Item code `game:longsword-iron` | Valid melee weapon |
| MELEE-023 | Shortsword detected | Item code `game:shortsword-iron` | Valid melee weapon |
| MELEE-024 | Falx detected | Item code `game:falx-copper` | Valid melee weapon |
| MELEE-025 | Spear detected | Item code `game:spear-copper` | Valid melee weapon |
| MELEE-026 | Knife not valid | Item code `game:knife-copper` | NOT valid melee weapon |
| MELEE-027 | Axe not valid | Item code `game:axe-copper` | NOT valid melee weapon |

### 2.4 Vanilla Soldier Trait Interaction Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MELEE-030 | Commoner can earn full 50% | Commoner earns 50 credits | 50% bonus |
| MELEE-031 | Soldier class has 30% base | Player with Soldier trait | Starts with 30% effective |
| MELEE-032 | Soldier class max effective bonus | Soldier + 20 earned credits | 50% total (30% vanilla + 20% earned) |

### 2.5 Negative Trait Cancellation Tests (Farsighted/Nervous)

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MELEE-040 | Hunter Farsighted starts at -15% | Hunter class, 0 credits | -15% melee penalty active |
| MELEE-041 | Farsighted partially cancelled | Hunter with 10 melee credits | -5% remaining penalty |
| MELEE-042 | Farsighted fully cancelled at 15 credits | Hunter with 15 melee credits | 0% penalty (trait removed from UI) |
| MELEE-043 | Hunter net bonus after cancellation | Hunter with 20 melee credits | +5% net bonus displayed |
| MELEE-044 | Nervous (Malefactor) cancellation | Malefactor with 15 melee credits | Nervous fully cancelled |
| MELEE-045 | Nervous (Clockmaker) cancellation | Clockmaker with 15 melee credits | Nervous fully cancelled |

---

## 3. Ranged Progression System

### 3.1 Credit Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| RANGED-001 | First credit at 100 damage | 100 ranged damage dealt | 1 credit |
| RANGED-002 | All three stats increase together | 1 credit earned | +1% damage, +1% accuracy, +1% distance |
| RANGED-003 | Individual stat caps apply | 50 damage credits, vanilla Focused | Damage capped at 50%, accuracy capped, distance capped |
| RANGED-004 | Max credits cap at 50 | 51+ credits earned | Clamped to 50 |

### 3.2 Per-Weapon Combo Tracking Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| RANGED-010 | Bow+arrow combo tracked | 100 damage with bow-long+arrow-copper | 1 credit |
| RANGED-011 | Different arrow type = different combo | bow-long+arrow-flint vs bow-long+arrow-copper | Tracked separately |
| RANGED-012 | Different bow type = different combo | bow-crude+arrow-copper vs bow-long+arrow-copper | Tracked separately |
| RANGED-013 | Sling+stone combo tracked | 100 damage with sling+stone-granite | 1 credit |
| RANGED-014 | Thrown spear tracked | 100 damage with thrownspear-copper | 1 credit |

### 3.3 Projectile Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| RANGED-020 | Arrow projectile detected | Projectile code `arrow-copper` | Valid ranged projectile |
| RANGED-021 | Stone projectile detected | Projectile code `stone-granite` | Valid ranged projectile |
| RANGED-022 | Thrown spear detected | Projectile code `thrownspear-copper` | Valid ranged projectile |
| RANGED-023 | Melee bow hit ignored | Bow used as melee | NOT ranged damage |
| RANGED-024 | PiercingAttack damage type | Arrow damage | Valid ranged damage |
| RANGED-025 | BluntAttack damage type | Stone damage | Valid ranged damage |

### 3.4 Vanilla Focused Trait Interaction Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| RANGED-030 | Commoner can earn full 50% each | Commoner earns 50 credits | 50% all stats |
| RANGED-031 | Focused class base bonuses | Player with Focused trait | +20% damage, +30% accuracy, +20% distance |
| RANGED-032 | Focused class effective caps | Focused player at max | 50% damage, 50% accuracy, 50% distance |

### 3.5 Negative Trait Cancellation Tests (Nearsighted/Frail)

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| RANGED-040 | Blackguard Nearsighted starts at -15% | Blackguard, 0 credits | -15% ranged damage penalty |
| RANGED-041 | Nearsighted partially cancelled | Blackguard with 10 credits | -5% remaining damage penalty |
| RANGED-042 | Nearsighted fully cancelled | Blackguard with 15 credits | 0% penalty |
| RANGED-043 | Frail distance penalty (-25%) | Malefactor, 0 credits | -25% ranged distance penalty |
| RANGED-044 | Frail HP penalty (-2.5 HP) tied to distance | Malefactor, 0 credits | -2.5 HP penalty |
| RANGED-045 | Frail both penalties cancel together | Malefactor with 25 ranged credits | Both -25% distance and -2.5 HP cancelled |

---

## 4. Walking Speed Progression System

### 4.1 Credit Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| WALK-001 | First credit at 1000 blocks | 1000 blocks walked | 1 credit, 1% bonus |
| WALK-002 | Scaling increment | 2000 additional blocks | 2 credits total |
| WALK-003 | Max credits cap at 15 | 16+ credits earned | Clamped to 15 |
| WALK-004 | Partial progress preserved | 500 blocks walked | 0 credits, 500 blocks in increment |

### 4.2 Distance Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| WALK-010 | Horizontal movement only (X/Z) | Move 100 blocks on X axis | 100 blocks counted |
| WALK-011 | Vertical movement ignored | Climb 100 blocks on Y axis | 0 blocks counted |
| WALK-012 | Diagonal movement calculated correctly | Move 3X + 4Z | 5 blocks (pythagorean) |
| WALK-013 | Teleport detection (>10 blocks) | Teleport 100 blocks | Movement discarded |
| WALK-014 | Normal speed cap (10 blocks/tick) | Walk 8 blocks in tick | 8 blocks counted |

### 4.3 Vanilla Fleetfooted Trait Interaction Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| WALK-020 | Commoner can earn full 15% | Commoner earns 15 credits | 15% bonus |
| WALK-021 | Fleetfooted class has 10% base | Player with Fleetfooted | Starts with 10% effective |
| WALK-022 | Fleetfooted class max effective | Fleetfooted + 5 earned credits | 15% total |

---

## 5. Hunger Rate Progression System

### 5.1 Credit Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| HUNGER-001 | First credit at 300 seconds | 300 seconds at full saturation | 1 credit |
| HUNGER-002 | Scaling increment (+60s each) | 300 + 360 + 420 seconds | 3 credits |
| HUNGER-003 | Non-Ravenous max 25 credits | Commoner earning credits | Max 25 credits (75% target rate) |
| HUNGER-004 | Ravenous max 55 credits | Blackguard earning credits | Max 55 credits (75% target rate) |

### 5.2 Saturation Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| HUNGER-010 | Full saturation detected | 1500/1500 saturation | Time counted |
| HUNGER-011 | Partial saturation ignored | 1400/1500 saturation | Time NOT counted |
| HUNGER-012 | Time tracked per second | Full saturation for 5 seconds | 5 seconds added |

### 5.3 Vanilla Ravenous Trait Interaction Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| HUNGER-020 | Commoner base rate 100% | Commoner, 0 credits | 100% hunger rate |
| HUNGER-021 | Ravenous base rate 130% | Blackguard, 0 credits | 130% hunger rate |
| HUNGER-022 | Both classes reach 75% target | Max credits earned | 75% hunger rate for both |
| HUNGER-023 | Ravenous remaining penalty display | Blackguard with 10 credits | 20% remaining penalty shown |
| HUNGER-024 | Ravenous penalty removed at 30 credits | Blackguard with 30 credits | Ravenous trait removed from UI |

---

## 6. Armor Progression System

### 6.1 First-Equip Bonus Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| ARMOR-001 | Light armor first equip | Equip leather armor first time | +1% durability, +1% walk speed reduction |
| ARMOR-002 | Chain armor first equip | Equip chain armor first time | +1% durability, +1% walk speed reduction |
| ARMOR-003 | Brigandine first equip | Equip brigandine first time | +2% durability, +2% walk speed reduction |
| ARMOR-004 | Scale armor first equip | Equip scale armor first time | +3% durability, +3% walk speed reduction |
| ARMOR-005 | Plate armor first equip | Equip plate armor first time | +3% durability, +3% walk speed reduction |
| ARMOR-006 | Repeat equip no bonus | Equip same armor again | No additional bonus |
| ARMOR-007 | Different armor same tier | Equip different plate armor | Additional bonus granted |

### 6.2 Damage Blocked Progression Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| ARMOR-010 | First damage credit | 100 damage blocked | 1 durability credit |
| ARMOR-011 | Scaling increment | 200 additional blocked | 2 durability credits total |
| ARMOR-012 | Per-armor tracking | 100 blocked by chest, 100 by legs | 2 credits (1 each) |
| ARMOR-013 | Hit distribution (head 20%) | 500 total damage, head hit | 100 allocated to head armor |
| ARMOR-014 | Hit distribution (body 50%) | 200 total damage, body hit | 100 allocated to body armor |
| ARMOR-015 | Hit distribution (legs 30%) | 333 total damage, leg hit | 100 allocated to leg armor |

### 6.3 Armor Repair Progression Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| ARMOR-020 | First repair credit | 1 armor repair | 1 durability credit |
| ARMOR-021 | Scaling increment (repairs) | 1 + 2 + 3 repairs | 3 durability credits |
| ARMOR-022 | Per-armor repair tracking | Repair chest 1x, legs 1x | 2 credits (1 each) |

### 6.4 Armor Type Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| ARMOR-030 | Plate armor detected | Code `armor-body-plate-iron` | Plate tier (3 credits) |
| ARMOR-031 | Scale armor detected | Code `armor-body-scale-iron` | Scale tier (3 credits) |
| ARMOR-032 | Brigandine detected | Code `armor-body-brigandine-iron` | Brigandine tier (2 credits) |
| ARMOR-033 | Chain armor detected | Code `armor-body-chain-iron` | Chain tier (1 credit) |
| ARMOR-034 | Lamellar as chain | Code `armor-body-lamellar-iron` | Chain tier (1 credit) |
| ARMOR-035 | Leather as light | Code `armor-body-leather` | Light tier (1 credit) |
| ARMOR-036 | Gambeson as light | Code `armor-body-gambeson` | Light tier (1 credit) |
| ARMOR-037 | Improvised as light | Code `armor-body-improvised` | Light tier (1 credit) |
| ARMOR-038 | Non-armor ignored | Code `clothes-upperbody-shirt` | Not armor |

### 6.5 Vanilla Soldier Trait Interaction Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| ARMOR-040 | Commoner max durability | Commoner earns 50 durability credits | 50% bonus |
| ARMOR-041 | Commoner max walk speed reduction | Commoner earns 50 walk speed credits | 50% bonus |
| ARMOR-042 | Soldier base durability | Soldier trait | +15% durability base |
| ARMOR-043 | Soldier base walk speed | Soldier trait | -25% armor penalty base |
| ARMOR-044 | Soldier effective durability cap | Soldier + 35 earned | 50% total durability |
| ARMOR-045 | Soldier effective walk speed cap | Soldier + 25 earned | 50% total walk speed reduction |

---

## 7. Clothier Trait System (Unlock)

### 7.1 Unlock Requirement Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| CLOTH-001 | Progress below threshold | 19 unique clothes worn | NOT unlocked |
| CLOTH-002 | Unlock at threshold | 20 unique clothes worn | Unlocked |
| CLOTH-003 | Unique items only counted | Same shirt worn 20 times | 1 unique item |
| CLOTH-004 | Different items counted | 20 different shirts | 20 unique items |

### 7.2 Clothing Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| CLOTH-010 | `clothes-` prefix detected | Code `clothes-upperbody-shirt-linen` | Valid clothing |
| CLOTH-011 | `shirt-` prefix detected | Code `shirt-linen` | Valid clothing |
| CLOTH-012 | `trousers-` prefix detected | Code `trousers-linen` | Valid clothing |
| CLOTH-013 | `dress-` prefix detected | Code `dress-wool` | Valid clothing |
| CLOTH-014 | `hat-` prefix detected | Code `hat-straw` | Valid clothing |
| CLOTH-015 | Armor not clothing | Code `armor-body-plate` | NOT clothing |

---

## 8. Mender Trait System

### 8.1 Credit Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MEND-001 | First credit at 5 repairs | 5 sewing kit repairs | 1 credit |
| MEND-002 | Scaling increment (+1) | 5 + 6 + 7 repairs | 3 credits |
| MEND-003 | Max credits cap at 20 | 21+ credits earned | Clamped to 20 |

### 8.2 Repair Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MEND-010 | Sewing kit repair detected | Repair with sewing kit | Progress added |
| MEND-011 | Non-sewing repair ignored | Repair with other item | No progress |

### 8.3 Vanilla Mender Trait Interaction Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| MEND-020 | Commoner max bonus | Commoner earns 20 credits | 20% bonus |
| MEND-021 | Mender trait base | Player with Mender trait | +10% base |
| MEND-022 | Mender effective cap | Mender + 10 earned | 20% total |

---

## 9. Pilferer Trait System

### 9.1 Credit Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| PILF-001 | First credit at 10 points | 5 vessels broken (10 points) | 1 credit |
| PILF-002 | Chest opening grants points | 10 chests opened | 1 credit (10 points) |
| PILF-003 | Mixed sources | 3 vessels (6) + 4 chests (4) | 1 credit (10 points) |
| PILF-004 | Scaling increment | 10 + 20 + 30 points | 3 credits |
| PILF-005 | Max credits cap at 20 | 21+ credits | Clamped to 20 |

### 9.2 Point Source Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| PILF-010 | Vessel break = 2 points | Break 1 vessel | 2 points |
| PILF-011 | First chest open = 1 point | Open new chest | 1 point |
| PILF-012 | Repeat chest = 0 points | Open same chest again | 0 points |
| PILF-013 | Different chest position | Open chest at new position | 1 point |

### 9.3 Block Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| PILF-020 | Cracked vessel detected | Block `crackedvessel` | Valid vessel |
| PILF-021 | Storage vessel detected | Block `storagevessel` | Valid vessel |
| PILF-022 | Urn detected | Block `urn-*` | Valid vessel |
| PILF-023 | Regular chest not vessel | Block `chest-*` | NOT vessel (grants chest points instead) |

### 9.4 Vanilla Pilferer Trait Interaction Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| PILF-030 | Commoner max bonus | Commoner earns 20 credits | 20% all stats |
| PILF-031 | Pilferer trait bases | Player with Pilferer | +10% gear, +15% vessel, +12% collection |
| PILF-032 | Pilferer effective caps | Pilferer + 10 earned | 20% all stats |

---

## 10. Resourceful Trait System

### 10.1 Credit Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| RES-001 | First credit at 10 harvests | 10 animals harvested | 1 credit |
| RES-002 | Scaling increment | 10 + 20 + 30 harvests | 3 credits |
| RES-003 | Max loot credits 20 | 21+ loot credits | Clamped to 20 |
| RES-004 | Max speed credits 25 | 26+ speed credits | Clamped to 25 |

### 10.2 Harvest Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| RES-010 | Animal harvest detected | Complete harvest action | 1 progress |
| RES-011 | Partial harvest not counted | Start but don't finish | 0 progress |

### 10.3 Vanilla Resourceful Trait Interaction Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| RES-020 | Commoner loot max | Commoner earns credits | 20% loot bonus |
| RES-021 | Commoner speed max | Commoner earns credits | 25% speed bonus |
| RES-022 | Resourceful trait bases | Player with Resourceful | +10% loot, +25% speed |
| RES-023 | Resourceful loot cap | Resourceful + 10 earned | 20% loot (10 more available) |
| RES-024 | Resourceful speed cap | Resourceful trait | Already at 25% cap |

---

## 11. Forager Trait System

### 11.1 Credit Calculation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| FOR-001 | First credit at 10 crops | 10 wild crops broken | 1 credit |
| FOR-002 | Scaling increment | 10 + 20 + 30 crops | 3 credits |
| FOR-003 | Max foraging loot 20 | 21+ foraging credits | Clamped to 20 |
| FOR-004 | Max wild crop drop 20 | 21+ wild crop credits | Clamped to 20 |

### 11.2 Wild Crop Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| FOR-010 | Tallgrass detected | Block `tallgrass` | Valid wild crop |
| FOR-011 | Flower detected | Block `flower-*` | Valid wild crop |
| FOR-012 | Mushroom detected | Block `mushroom-*` | Valid wild crop |
| FOR-013 | Berry detected | Block `berry-*` | Valid wild crop |
| FOR-014 | Cattail detected | Block `cattail` | Valid wild crop |
| FOR-015 | Fern detected | Block `fern` | Valid wild crop |
| FOR-016 | Wild vine detected | Block `wildvine` | Valid wild crop |
| FOR-017 | Reeds detected | Block `reeds` | Valid wild crop |
| FOR-018 | Waterlily detected | Block `waterlily` | Valid wild crop |
| FOR-019 | Seaweed detected | Block `seaweed` | Valid wild crop |
| FOR-020 | Wild crop variant | Block `crop-*wild*` | Valid wild crop |
| FOR-021 | Cultivated crop ignored | Block `crop-wheat-1` | NOT wild crop |

### 11.3 Negative Trait Cancellation Tests (Civil/Heavyhanded)

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| FOR-030 | Tailor Civil starts at -10% | Tailor, 0 credits | -10% foraging loot |
| FOR-031 | Civil partially cancelled | Tailor with 5 credits | -5% remaining |
| FOR-032 | Civil fully cancelled | Tailor with 10 credits | 0% penalty |
| FOR-033 | Heavyhanded foraging -15% | Blackguard, 0 credits | -15% foraging loot |
| FOR-034 | Heavyhanded foraging partial | Blackguard with 10 credits | -5% remaining |
| FOR-035 | Heavyhanded foraging cancelled | Blackguard with 15 credits | 0% penalty |
| FOR-036 | Heavyhanded wild crop -20% | Blackguard, 0 credits | -20% wild crop |
| FOR-037 | Heavyhanded wild crop cancelled | Blackguard with 20 credits | 0% penalty |

---

## 12. Technical Trait System (Unlock)

### 12.1 Unlock Requirement Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| TECH-001 | Progress below threshold | 4 translocators repaired | NOT unlocked |
| TECH-002 | Unlock at threshold | 5 translocators repaired | Unlocked |
| TECH-003 | Unlock effect applied | Technical unlocked | temporalGearTLRepairCost = -1 |

### 12.2 Repair Detection Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| TECH-010 | Full repair counted | Complete translocator repair | 1 progress |
| TECH-011 | Partial repair not counted | Add gear but not complete | 0 progress |

### 12.3 Tinkerer Dependency Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| TECH-020 | Tinkerer requires Technical | Technical locked, 10% Precise | Tinkerer NOT unlocked |
| TECH-021 | Tinkerer requires Precise | Technical unlocked, 0% Precise | Tinkerer NOT unlocked |
| TECH-022 | Tinkerer unlocks with both | Technical + 10% Precise | Tinkerer unlocked |

---

## 13. Negative Trait Cancellation Integration Tests

### 13.1 Tailor Class Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| NEG-T01 | Tailor Civil foraging | Forager level 10 | Civil cancelled |
| NEG-T02 | Tailor Weak mining | Mining level 10 | Weak mining cancelled |
| NEG-T03 | Tailor Weak HP tied to mining | Mining level 10 | Weak HP also cancelled |
| NEG-T04 | Tailor Kind loot | Resourceful level 10 | Kind loot cancelled |
| NEG-T05 | Tailor Kind speed | Resourceful level 25 | Kind speed cancelled |
| NEG-T06 | Tailor extended Forager max | Tailor class | Forager max = 30 (not 20) |
| NEG-T07 | Tailor extended Resourceful max | Tailor class | Resourceful max = 35 loot, 50 speed |

### 13.2 Hunter Class Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| NEG-H01 | Hunter Farsighted melee | Melee level 15 | Farsighted cancelled |
| NEG-H02 | Hunter Claustrophobic mining | Mining level 10 | Claustrophobic mining cancelled |
| NEG-H03 | Hunter Claustrophobic ore tied | Mining level 10 | Claustrophobic ore also cancelled |
| NEG-H04 | Hunter extended Soldier max | Hunter class | Soldier max = 65 (not 50) |

### 13.3 Malefactor/Clockmaker Class Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| NEG-M01 | Malefactor Nervous melee | Melee level 15 | Nervous cancelled |
| NEG-M02 | Clockmaker Nervous melee | Melee level 15 | Nervous cancelled |
| NEG-M03 | Malefactor Frail distance | Ranged level 25 | Frail distance cancelled |
| NEG-M04 | Malefactor Frail HP tied | Ranged level 25 | Frail HP also cancelled |

### 13.4 Blackguard Class Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| NEG-B01 | Blackguard Nearsighted | Ranged level 15 | Nearsighted cancelled |
| NEG-B02 | Blackguard Heavyhanded vessel | Pilferer level 10 | Heavyhanded vessel cancelled |
| NEG-B03 | Blackguard Heavyhanded foraging | Forager level 15 | Heavyhanded foraging cancelled |
| NEG-B04 | Blackguard Heavyhanded wild | Forager level 20 | Heavyhanded wild crop cancelled |
| NEG-B05 | Blackguard Ravenous penalty removed | Hunger level 30 | Ravenous trait removed |
| NEG-B06 | Blackguard hunger continues to 75% | Hunger level 55 | 75% hunger rate achieved |

---

## 14. Command Handler Tests

### 14.1 View Commands (Read-Only)

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| CMD-001 | View mining stats | `/trait mining` | Shows credits, bonus, per-pickaxe progress |
| CMD-002 | View melee stats | `/trait melee` | Shows credits, bonus, per-weapon progress |
| CMD-003 | View ranged stats | `/trait ranged` | Shows all three stats |
| CMD-004 | View walking stats | `/trait walking` | Shows credits, bonus, blocks in increment |
| CMD-005 | View hunger stats | `/trait hunger` | Shows credits, class-specific max |
| CMD-006 | View armor stats | `/trait armor` | Shows both durability and walk speed |
| CMD-007 | View clothier stats | `/trait clothier` | Shows unique clothes, unlock status |
| CMD-008 | View mender stats | `/trait mender` | Shows repairs, credits |
| CMD-009 | View pilferer stats | `/trait pilferer` | Shows points, credits |
| CMD-010 | View resourceful stats | `/trait resourceful` | Shows harvests, credits |
| CMD-011 | View forager stats | `/trait forager` | Shows crops, credits |
| CMD-012 | View technical stats | `/trait technical` | Shows repairs, unlock status |

### 14.2 Admin Set Commands

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| CMD-020 | Set mining level | `/trait mininglevel 25` | Credits set to 25 |
| CMD-021 | Set melee level | `/trait meleelevel 25` | Credits set to 25 |
| CMD-022 | Set ranged level | `/trait rangedlevel 25` | Credits set to 25 |
| CMD-023 | Set walking level | `/trait walkinglevel 10` | Credits set to 10 |
| CMD-024 | Set hunger level | `/trait hungerlevel 25` | Credits set to 25 |
| CMD-025 | Set armor durability | `/trait armorlevel 25` | Durability credits set to 25 |
| CMD-026 | Set armor walk speed | `/trait armorwalkspeedlevel 25` | Walk speed credits set to 25 |
| CMD-027 | Level exceeds max clamped | `/trait mininglevel 100` | Clamped to max (50) |
| CMD-028 | Negative level rejected | `/trait mininglevel -5` | Error or clamped to 0 |

### 14.3 Admin Config Commands

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| CMD-030 | Set mining base | `/trait miningbase 200` | BaseBlocksPerIncrement = 200 |
| CMD-031 | Set melee base | `/trait meleebase 200` | BaseDamagePerIncrement = 200 |
| CMD-032 | Set mining max | `/trait miningmax 75` | MiningMaxPercent = 75 |
| CMD-033 | Get config value | `/trait miningbase` | Returns current value |

### 14.4 Global Admin Commands

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| CMD-040 | Reset all traits | `/trait reset` | All player traits reset to 0 |
| CMD-041 | Reset config | `/trait resetconfig` | All config values reset to defaults |
| CMD-042 | Max all traits | `/trait maxall` | All traits set to maximum (testing) |

---

## 15. Data Persistence Tests

### 15.1 Save/Load Cycle Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| PERS-001 | Mining data survives restart | Save with progress, restart | Progress restored |
| PERS-002 | Melee data survives restart | Save with progress, restart | Progress restored |
| PERS-003 | Ranged data survives restart | Save with progress, restart | Progress restored |
| PERS-004 | Walking data survives restart | Save with progress, restart | Progress restored |
| PERS-005 | Hunger data survives restart | Save with progress, restart | Progress restored |
| PERS-006 | Armor data survives restart | Save with progress, restart | Progress restored |
| PERS-007 | Clothier data survives restart | Save with progress, restart | Progress restored |
| PERS-008 | Mender data survives restart | Save with progress, restart | Progress restored |
| PERS-009 | Pilferer data survives restart | Save with progress, restart | Progress restored |
| PERS-010 | Resourceful data survives restart | Save with progress, restart | Progress restored |
| PERS-011 | Forager data survives restart | Save with progress, restart | Progress restored |
| PERS-012 | Technical data survives restart | Save with progress, restart | Progress restored |

### 15.2 Multi-Player Isolation Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| PERS-020 | Player A data isolated | Player A mines, Player B mines | Separate progress |
| PERS-021 | Player disconnect doesn't affect others | Player A disconnects | Player B data intact |
| PERS-022 | Concurrent save safety | Two players save simultaneously | No data corruption |

### 15.3 Format Migration Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| PERS-030 | Mining v1 → v3 migration | Load v1 format | Converted without data loss |
| PERS-031 | Mining v2 → v3 migration | Load v2 format | Converted without data loss |
| PERS-032 | Invalid format handling | Corrupt magic bytes | Graceful reset |

---

## 16. Stat Application Tests

### 16.1 Mining Stat Application

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| STAT-001 | Mining bonus applied | 25 credits | sitMiningBonus = 0.25 |
| STAT-002 | Mining bonus updates | Credit increases | Stat updated immediately |

### 16.2 Melee Stat Application

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| STAT-010 | Melee bonus applied | 25 credits | sitMeleeBonus = 0.25 |
| STAT-011 | Soldier interaction | Vanilla Soldier + 10 earned | Net bonus calculated correctly |

### 16.3 Ranged Stat Application

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| STAT-020 | Ranged damage applied | 25 credits | sitRangedDamageBonus = 0.25 |
| STAT-021 | Ranged accuracy applied | 25 credits | sitRangedAccuracyBonus = 0.25 |
| STAT-022 | Ranged distance applied | 25 credits | sitRangedDistanceBonus = 0.25 |

### 16.4 Walking Stat Application

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| STAT-030 | Walking bonus applied | 10 credits | sitWalkingBonus = 0.10 |

### 16.5 Hunger Stat Application

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| STAT-040 | Hunger reduction applied | 25 credits (Commoner) | hungerrate = -0.25 |
| STAT-041 | Ravenous reduction applied | 55 credits (Blackguard) | hungerrate = -0.55 |

### 16.6 Armor Stat Application

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| STAT-050 | Durability bonus applied | 25 credits | armorDurabilityLoss = -0.25 |
| STAT-051 | Walk speed bonus applied | 25 credits | walkspeed += 0.25 |

---

## 17. WatchedAttributes Sync Tests

### 17.1 Client Sync Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| SYNC-001 | Mining level synced | Credit changes | sitMiningLevel updated |
| SYNC-002 | Mining bonus synced | Credit changes | sitMiningBonusPercent updated |
| SYNC-003 | Negative trait remaining synced | Cancellation progress | sitCivilRemaining updated |
| SYNC-004 | Sync only on change | No credit change | No attribute update |

### 17.2 Vanilla Trait Detection Sync

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| SYNC-010 | Hardy trait detected | Player has Hardy | sitHasVanillaHardy = true |
| SYNC-011 | Soldier trait detected | Player has Soldier | sitHasVanillaSoldier = true |
| SYNC-012 | Focused trait detected | Player has Focused | sitHasVanillaFocused = true |
| SYNC-013 | Negative trait detected | Player has Civil | sitHasCivil = true |

---

## 18. Edge Case Tests

### 18.1 Boundary Conditions

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| EDGE-001 | Zero to one credit transition | Exactly base increment | 1 credit earned |
| EDGE-002 | Max credit boundary | One below max, earn one | Reaches max exactly |
| EDGE-003 | At max, no overflow | Already at max, earn more | Stays at max |
| EDGE-004 | Floating point damage | 99.9999 damage | Rounds correctly |

### 18.2 Race Conditions

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| EDGE-010 | Rapid block breaking | 100 blocks in 1 second | All counted correctly |
| EDGE-011 | Rapid damage dealing | 100 hits in 1 second | All counted correctly |
| EDGE-012 | Save during progress update | Save while earning credit | No data loss |

### 18.3 Invalid State Handling

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| EDGE-020 | Null player reference | Player disconnects mid-action | Graceful handling |
| EDGE-021 | Missing tool in hand | Break block with empty hand | No crash, no progress |
| EDGE-022 | Invalid item code | Item with null code | Graceful handling |

---

## 19. Performance Tests

### 19.1 Memory Usage

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| PERF-001 | Memory per player | 1000 players with full progress | Reasonable memory usage |
| PERF-002 | Per-tool dictionary growth | 100 unique tools per player | Dictionary size reasonable |

### 19.2 Processing Time

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|-----------------|
| PERF-010 | Block break processing | 1000 blocks/second | No noticeable lag |
| PERF-011 | Damage processing | 100 hits/second | No noticeable lag |
| PERF-012 | Save operation | 100 players | Completes quickly |

---

## 20. Integration Test Scenarios

### 20.1 Full Player Progression Scenario

| Test ID | Description | Steps |
|---------|-------------|-------|
| INT-001 | New player full progression | Create player → Mine blocks → Deal damage → Walk → Reach full saturation → Verify all systems progress |
| INT-002 | Class-specific progression | Create Hunter → Verify Focused base → Earn ranged credits → Verify Farsighted cancellation |
| INT-003 | Multi-system interaction | Earn mining credits → Earn armor credits → Verify Hardy health unlock |

### 20.2 Server Restart Scenario

| Test ID | Description | Steps |
|---------|-------------|-------|
| INT-010 | Progress survives restart | Earn progress → Save → Restart server → Verify all progress restored |
| INT-011 | Config survives restart | Change config → Restart → Verify config persisted |

### 20.3 Multi-Player Scenario

| Test ID | Description | Steps |
|---------|-------------|-------|
| INT-020 | Independent player progress | Player A mines → Player B mines → Verify separate progress |
| INT-021 | Player join with existing data | Player joins with saved data → Verify loaded correctly |

---

## Test Implementation: In-Game Test Suite

### Command Structure

```
/trait testsuite              - Run all automated tests
/trait testsuite <category>   - Run specific category (mining, melee, ranged, etc.)
/trait testsuite list         - List all test categories
/trait testsuite verbose      - Run with detailed output
```

### Test Categories

| Category | Command | Tests |
|----------|---------|-------|
| Mining calculations | `/trait testsuite mining` | Credit math, increment scaling, ore multiplier |
| Melee calculations | `/trait testsuite melee` | Damage tracking, weapon detection |
| Ranged calculations | `/trait testsuite ranged` | Triple-stat progression, weapon combos |
| Walking calculations | `/trait testsuite walking` | Distance math, teleport detection |
| Hunger calculations | `/trait testsuite hunger` | Saturation tracking, class-specific caps |
| Armor calculations | `/trait testsuite armor` | Equip bonuses, damage blocked, repairs |
| Negative traits | `/trait testsuite negative` | Cancellation math for all classes |
| Stat application | `/trait testsuite stats` | Verify stats applied correctly |
| Data persistence | `/trait testsuite persistence` | Save/load cycle verification |
| All tests | `/trait testsuite` | Run everything |

### Test Output Format

```
[SeraphLeveling Tests] Running mining tests...
  [PASS] MINE-001: First credit at base increment
  [PASS] MINE-002: Second credit requires more blocks
  [FAIL] MINE-003: Ore multiplier applied
         Expected: 1 credit, Got: 0 credits
  [PASS] MINE-004: Mixed block types
  ...
[SeraphLeveling Tests] Mining: 24/25 passed (96%)

[SeraphLeveling Tests] === SUMMARY ===
  Mining:     24/25 (96%)
  Melee:      25/25 (100%)
  Ranged:     25/25 (100%)
  ...
  TOTAL:      198/200 (99%)
```

### Test Types

#### 1. Automated Unit Tests (No Player Action Required)
These test pure calculation logic and can run instantly:

- Credit calculation formulas
- Increment scaling math
- Per-tool/weapon progress tracking logic
- Block/item code detection (pattern matching)
- Negative trait cancellation calculations
- Stat value calculations
- WatchedAttributes value verification
- Data serialization/deserialization

#### 2. State Verification Tests
These verify current player state is consistent:

- Stats match expected values for current credits
- WatchedAttributes synced correctly
- Vanilla trait detection accurate
- Negative trait remaining values correct

#### 3. Interactive Tests (Optional, Require Player Action)
These can be triggered separately and verify real gameplay:

```
/trait testsuite interactive mining   - "Break 10 stone blocks now..."
/trait testsuite interactive melee    - "Deal damage to a creature now..."
```

### Implementation Architecture

```csharp
public class TraitTestSuite
{
    private ICoreServerAPI api;
    private IServerPlayer player;
    private List<TestResult> results;

    // Test runner
    public void RunAllTests(IServerPlayer player, bool verbose);
    public void RunCategory(string category, IServerPlayer player, bool verbose);

    // Test categories
    private void RunMiningTests();
    private void RunMeleeTests();
    private void RunRangedTests();
    private void RunWalkingTests();
    private void RunHungerTests();
    private void RunArmorTests();
    private void RunNegativeTraitTests();
    private void RunStatApplicationTests();
    private void RunPersistenceTests();

    // Assertion helpers
    private void AssertEqual<T>(string testId, string desc, T expected, T actual);
    private void AssertTrue(string testId, string desc, bool condition);
    private void AssertInRange(string testId, string desc, float value, float min, float max);
}
```

### Test Data Isolation

Tests use temporary data to avoid affecting real player progress:

```csharp
// Before tests
var backupData = BackupPlayerProgress(player);

// Run tests with isolated data
RunTestsWithTestData();

// After tests
RestorePlayerProgress(player, backupData);
```

### What Can Be Fully Automated

| Test Area | Automatable | Notes |
|-----------|-------------|-------|
| Credit calculations | ✅ Yes | Pure math, no game state needed |
| Increment scaling | ✅ Yes | Pure math |
| Per-tool tracking logic | ✅ Yes | Dictionary operations |
| Block code detection | ✅ Yes | String pattern matching |
| Weapon code detection | ✅ Yes | String pattern matching |
| Negative trait math | ✅ Yes | Pure calculations |
| Stat value calculation | ✅ Yes | Formula verification |
| Data serialization | ✅ Yes | Byte array operations |
| Vanilla trait detection | ⚠️ Partial | Needs player with traits |
| Stat application | ⚠️ Partial | Can verify after manual set |
| WatchedAttributes sync | ⚠️ Partial | Can verify values exist |
| Actual progression | ❌ No | Requires gameplay actions |
| Multi-player isolation | ❌ No | Requires multiple players |
| Server restart | ❌ No | Requires actual restart |

### Estimated Test Coverage

**Fully Automated:** ~150 tests (75%)
- All calculation tests
- All detection tests
- All negative trait cancellation tests
- Data persistence (in-memory)

**State Verification:** ~30 tests (15%)
- Current player stat verification
- WatchedAttributes consistency

**Manual/Interactive:** ~20 tests (10%)
- Actual progression events
- Multi-player scenarios

---

## Approval Checklist

- [ ] Mining progression tests comprehensive
- [ ] Melee progression tests comprehensive
- [ ] Ranged progression tests comprehensive
- [ ] Walking progression tests comprehensive
- [ ] Hunger progression tests comprehensive
- [ ] Armor progression tests comprehensive
- [ ] Unlock trait tests comprehensive
- [ ] Negative trait cancellation tests comprehensive
- [ ] Command handler tests comprehensive
- [ ] Data persistence tests comprehensive
- [ ] Edge case tests comprehensive
- [ ] Integration tests comprehensive
- [ ] Mocking strategy approved
- [ ] Test framework chosen (NUnit/xUnit)
