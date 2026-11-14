"""生成 yaw / pitch 教学示意图（不进入 Unity，只画到 Docs 目录）。"""
import math
import os

from PIL import Image, ImageDraw, ImageFont

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(OUT_DIR, "yaw-pitch.png")

FONT = r"C:\Windows\Fonts\msyh.ttc"
FONT_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"

W, H = 1240, 830
BG = (250, 250, 252)
INK = (33, 37, 41)
MUTED = (108, 117, 125)
BLUE = (42, 111, 176)
ORANGE = (203, 108, 32)
GRAY = (158, 164, 170)
SOFT = (228, 231, 235)
GROUND = (238, 232, 222)

f_title = ImageFont.truetype(FONT_BOLD, 33)
f_sub = ImageFont.truetype(FONT_BOLD, 21)
f_lbl = ImageFont.truetype(FONT, 18)
f_small = ImageFont.truetype(FONT, 15)
f_tiny = ImageFont.truetype(FONT, 14)

img = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(img)


def text(x, y, s, f, fill=INK, anchor="lm"):
    d.text((x, y), s, font=f, fill=fill, anchor=anchor)


def dashed(p0, p1, fill=GRAY, dash=9, gap=7, width=2):
    x0, y0 = p0
    x1, y1 = p1
    dist = math.hypot(x1 - x0, y1 - y0)
    if dist == 0:
        return
    ux, uy = (x1 - x0) / dist, (y1 - y0) / dist
    t = 0.0
    while t < dist:
        e = min(t + dash, dist)
        d.line([(x0 + ux * t, y0 + uy * t), (x0 + ux * e, y0 + uy * e)],
               fill=fill, width=width)
        t = e + gap


def arrow(p0, p1, fill, width=2, head=10):
    d.line([p0, p1], fill=fill, width=width)
    ang = math.atan2(p1[1] - p0[1], p1[0] - p0[0])
    for sgn in (0.42, -0.42):
        d.line([p1, (p1[0] - head * math.cos(ang + sgn),
                     p1[1] - head * math.sin(ang + sgn))], fill=fill, width=width)


def camera_icon(cx, cy, angle_deg, scale=1.0):
    """画一个简易相机（机身 + 镜头），镜头朝向 angle_deg（屏幕坐标系，0=向右）。"""
    bw, bh = 24 * scale, 17 * scale
    d.rounded_rectangle([cx - bw / 2, cy - bh / 2, cx + bw / 2, cy + bh / 2],
                        radius=4 * scale, fill=(70, 76, 84), outline=(40, 44, 50), width=1)
    lx = cx + math.cos(math.radians(angle_deg)) * bw * 0.62
    ly = cy + math.sin(math.radians(angle_deg)) * bh * 0.62
    d.ellipse([lx - 5 * scale, ly - 5 * scale, lx + 5 * scale, ly + 5 * scale],
              fill=(120, 190, 240), outline=(40, 44, 50), width=1)


def panel(box, title):
    d.rounded_rectangle(box, radius=14, fill=(255, 255, 255), outline=SOFT, width=2)
    cx = (box[0] + box[2]) / 2
    text(cx, box[1] + 30, title, f_sub, INK, anchor="mm")


text(W / 2, 38, "yaw 和 pitch 到底是什么", f_title, INK, anchor="mm")
text(W / 2, 74, "把相机想象成“长在角色肩上的脖子”", f_small, MUTED, anchor="mm")

# ---------------------------------------------------------------- 面板 A：yaw
A = (30, 100, 600, 800)
panel(A, "① yaw = 绕着角色转圈（水平，360°）")

cx, cy, R = 315, 465, 150
# 轨道
d.ellipse([cx - R, cy - R, cx + R, cy + R], outline=SOFT, width=2)
# 世界轴参考
arrow((cx, cy + R + 62), (cx, cy + R + 22), (200, 205, 210), 2, 8)
text(cx + 8, cy + R + 72, "玩家正面 +Z", f_tiny, MUTED, anchor="lm")
arrow((cx + R + 18, cy + R + 60), (cx + R + 58, cy + R + 60), (200, 205, 210), 2, 8)
text(cx + R + 64, cy + R + 60, "+X", f_tiny, MUTED, anchor="lm")

# 玩家
d.ellipse([cx - 15, cy - 15, cx + 15, cy + 15], fill=BLUE)
arrow((cx, cy - 4), (cx, cy - 52), BLUE, 3, 11)
text(cx + 24, cy - 30, "Player", f_lbl, BLUE, anchor="lm")

# 四个 yaw 位置（用实测偏移：yaw0=(0,0,-5) yaw90=(-5,0,0) yaw180=(0,0,5) yaw270=(5,0,0)）
marks = [
    ((cx + 0, cy + R), "yaw = 0°", "相机在背后", "mm", (cx, cy + R + 34)),
    ((cx - R, cy), "yaw = 90°", "相机在左侧", "rm", (cx - R - 22, cy)),
    ((cx + 0, cy - R), "yaw = 180°", "相机跑到正前方", "mm", (cx, cy - R - 30)),
    ((cx + R, cy), "yaw = 270°", "相机在右侧", "lm", (cx + R + 22, cy)),
]

for (mx, my), name, desc, anchor, lpos in marks:
    # 视线（相机 → 角色）
    dashed((mx, my), (cx, cy), GRAY, 7, 6, 2)
    vx, vy = cx - mx, cy - my
    n = math.hypot(vx, vy)
    tipx, tipy = cx - vx / n * 18, cy - vy / n * 18
    arrow((mx + vx / n * 16, my + vy / n * 16), (tipx, tipy), (120, 126, 134), 2, 8)
    camera_icon(mx, my, math.degrees(math.atan2(vy, vx)))
    text(lpos[0], lpos[1] - 9 if anchor == "mm" else lpos[1] - 10, name, f_lbl, INK, anchor=anchor)
    text(lpos[0], lpos[1] + 11 if anchor == "mm" else lpos[1] + 11, desc, f_tiny, MUTED, anchor=anchor)

text(cx, 726, "鼠标左右移动 → 改 yaw → 相机绕着角色转一圈", f_small, INK, anchor="mm")
text(cx, 752, "yaw 没有上下限，可以无限累加（转 720° 也没问题）", f_tiny, MUTED, anchor="mm")

# ---------------------------------------------------------------- 面板 B：pitch
B = (640, 100, 1210, 800)
panel(B, "② pitch = 把相机抬高或放低（垂直，有限位）")

ox, oy, S = 1010, 600, 40  # oy = 地面，S = 像素/米
# 地面
d.rectangle([B[0] + 12, oy, B[2] - 12, B[3] - 12], fill=GROUND)
d.line([B[0] + 12, oy, B[2] - 12, oy], fill=(196, 186, 168), width=2)
text(B[0] + 20, oy + 14, "地面", f_tiny, (150, 140, 122), anchor="lm")

piv_x, piv_y = ox, oy - int(1.5 * S)

# 玩家（胶囊，2m 高，面向 +Z 即屏幕右）
d.rounded_rectangle([ox - 20, oy - 80, ox + 20, oy], radius=20, fill=BLUE)
d.ellipse([ox - 11, oy - 96, ox + 11, oy - 74], fill=BLUE)
arrow((ox + 22, oy - 40), (ox + 62, oy - 40), BLUE, 3, 10)
text(ox + 70, oy - 40, "正面 +Z", f_tiny, BLUE, anchor="lm")

# 枢轴
d.ellipse([piv_x - 6, piv_y - 6, piv_x + 6, piv_y + 6], fill=ORANGE)
text(piv_x + 32, piv_y - 44, "枢轴 pivot", f_tiny, ORANGE, anchor="lm")

# 三个 pitch 位置（实测：-35 => y=-2.87,z=-4.10；0 => y=0,z=-5；70 => y=4.70,z=-1.71）
pitch_pts = [
    (-35, -4.10, -2.87, "pitch = -35°", "相机在下方，仰视", "lm", (30, -8)),
    (0, -5.00, 0.00, "pitch = 0°", "平视", "rm", (-26, -6)),
    (70, -1.71, 4.70, "pitch = 70°", "相机在上方，俯视", "mm", (-10, -40)),
]

for deg, z, y, name, desc, anchor, (dx, dy) in pitch_pts:
    px = ox + int(z * S)
    py = oy - int(y * S)
    dashed((px, py), (piv_x, piv_y), GRAY, 7, 6, 2)
    vx, vy = piv_x - px, piv_y - py
    n = math.hypot(vx, vy)
    arrow((px + vx / n * 16, py + vy / n * 16),
          (piv_x - vx / n * 16, piv_y - vy / n * 16), (120, 126, 134), 2, 8)
    camera_icon(px, py, math.degrees(math.atan2(vy, vx)))
    text(px + dx, py + dy, name, f_lbl, INK, anchor=anchor)
    text(px + dx, py + dy + 22, desc, f_tiny, MUTED, anchor=anchor)

# 限位提示
text((B[0] + B[2]) / 2, 748, "pitch 有上下限：本工程设为 -35° ~ 70°", f_small, INK, anchor="mm")
text((B[0] + B[2]) / 2, 774,
     "限位就是一次 Mathf.Clamp —— 永远翻不过头顶、也钻不到地下",
     f_tiny, MUTED, anchor="mm")

img.save(OUT)
print("saved:", OUT, img.size)
