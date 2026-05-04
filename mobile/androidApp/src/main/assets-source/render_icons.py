"""Render Gold-Bank.svg to PNGs at the densities Android needs (using resvg)."""
from pathlib import Path
import resvg_py

ROOT = Path(__file__).resolve().parents[1]  # main/
RES = ROOT / "res"
SVG = Path(__file__).with_name("Gold-Bank.svg")

LAUNCHER_DENSITIES = [
    ("mipmap-mdpi", 48),
    ("mipmap-hdpi", 72),
    ("mipmap-xhdpi", 96),
    ("mipmap-xxhdpi", 144),
    ("mipmap-xxxhdpi", 192),
]

SPLASH_SIZE = 432


def render(out_path: Path, size: int) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    png_bytes = resvg_py.svg_to_bytes(svg_path=str(SVG), width=size, height=size)
    out_path.write_bytes(bytes(png_bytes))
    print(f"  wrote {out_path.relative_to(ROOT)} ({size}x{size})")


def main():
    print("Launcher icons:")
    for folder, size in LAUNCHER_DENSITIES:
        for name in ("ic_launcher.png", "ic_launcher_round.png"):
            render(RES / folder / name, size)
    print("Splash icon:")
    render(RES / "drawable" / "splash_logo.png", SPLASH_SIZE)
    print("Done.")


if __name__ == "__main__":
    main()
