# Shikaku Go  Apple Game Center achievements

This folder is the source of truth for the ten version 1 achievements.

- `GameCenterAchievements.csv` contains the exact permanent IDs, points,
  English localization, image filenames, and matching in-game rules.
- `Achievements/` contains the 1024 � 1024 PNG files to upload.
- `Achievements/Source/` contains editable flat SVG originals.
- Total achievement points: 700 of Apple's 1000-point app limit.
- None are hidden or repeatable.

## App Store Connect setup

1. Open Shikaku Go (Apple ID REPLACE_WITH_SHIKAKU_APPLE_APP_ID) in App Store Connect.
2. Open Game Center and add each achievement from the CSV.
3. Copy every achievement ID exactly. Apple does not allow changing an ID
   after the achievement is created.
4. Add the English (U.S.) title and descriptions, points, and matching PNG.
5. Leave Hidden and Achievable More Than Once turned off.
6. On the iOS app version, enable Game Center and select all ten achievements
   for the version.
7. Submit the Game Center achievements with the first iOS build.

The game reports percentage progress for the incremental achievements and
queues unsent progress locally until Game Center authentication succeeds.
