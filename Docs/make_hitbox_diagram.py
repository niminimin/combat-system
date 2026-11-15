"""画一张"为什么要放大判定盒"的示意图。"""
import os

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "hitbox-why-larger.png")
FONT = r"C:\Windows\Fonts\msyh.ttc"
FONT_B = r"C:\Windows\Fonts\msyhbd.ttc"

W, H = 1200, 790
BG = (250, 250, 252)
INK = (33, 37, 41)
MUTED = (110, 117, 125)
BLUE = (42, 111, 176)
ORANGE = (226, 122, 42)
RED = (198, 58, 52)
GREEN = (32, 148, 88)

f_title = ImageFont.truetype(FONT_B, 30)
f_sub = ImageFont.truetype(FONT_B, 20)
f_lbl = ImageFont.truetype(FONT, 19)
f_small = ImageFont.truetype(FONT, 16)

img = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(img)


def text(x, y, s, f, fill=INK, anchor="lm"):
    d.text((x, y), s, font=f, fill=fill, anchor=anchor)


def panel(box, title):
    d.rounded_rectangle(box, radius=14, fill=(255, 255, 255), outline=(228, 231, 235), width=2)
    text(box[0] + 26, box[1] + 34, title, f_sub, INK, anchor="lm")


def player(cx, cy):
    d.ellipse([cx - 32, cy - 32, cx + 32, cy + 32], fill=BLUE)
    text(cx, cy + 52, "玩家", f_small, BLUE, anchor="mm")


def blade(x0, y0, x1):
    d.line([(x0, y0), (x1, y0)], fill=(196, 201, 206), width=10)
    d.line([(x0 + 18, y0 - 18), (x0 + 18, y0 + 18)], fill=(120, 126, 132), width=7)


def enemy(cx, cy, r=40):
    d.rounded_rectangle([cx - r, cy - r * 2.2, cx + r, cy + r * 2.2], radius=r, fill=(206, 138, 128))
    text(cx, cy + r * 2.2 + 20, "敌人", f_small, (150, 90, 85), anchor="mm")


text(W / 2, 42, "为什么要把剑的判定盒放大", f_title, INK, anchor="mm")
text(W / 2, 80, "判定不是「剑撞上去」，而是每帧用一个盒子去问引擎：谁在里面", f_small, MUTED, anchor="mm")

BLADE_X0, BLADE_X1 = 150, 640
ENEMY_X = 700

# ---------------- 面板 ① ----------------
A = (30, 105, 1170, 415)
panel(A, "① 盒子刚好和刀身一样大")
cy = 250
player(112, cy)
blade(BLADE_X0, cy, BLADE_X1)
d.rectangle([BLADE_X0, cy - 16, BLADE_X1, cy + 16], outline=ORANGE, width=3)
text(BLADE_X1, cy - 34, "判定盒", f_small, ORANGE, anchor="rm")

enemy(ENEMY_X, cy)

# 差距标注
d.line([(BLADE_X1 + 6, cy + 66), (ENEMY_X - 40, cy + 66)], fill=RED, width=3)
text((BLADE_X1 + ENEMY_X - 40) / 2, cy + 84, "差几厘米", f_small, RED, anchor="mm")
text(1150, 388, "刀尖够不到 → 判定失败", f_lbl, RED, anchor="rm")

# ---------------- 面板 ② ----------------
B = (30, 440, 1170, 750)
panel(B, "② 判定盒放大 20%")
cy2 = 580
player(112, cy2)
blade(BLADE_X0, cy2, BLADE_X1)
EXT = 30
d.rectangle([BLADE_X0 - EXT, cy2 - 19, BLADE_X1 + EXT, cy2 + 19], outline=ORANGE, width=3)
text(BLADE_X1 - 10, cy2 - 66, "两端各多出约 7cm", f_small, ORANGE, anchor="rm")

enemy(ENEMY_X, cy2)
d.rectangle([ENEMY_X - 44, cy2 - 92, ENEMY_X + 44, cy2 + 92], outline=GREEN, width=3)
text(1150, 724, "多出的部分盖住了差距 → 判定成功", f_lbl, GREEN, anchor="rm")

text(W / 2, 772, "放大不是「打空气也算命中」，而是补上「看起来碰到了、几何上差几厘米」的误差",
     f_small, MUTED, anchor="mm")

img.save(OUT)
print("saved", OUT, img.size)
