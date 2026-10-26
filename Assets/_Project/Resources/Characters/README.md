# Character Assets

`ArcherRun.png` is the user-provided prototype player sprite sheet.

- Layout: 4 columns by 2 rows, read left-to-right and top-to-bottom.
- Runtime: transparent bounds are trimmed per frame and aligned at the feet.
- Display: the first frame is used while idle; all eight frames loop while moving.
- Distribution: confirm the source asset's redistribution rights before making the repository public.
# Dash animation

`ArcherDash.png` is the user-provided transparent 2560x1080 sheet, read left-to-right
then top-to-bottom as 4 columns and 3 rows. E backstep starts a 12-frame, 0.6-second
one-shot roll. Facing stays locked during the roll; movement/idle resumes afterward.
Backstep travel distance, movement duration and cooldown remain unchanged.
The full cells share a fixed anchor so rotation does not cause per-frame crop jitter.

