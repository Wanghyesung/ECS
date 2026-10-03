# Table layout image guide

This version uses basic HTML tables, with two cards per row so the description fits beside the screenshot sidebar. No custom CSS permission is required. Actual table styling is controlled by itch.io.

## Paste and add pictures

1. Open `description-en-copy.txt` and copy everything.
2. In itch.io, open **Edit game > Description > <>** and replace the old description with the HTML.
3. Toggle **<>** back to visual mode.
4. Select one complete `[PHOTO NN: ...]` marker, delete its text, and keep the cursor in that same paragraph/table cell.
5. Click the picture icon and upload the corresponding file below. Set the image to fit its cell if the editor exposes a width control.
6. Repeat for all 14 markers. Do not delete the table itself.
7. Save, view the actual page, and check the mobile layout. Every PHOTO marker should be replaced or removed before publishing.

The game trailer already appears in the sidebar. Keep **Screenshots: Auto** or **Sidebar**. No additional video placeholder is required in the description.

## Image order

| Marker | Image | Local file |
| --- | --- | --- |
| 01 | Basic fire | Choose a screenshot showing basic fire. The previously referenced weapon-basic.jpg is no longer present. |
| 02 | Shotgun | [media/shotgun.png](media/shotgun.png) |
| 03 | Charge shot | [../Screenshots/weapon-charge.jpg](../Screenshots/weapon-charge.jpg) |
| 04 | Laser | [../Screenshots/weapon-beam.jpg](../Screenshots/weapon-beam.jpg) |
| 05 | Missiles | [media/missiles.png](media/missiles.png) |
| 06 | Homing shots | [../Screenshots/weapon-homing.png](../Screenshots/weapon-homing.png) |
| 07 | Support drone | [media/drone.jpg](media/drone.jpg) |
| 08 | Nuclear launch | [media/nuclear-launch.png](media/nuclear-launch.png) |
| 09 | Nuclear blast | [media/nuclear-blast.png](media/nuclear-blast.png) |
| 10 | Card rewards | [../Screenshots/Reward.png](../Screenshots/Reward.png) |
| 11 | Joker success | [../Screenshots/joker-success.png](../Screenshots/joker-success.png) |
| 12 | Joker failure | [../Screenshots/joker-fail.png](../Screenshots/joker-fail.png) |
| 13 | Boss barrage | [media/boss-barrage.png](media/boss-barrage.png) |
| 14 | Boss arrival | [media/boss-warning.png](media/boss-warning.png) |

## Preview

Open `description-table-preview.html` to see the intended image arrangement using your actual local pictures. Slot 01 remains a marker because the previous basic-fire image is missing. This is a visual reference, not a file to paste into itch.io. It uses local CSS only to approximate your dark theme; that CSS is not needed by the upload template. The original `preview.html` is preserved.
