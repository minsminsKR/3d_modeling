"""Rasterize the original storefront lettering with the bundled OFL font."""
from pathlib import Path
import random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

project = Path(__file__).resolve().parents[2]
font_path = project / 'Assets/Resources/Fonts/PauseMyeongjo.ttf'
output = project / 'Assets/Resources/TempleApproach/alley-store-sign.png'
rng = random.Random(12091)
image = Image.new('RGB', (512, 512), (219, 198, 157))
draw = ImageDraw.Draw(image)
draw.rectangle((13, 13, 498, 498), outline=(80, 51, 38), width=10)
draw.rectangle((31, 31, 480, 480), outline=(151, 108, 76), width=3)
font = ImageFont.truetype(str(font_path), 156)
for text, y in [('우암', 57), ('상회', 251)]:
    bounds = draw.textbbox((0, 0), text, font=font)
    x = (512 - (bounds[2] - bounds[0])) / 2 - bounds[0]
    draw.text((x, y - bounds[1]), text, font=font, fill=(54, 30, 25))
overlay = Image.new('RGBA', image.size)
wear = ImageDraw.Draw(overlay)
for _ in range(1500):
    x, y = rng.randrange(512), rng.randrange(512)
    radius = rng.choice((1, 1, 1, 2, 3))
    wear.ellipse((x, y, x + radius, y + radius), fill=(65, 43, 28, rng.randrange(12, 75)))
image = Image.alpha_composite(image.convert('RGBA'), overlay).convert('RGB')
output.parent.mkdir(parents=True, exist_ok=True)
image.save(output)
print(output)
