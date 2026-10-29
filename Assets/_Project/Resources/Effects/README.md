# Skill 1: Q double shot

`SkillOneVolley.png` is the supplied transparent 1026x684 sheet. Read the 3x2 grid
left-to-right, top-to-bottom, including the final empty cell. The reference GIF
contains six 80 ms frames: the effect plays once for 0.48 seconds, then cleans up.

The source art faces left. Its fixed emission anchor is rotated toward the aim
direction at cast time. This is a visual fan effect; the existing two projectiles,
their 0.12-second firing interval, damage and 4-second cooldown remain unchanged.
Only Q's placeholder rectangles are hidden when the effect loads successfully.
E backstep and right-click focus keep their existing behavior.

# Skill 2: R arrow rain

`SkillTwoArrowRain.png` is the supplied transparent 1026x1026, 3x3 sheet.
All nine cells play once at 80 ms per frame (0.72 seconds), matching the GIF.
The last cell clears the effect. The target is the center of the ground ring;
the arrows always fall down without rotating toward the mouse.

Press R to cast at the mouse position, clamped to 6 units from the player.
The first impact (frame 3, 0.24 seconds) damages enemies inside the ring once,
even if they have multiple colliders. Players and neutral NPCs are not damaged.
Prototype values: 30 damage, 8-second cooldown, ellipse radii 1.3 x 0.45 units.
Right-click focus boosts this damage by the same 1.5 multiplier as arrows.
Values are adjustable on ArcherController; E backstep remains unchanged.

# Skill 3: T power shot

Separate user-provided 3x3 sheets: `SkillThreeCast.png` (1254x1254) and
`SkillThreeFlight.png` (1026x1026). Both use nine 80 ms frames, matching the GIFs.
The bow follows the caster for 0.72 seconds. On its frame-7 release flash (0.56s),
one real projectile spawns. The flight animation loops until impact or its 1.5s
lifetime expires. The cast-time aim direction is retained throughout.

The flight sprite's tip is anchored at the projectile's collision position.
A two-pixel cell inset excludes the source sheets' green guides. Placeholder
scaling is removed so the arrow is not stretched. Existing projectile collision
handles enemy damage and obstacles, and destroys its attached visual on impact.
Prototype values: damage 50, cooldown 10s, speed 9 units/s; focus boosts damage 1.5x.

# Basic attack: left mouse button

`BasicAttackBow.png` is the original transparent 1254x1254 2x2 sheet.
Its four poses play once in row order at 60 ms per frame. On the final pose
(0.18s), one projectile using `BasicAttackArrow.png` launches toward the aim
direction captured at attack start. The bow follows the player's upper body
and clears at 0.24s. Both source images point northeast and rotate to aim.
The arrow tip follows the existing collision position; impact or lifetime
expiry removes its visual. Holding left mouse repeats at the existing 0.45s
interval (0.27s while focused). Damage remains 10, boosted to 15 by focus.
