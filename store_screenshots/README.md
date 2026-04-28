# Store Screenshots

Mockup screenshots for the Microsoft Store listing.

## Contents (40 images)

Each layout is shown in all 8 color themes (Blue, Brown, Orange, Purple, Green, Yellow, Red, Gray) with a different handwriting font per mockup so all 7 fonts (Caveat, Comic Relief, Delius, Loved By The King, Open Sans, Patrick Hand, Roboto) are represented across each layout type.

| Layout | Size | Files |
|---|---|---|
| Landscape — two-page spread | 1366 × 768 | `landscape_{color}_{font}.png` |
| Portrait — Tasks | 390 × 844 | `portrait_tasks_{color}_{font}.png` |
| Portrait — Notes | 390 × 844 | `portrait_notes_{color}_{font}.png` |
| Settings page | 390 × 844 | `settings_{color}_{font}.png` |
| Help page | 390 × 844 | `help_{color}_{font}.png` |

## Regenerating

```
pip install playwright
python -m playwright install chromium
python3 /tmp/gen_screenshots.py   # script lives in repo root if needed
```
