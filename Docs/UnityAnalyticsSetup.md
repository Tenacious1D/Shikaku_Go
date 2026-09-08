# Unity Analytics dashboard setup

The Unity-side analytics implementation is complete. Analytics is off until
the player explicitly enables it. The first-run Welcome screen acknowledges
the Terms of Service and Privacy Policy separately from the optional analytics
choice. Google UMP ad consent remains a separate flow.

## 1. Create environments

In the Unity Dashboard, create an environment named exactly `development`.
Keep the existing `production` environment.

- Unity Editor and Development Builds send to `development`.
- Non-development release builds send to `production`.

## 2. Create custom events

In Analytics > Event Manager, create and enable these custom events. Parameter
names and types are case-sensitive.

| Event | Parameters |
| --- | --- |
| `screen_viewed` | `screen` string |
| `puzzle_started` | `mode` string, `board_width` int, `board_height` int, `puzzle_id` string, `pack_id` string, `level_index` int, `daily_key` string |
| `puzzle_completed` | all puzzle parameters above, `completion_seconds` float, `hints_used` int |
| `puzzle_abandoned` | all puzzle parameters above, `elapsed_seconds` float, `filled_cells` int |
| `hint_used` | all puzzle parameters above, `source` string, `hints_used` int |
| `time_trial_started` | `board_width` int, `board_height` int, `duration_seconds` int |
| `time_trial_finished` | `board_width` int, `board_height` int, `score` int, `puzzles_completed` int, `best_score` int, `duration_seconds` float, `finish_reason` string |
| `tutorial_started` | no custom parameters |
| `tutorial_step_reached` | `step` int, `step_name` string |
| `tutorial_completed` | no custom parameters |
| `tutorial_exited` | `last_step` int |
| `rewarded_ad_requested` | `placement` string |
| `rewarded_ad_completed` | `placement` string, `reward_type` string |
| `purchase_completed` | `product_id` string, `product_type` string, `quantity` int, `price` double, `currency` string |

`daily_key` is only supplied for Daily puzzles. If Event Manager requires
every assigned parameter, make it optional.

Create and test the definitions in `development`, then use Event Manager's
copy feature to copy them to `production`.

## 3. Test

1. Clear local PlayerPrefs once to retest the first-launch flow.
2. Start the app and verify Google UMP appears only when required, followed by
   the Welcome screen before the tutorial.
3. Leave Usage Analytics off, select Continue, and confirm no custom events
   arrive.
4. Clear local PlayerPrefs, enable Usage Analytics on the Welcome screen, and
   confirm collection begins only after Continue.
5. Relaunch and confirm the Welcome screen does not appear again.
6. Open Settings > Privacy Preferences, turn Usage Analytics off and on, and
   confirm collection follows the switch.
7. Open menus, complete and exit puzzles, use each hint source, finish and exit
   a Time Trial, finish or exit the tutorial, simulate a hint purchase, and
   test a rewarded hint ad.
8. Check Event Manager's valid and invalid event counts in `development`.
9. Test Delete Analytics Data and confirm the toggle turns off without causing
   the Welcome screen to reappear.
10. Verify Privacy Policy and Terms of Service links from both the Welcome and
    Settings screens, including light and dark appearance.

## 4. Store and privacy disclosures

- Update the privacy policy to describe Unity Analytics, the categories of
  usage/device data collected, why it is collected, the consent toggle, and
  the deletion action.
- Publish the Terms of Service at the URL configured in `LegalLinks.cs`.
- Complete Google Play Data Safety and Apple's App Privacy answers based on
  the final SDK list and event parameters.
- Do not describe this data as anonymous. The Welcome screen accurately says
  that the data is optional; identifier and device details belong in the
  Privacy Policy rather than the Welcome screen.

The game never sends a player's name, email, free-form text, transaction ID,
or puzzle file path through these custom events.
