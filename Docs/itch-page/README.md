# ORBITAL — English page kit

Open `preview.html` to view the English game page. It contains six weapon entries, drone support, the nuclear strike, Joker cards, boss combat, equipment, controls, and the 84-second gameplay showcase.

## Files

| File | Purpose |
| --- | --- |
| `preview.html` | Responsive English page with a playable video and chapter buttons |
| `serve.py` | Local preview server with HTTP byte ranges for video seeking |
| `description-en.md` | English game description for reuse in the store editor |
| `description-en.html` | The same description as a simple HTML fragment |
| `media/orbital-showcase.mp4` | Web-sized 720p copy of the v9 showcase, including the corrected drone audio |
| `media/drone.jpg` | Actual drone gameplay frame from the showcase |
| `background.jpg` | Existing background used by the preview |
| `../Screenshots/` | Actual gameplay images used by the weapon cards and gallery |

The page title is rendered as English HTML text over an actual gameplay screenshot. The earlier `header.png`, `cover.png`, `generate_assets.py`, and `description-ko.md` are legacy Korean assets; the English preview does not use them. Do not upload those older title graphics as English artwork.

## Preview

Double-click `preview.html`, or run `python Docs/itch-page/serve.py` from the repository root and open `http://127.0.0.1:8768/itch-page/preview.html`. The included server supports byte ranges so video chapter buttons can seek correctly. Keep `itch-page` and `Screenshots` beside one another when moving the page. The video and all images are local files; no external fonts or scripts are required.

The video has native playback, volume, seeking, and fullscreen controls. Chapter buttons jump to cards/homing, shotgun, drone, charge shot, laser, missiles/boss, and the nuclear finale. Playback starts only after an explicit user action.

## Content included

1. Basic fire
2. Shotgun
3. Charge shot (blue)
4. Laser (yellow)
5. Missiles (red)
6. Homing shots
7. Autonomous support drone (blue projectiles)
8. Legendary nuclear strike
9. Level-up card choices and Joker risks/rewards
10. Boss encounters, lobby shop, inventory, and equipment
11. Keyboard/mouse controls and Windows extraction instructions

## Reusing the description

Use `description-en.md` for the text, or `description-en.html` for an HTML-capable editor. Add the gameplay images using the destination editor's media controls. The complete `preview.html` is a local layout preview, not an itch.io theme upload.

The smaller page video is derived from `C:/Users/왕혜성/Videos/Orbital_Gameplay_Demo_v9_1080p.mp4`. Use that 1080p master for publishing the full-quality trailer. Its drone effects are 13.5 dB louder than v8; the music, picture, and 84-second duration are unchanged.

## Before publishing

Supply the actual Windows download, release/version information, price, contact details, and a public trailer URL if needed. These have not been invented or replaced with nonfunctional download buttons. Confirm controls against the distributed build. Minimum requirements and other platforms are omitted until verified.

## Content checks

The weapon and ability descriptions are based on the supplied gameplay, `SOFeatureNuke`, `NukeStrike`, `Drone`, and the weapon implementation. Controls are based on `PlayerAction.inputactions`, `PlayerMovement`, `Player`, and `PlayerChargeController` as of 2026-10-02. The page describes implemented behavior without promising fixed damage values, guaranteed wins, or untested platform support.

## Selected screenshots (2026-10-02)

The six user-supplied images are copied unchanged into `media/`:

| Attachment | File | Placement |
| --- | --- | --- |
| 1 | `nuclear-launch.png` | Nuclear missile descent |
| 2 | `nuclear-blast.png` | Nuclear detonation |
| 3 | `boss-barrage.png` | Boss gallery and hero backdrop |
| 4 | `boss-warning.png` | Boss gallery |
| 5 | `shotgun.png` | Shotgun weapon card |
| 6 | `missiles.png` | Missiles weapon card |

Gallery images preserve the complete frame and link to the original-size copies.
