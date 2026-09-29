"""Create itch.io artwork from the project's real gameplay screenshots."""

from pathlib import Path
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont
import random

ROOT = Path(__file__).resolve().parents[1]
OUT = Path(__file__).resolve().parent
SHOTS = ROOT / "Screenshots"
FONT_BOLD = Path(r"C:\Windows\Fonts\bahnschrift.ttf")
FONT_KO = Path(r"C:\Windows\Fonts\malgunbd.ttf")
CYAN = (99, 236, 255)
WHITE = (239, 250, 255)
GOLD = (255, 201, 90)


def font(size, korean=False):
    return ImageFont.truetype(str(FONT_KO if korean else FONT_BOLD), size)


def fit_crop(image, size, focus=(0.5, 0.5)):
    image = image.convert("RGB")
    ratio = max(size[0] / image.width, size[1] / image.height)
    scaled = image.resize((round(image.width * ratio), round(image.height * ratio)), Image.Resampling.LANCZOS)
    left = round((scaled.width - size[0]) * focus[0])
    top = round((scaled.height - size[1]) * focus[1])
    return scaled.crop((left, top, left + size[0], top + size[1]))


def tint(image, color=(3, 12, 29), strength=0.48):
    return Image.blend(image, Image.new("RGB", image.size, color), strength)


def title(draw, xy, text, size, fill=WHITE, spacing=3):
    x, y = xy
    face = font(size)
    for char in text:
        draw.text((x, y), char, font=face, fill=fill, stroke_width=1, stroke_fill=(7, 26, 41))
        x += draw.textlength(char, font=face) + spacing
    return x


def linework(draw, w, h):
    draw.line([(0, 0), (w, 0)], fill=(35, 213, 241), width=7)
    draw.line([(0, h - 6), (w, h - 6)], fill=(19, 112, 143), width=5)
    draw.line([(w - 92, 0), (w - 20, 70)], fill=CYAN, width=3)
    draw.line([(20, h - 47), (67, h - 7)], fill=CYAN, width=3)


def make_cover():
    w, h = 630, 500
    shot = fit_crop(Image.open(SHOTS / "boss.png"), (w, h), (0.66, 0.47))
    base = tint(shot, strength=0.34).convert("RGBA")
    wash = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    wd = ImageDraw.Draw(wash)
    for x in range(w):
        alpha = int(227 * (1 - x / w) ** 1.45)
        wd.line([(x, 0), (x, h)], fill=(3, 11, 28, alpha))
    base = Image.alpha_composite(base, wash).convert("RGB")
    d = ImageDraw.Draw(base)
    linework(d, w, h)
    d.text((37, 42), "오비탈", font=font(22, True), fill=CYAN)
    title(d, (35, 135), "ORBITAL", 76, spacing=0)
    d.rounded_rectangle((37, 246, 386, 288), radius=5, fill=(7, 27, 47), outline=(55, 203, 230), width=2)
    d.text((52, 252), "SHOOT  /  CHOOSE  /  RISK", font=font(20), fill=WHITE)
    d.text((37, 420), "3D ROGUELITE SHOOTER", font=font(17), fill=WHITE)
    d.text((37, 447), "WINDOWS  |  IN DEVELOPMENT", font=font(14), fill=CYAN)
    base.save(OUT / "cover.png", optimize=True)


def make_banner():
    w, h = 960, 300
    shot = fit_crop(Image.open(SHOTS / "boss.png"), (w, h), (0.53, 0.33))
    base = tint(shot, strength=0.50).convert("RGBA")
    wash = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    wd = ImageDraw.Draw(wash)
    for x in range(w):
        wd.line([(x, 0), (x, h)], fill=(2, 8, 24, round(226 * (1 - x / w) ** 1.3)))
    base = Image.alpha_composite(base, wash).convert("RGB")
    d = ImageDraw.Draw(base)
    linework(d, w, h)
    d.text((55, 35), "오비탈  /  ORBITAL", font=font(20, True), fill=CYAN)
    title(d, (48, 77), "ORBITAL", 117, spacing=0)
    d.line([(55, 220), (540, 220)], fill=CYAN, width=2)
    d.text((55, 235), "한 번 더 걸 것인가, 지금 살아남을 것인가", font=font(19, True), fill=WHITE)
    base.save(OUT / "header.png", optimize=True)


def make_background():
    w, h = 1920, 1080
    shot = fit_crop(Image.open(SHOTS / "lobby.png"), (w, h), (0.5, 0.5))
    shot = ImageEnhance.Color(shot).enhance(0.65).filter(ImageFilter.GaussianBlur(18))
    base = tint(shot, strength=0.78)
    overlay = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(overlay)
    rng = random.Random(1107)
    for _ in range(165):
        x, y = rng.randrange(w), rng.randrange(h)
        r = rng.choice((1, 1, 1, 2))
        d.ellipse((x-r, y-r, x+r, y+r), fill=(126, 226, 255, rng.randrange(45, 130)))
    d.ellipse((-350, 280, 390, 1020), outline=(38, 180, 208, 42), width=3)
    d.ellipse((1440, -230, 2170, 500), outline=(196, 73, 198, 55), width=4)
    base = Image.alpha_composite(base.convert("RGBA"), overlay).convert("RGB")
    base.save(OUT / "background.jpg", quality=86, optimize=True)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    make_cover()
    make_banner()
    make_background()
