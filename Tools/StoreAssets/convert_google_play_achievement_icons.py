"""Create Google Play Games achievement icons from the Apple source art.

The converter keeps the source files untouched, removes the solid cream matte,
resizes with premultiplied-alpha Lanczos filtering, and validates Google's
512x512 PNG and sub-1 MiB import constraints.
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "AppStore" / "Apple" / "GameCenter" / "Achievements"
OUTPUT_DIR = ROOT / "AppStore" / "GooglePlay" / "Achievements"

SOURCE_NAMES = (
    "adventure-begins.png",
    "against-the-clock.png",
    "century-club.PNG",
    "clockwork.png",
    "shikaku-master.png",
    "free-thinker.png",
    "pack-it-up.png",
    "perfect-week.png",
    "seasoned-explorer.png",
    "triple-threat.png",
)

# Flat colors used by the achievement artwork. These let us reconstruct clean
# antialiased edges after removing the original cream canvas.
FOREGROUND_PALETTE = np.asarray(
    [
        (39, 49, 59),
        (38, 48, 58),
        (216, 94, 94),
        (98, 166, 111),
        (77, 139, 206),
        (78, 139, 206),
        (230, 184, 77),
        (255, 253, 248),
        (255, 254, 248),
    ],
    dtype=np.float32,
)


def remove_cream_matte(image: Image.Image) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.uint8).copy()
    rgb = rgba[..., :3].astype(np.float32)

    # The dominant source color is the canvas matte. Sampling all four corners
    # handles the one source whose red channel differs by one value.
    corners = np.asarray(
        [rgb[0, 0], rgb[0, -1], rgb[-1, 0], rgb[-1, -1]],
        dtype=np.float32,
    )
    background = np.median(corners, axis=0)

    best_error = np.full(rgb.shape[:2], np.inf, dtype=np.float32)
    best_t = np.ones(rgb.shape[:2], dtype=np.float32)
    best_color = np.zeros_like(rgb)

    delta = rgb - background
    for foreground in FOREGROUND_PALETTE:
        direction = foreground - background
        denominator = float(np.dot(direction, direction))
        if denominator == 0:
            continue

        t = np.clip(np.sum(delta * direction, axis=2) / denominator, 0.0, 1.0)
        reconstructed = background + t[..., None] * direction
        error = np.linalg.norm(rgb - reconstructed, axis=2)
        better = error < best_error
        best_error[better] = error[better]
        best_t[better] = t[better]
        best_color[better] = foreground

    distance_from_background = np.linalg.norm(delta, axis=2)

    # The cream canvas contains subtle paper speckles. Flood only the pixels
    # connected to the outside edge, so enclosed light artwork (white numbers
    # and the clock face) is preserved even when close to the matte color.
    background_candidate = distance_from_background <= 80.0
    flood_source = Image.fromarray(
        np.where(background_candidate, 255, 0).astype(np.uint8), "L"
    )
    ImageDraw.floodfill(flood_source, (0, 0), 128, thresh=0)
    matte = np.asarray(flood_source, dtype=np.uint8) == 128

    # Pixels just inside the remaining artwork boundary that lie on a clean
    # matte-to-palette blend retain proportional alpha and are decontaminated
    # back to their intended foreground color.
    antialias = (~matte) & (best_error <= 3.0) & (best_t < 0.98)

    new_alpha = np.full(rgb.shape[:2], 255.0, dtype=np.float32)
    new_alpha[matte] = 0.0
    new_alpha[antialias] = np.clip(best_t[antialias] * 255.0, 0.0, 255.0)

    rgb[matte] = 0.0
    rgb[antialias] = best_color[antialias]

    existing_alpha = rgba[..., 3].astype(np.float32) / 255.0
    new_alpha *= existing_alpha

    result = np.empty_like(rgba)
    result[..., :3] = np.clip(np.rint(rgb), 0, 255).astype(np.uint8)
    result[..., 3] = np.clip(np.rint(new_alpha), 0, 255).astype(np.uint8)
    return Image.fromarray(result, "RGBA")


def complete_and_inset_shikaku_master(image: Image.Image) -> Image.Image:
    """Close the source's full-bleed tile and inset it for the toast circle."""
    reconstructed = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(reconstructed)
    draw.rounded_rectangle(
        (201, 445, 822, 970), radius=65, fill=(39, 49, 59, 255)
    )
    draw.rounded_rectangle(
        (222, 466, 801, 949), radius=45, fill=(216, 94, 94, 255)
    )

    source_top = image.copy()
    source_alpha = np.asarray(source_top.getchannel("A"), dtype=np.uint8).copy()
    source_alpha[900:, :] = 0
    source_top.putalpha(Image.fromarray(source_alpha, "L"))
    reconstructed.alpha_composite(source_top)

    inset = resize_premultiplied(reconstructed, (942, 942))
    canvas = Image.new("RGBA", image.size, (0, 0, 0, 0))
    canvas.alpha_composite(inset, (41, 41))
    return canvas


def resize_premultiplied(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    rgba = np.asarray(image.convert("RGBA"), dtype=np.float32)
    alpha = rgba[..., 3:4] / 255.0
    premultiplied = rgba[..., :3] * alpha

    resized_channels: list[np.ndarray] = []
    for channel in range(3):
        channel_image = Image.fromarray(
            np.clip(np.rint(premultiplied[..., channel]), 0, 255).astype(np.uint8),
            "L",
        )
        resized_channels.append(
            np.asarray(
                channel_image.resize(size, Image.Resampling.LANCZOS),
                dtype=np.float32,
            )
        )

    alpha_image = Image.fromarray(
        np.clip(np.rint(rgba[..., 3]), 0, 255).astype(np.uint8),
        "L",
    )
    resized_alpha = np.asarray(
        alpha_image.resize(size, Image.Resampling.LANCZOS),
        dtype=np.float32,
    )

    output = np.zeros((size[1], size[0], 4), dtype=np.uint8)
    output[..., 3] = np.clip(np.rint(resized_alpha), 0, 255).astype(np.uint8)

    nonzero = resized_alpha > 0
    for channel, resized in enumerate(resized_channels):
        unpremultiplied = np.zeros_like(resized)
        unpremultiplied[nonzero] = (
            resized[nonzero] * 255.0 / resized_alpha[nonzero]
        )
        output[..., channel] = np.clip(
            np.rint(unpremultiplied), 0, 255
        ).astype(np.uint8)

    return Image.fromarray(output, "RGBA")


def circle_crop_loss(image: Image.Image) -> int:
    alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
    height, width = alpha.shape
    yy, xx = np.ogrid[:height, :width]
    center_x = (width - 1) / 2.0
    center_y = (height - 1) / 2.0
    radius = min(width, height) / 2.0
    outside = (xx - center_x) ** 2 + (yy - center_y) ** 2 > radius**2
    return int(np.count_nonzero((alpha > 16) & outside))


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    for source_name in SOURCE_NAMES:
        source = SOURCE_DIR / source_name
        destination = OUTPUT_DIR / f"{source.stem.lower()}.png"

        with Image.open(source) as original:
            transparent = remove_cream_matte(original)
            if source.stem.lower() == "shikaku-master":
                transparent = complete_and_inset_shikaku_master(transparent)
            converted = resize_premultiplied(transparent, (512, 512))
            converted.save(destination, "PNG", optimize=True)

        with Image.open(destination) as check:
            if check.size != (512, 512):
                raise RuntimeError(f"Incorrect dimensions: {destination}")
            if check.mode != "RGBA":
                raise RuntimeError(f"Missing alpha channel: {destination}")
            if destination.stat().st_size >= 1_048_576:
                raise RuntimeError(f"File exceeds 1 MiB: {destination}")

            alpha_min, alpha_max = check.getchannel("A").getextrema()
            clipped_pixels = circle_crop_loss(check)
            print(
                f"{destination.name}: 512x512 RGBA, "
                f"{destination.stat().st_size:,} bytes, "
                f"alpha={alpha_min}-{alpha_max}, "
                f"circle-mask pixels clipped={clipped_pixels}"
            )


if __name__ == "__main__":
    main()
