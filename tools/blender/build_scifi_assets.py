"""
Renders the pilot set of Blender sci-fi assets for Hexen Sharp's "Rendered art" option.

Everything is modelled in code from primitives, rendered with Cycles at 4x size, then shrunk to the game's
64x64 with a hard alpha edge, a slightly reduced colour depth and (for sprites) the same dark outline the
procedural art uses, so the renders sit comfortably in a 320x200 frame.

Run from the repository root (Blender 4.x, no GUI needed):

    blender -b --factory-startup -noaudio -P tools/blender/build_scifi_assets.py

Output goes to assets/scifi/{sprites,monsters,textures}/*.png, which the game embeds. Pass a name filter
after "--" to rebuild only some assets, e.g. `... -P tools/blender/build_scifi_assets.py -- jetpack afrit`.
"""

import math
import os
import struct
import sys
import zlib

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "assets", "scifi")
TMP = os.path.join(OUT, ".render_tmp.png")
SIZE = 64          # final texture size, as in the game
SCALE = 4          # render at 4x and shrink
SAMPLES = 96
DARK = (14, 12, 18)  # outline colour, matches Art.Dark

FILTER = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


# ---------------------------------------------------------------- scene helpers

_mats = {}


def reset(transparent=True):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = SAMPLES
    sc.cycles.use_denoising = False   # Ubuntu's Blender is built without OpenImageDenoise
    sc.cycles.seed = 7
    sc.render.resolution_x = sc.render.resolution_y = SIZE * SCALE
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.image_settings.color_depth = "8"
    sc.view_settings.view_transform = "Standard"   # keep colours punchy, like the game's palette
    sc.view_settings.look = "None"
    world = bpy.data.worlds.new("world")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.7, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.35
    return sc


def mat(rgb, metal=0.0, rough=0.45, emit=None, strength=4.0):
    key = (tuple(rgb), metal, rough, tuple(emit) if emit else None, strength)
    if key in _mats:
        return _mats[key]
    m = bpy.data.materials.new("m%d" % len(_mats))
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*srgb(rgb), 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = (*srgb(emit), 1)
        b.inputs["Emission Strength"].default_value = strength
    _mats[key] = m
    return m


def srgb(rgb):
    """0-255 sRGB colour to Blender's linear floats."""
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(c) for c in rgb)


def add(kind, loc, size=(1, 1, 1), rot=(0, 0, 0), m=None, bevel=0.0, **kw):
    ops = {
        "cube": bpy.ops.mesh.primitive_cube_add,
        "cyl": bpy.ops.mesh.primitive_cylinder_add,
        "sphere": bpy.ops.mesh.primitive_uv_sphere_add,
        "cone": bpy.ops.mesh.primitive_cone_add,
        "torus": bpy.ops.mesh.primitive_torus_add,
    }
    if kind == "cube":
        kw.setdefault("size", 1)
    if kind == "cyl":
        kw.setdefault("vertices", 24)
    if kind == "sphere":
        kw.setdefault("segments", 24)
        kw.setdefault("ring_count", 12)
    ops[kind](location=loc, rotation=[math.radians(a) for a in rot], **kw)
    o = bpy.context.object
    o.scale = size
    if m is not None:
        o.data.materials.append(m)
    if bevel > 0:
        mod = o.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
    bpy.ops.object.shade_smooth() if kind in ("sphere", "cyl", "torus", "cone") else None
    return o


def lights(key=4.0, fill=1.2, key_rot=(50, 15, 35)):
    bpy.ops.object.light_add(type="SUN", location=(0, 0, 5))
    s = bpy.context.object
    s.data.energy = key
    s.rotation_euler = [math.radians(a) for a in key_rot]
    bpy.ops.object.light_add(type="SUN", location=(0, 0, 5))
    f = bpy.context.object
    f.data.energy = fill
    f.rotation_euler = [math.radians(a) for a in (60, -20, -150)]


def meshes():
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def fit_camera(tilt=12.0, fill=0.8, bottom=1.5):
    """Orthographic camera looking at the model from the front (-Y), tilted down by `tilt` degrees, framed so
    the model fills `fill` of the frame and its lowest point sits `bottom` final pixels above the bottom edge."""
    bpy.ops.object.camera_add(location=(0, -20, 0))
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.rotation_euler = (math.radians(90 - tilt), 0, 0)
    bpy.context.scene.camera = cam
    bpy.context.view_layer.update()
    inv = cam.matrix_world.inverted()
    xs, ys = [], []
    for o in meshes():
        dg = o.evaluated_get(bpy.context.evaluated_depsgraph_get())
        for v in dg.data.vertices:
            p = inv @ (o.matrix_world @ v.co)
            xs.append(p.x)
            ys.append(p.y)
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    scale = max(w, h) / fill
    cam.data.ortho_scale = scale
    cx = (max(xs) + min(xs)) / 2
    # frame bottom = -scale/2 in camera space; put the model's bottom `bottom` pixels above it
    cy = min(ys) - bottom * scale / SIZE + scale / 2
    cam.location = cam.location + cam.matrix_world.to_3x3() @ Vector((cx, cy, 0))
    return cam


def render():
    sc = bpy.context.scene
    sc.render.filepath = TMP
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(TMP)
    w, h = img.size
    px = list(img.pixels[:])
    bpy.data.images.remove(img)
    os.remove(TMP)
    # to top-down rows of 0-255 RGBA
    rows = []
    for y in range(h - 1, -1, -1):
        row = []
        for x in range(w):
            i = (y * w + x) * 4
            row.append(tuple(px[i:i + 4]))
        rows.append(row)
    return rows


# ---------------------------------------------------------------- shrinking to game pixels

def shrink(rows, sprite):
    out = []
    for y in range(SIZE):
        line = []
        for x in range(SIZE):
            r = g = b = a = 0.0
            for dy in range(SCALE):
                for dx in range(SCALE):
                    pr, pg, pb, pa = rows[y * SCALE + dy][x * SCALE + dx]
                    if not sprite:
                        pa = 1.0
                    r += pr * pa; g += pg * pa; b += pb * pa; a += pa
            n = SCALE * SCALE
            if sprite and a / n < 0.5:
                line.append((0, 0, 0, 0))
                continue
            # a few levels fewer per channel reads as pixel art rather than a smooth render
            q = lambda c: int(round(min(1.0, c / a) * 31)) * 255 // 31
            line.append((q(r), q(g), q(b), 255))
        out.append(line)
    if sprite:
        edge = [[False] * SIZE for _ in range(SIZE)]
        for y in range(SIZE):
            for x in range(SIZE):
                if out[y][x][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < SIZE and 0 <= ny < SIZE and out[ny][nx][3]:
                        edge[y][x] = True
        for y in range(SIZE):
            for x in range(SIZE):
                if edge[y][x]:
                    out[y][x] = (*DARK, 255)
    return out


def write_png(path, rows):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    raw = b"".join(b"\x00" + bytes(c for px in row for c in px) for row in rows)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)
    print("wrote", os.path.relpath(path, ROOT))


def wanted(name):
    return not FILTER or any(f in name for f in FILTER)


_locked = {}


def sprite(folder, name, build, tilt=12.0, fill=0.8, bottom=1.5, group=None):
    """Renders one sprite. Sprites in the same `group` (a monster's poses) share the first one's framing, so the
    model doesn't grow or shrink between frames."""
    if not wanted(name):
        return
    reset(True)
    build()
    lights()
    if group in _locked:
        bpy.ops.object.camera_add()
        cam = bpy.context.object
        cam.data.type = "ORTHO"
        cam.data.ortho_scale, cam.location, cam.rotation_euler = _locked[group]
        bpy.context.scene.camera = cam
    else:
        cam = fit_camera(tilt, fill, bottom)
        if group:
            _locked[group] = (cam.data.ortho_scale, cam.location.copy(), cam.rotation_euler.copy())
    write_png(os.path.join(OUT, folder, name + ".png"), shrink(render(), True))


def texture(name, build):
    """A tileable 1x1 texture seen straight on from above: keep all detail inside the square."""
    if not wanted(name):
        return
    reset(False)
    build()
    lights(key=3.5, fill=0.8, key_rot=(35, -25, 20))
    bpy.ops.object.camera_add(location=(0.5, 0.5, 5))
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 1.0
    bpy.context.scene.camera = cam
    write_png(os.path.join(OUT, "textures", name + ".png"), shrink(render(), False))


# ---------------------------------------------------------------- pickups

STEEL = dict(metal=0.85, rough=0.3)


def stim():
    add("cyl", (0, 0, 0.55), (0.16, 0.16, 0.42), m=mat((200, 230, 255), rough=0.1, emit=(90, 230, 150), strength=2.5))
    add("cyl", (0, 0, 0.08), (0.2, 0.2, 0.08), m=mat((210, 214, 222), **STEEL))
    add("cyl", (0, 0, 1.02), (0.2, 0.2, 0.07), m=mat((210, 214, 222), **STEEL))
    add("cyl", (0, 0, 1.18), (0.05, 0.05, 0.12), m=mat((150, 150, 160), **STEEL))
    add("torus", (0, 0, 0.55), (1, 1, 1), m=mat((230, 60, 60)), major_radius=0.17, minor_radius=0.025)


def medkit():
    add("cube", (0, 0, 0.35), (1.1, 0.5, 0.7), m=mat((235, 236, 240), rough=0.35), bevel=0.06)
    red = mat((220, 40, 40), rough=0.4)
    add("cube", (0, -0.26, 0.35), (0.14, 0.02, 0.46), m=red)
    add("cube", (0, -0.26, 0.35), (0.46, 0.02, 0.14), m=red)
    add("cube", (0, 0, 0.74), (0.5, 0.12, 0.08), m=mat((80, 84, 92), **STEEL), bevel=0.02)
    add("cube", (0, 0, 0.36), (1.12, 0.52, 0.06), m=mat((90, 94, 104), **STEEL))


def nano():
    gold = mat((215, 170, 60), metal=0.9, rough=0.3)
    add("cyl", (0, 0, 0.5), (0.42, 0.42, 0.5), m=gold)
    add("cyl", (0, 0, 0.5), (0.435, 0.435, 0.22), m=mat((255, 240, 170), emit=(255, 225, 120), strength=3))
    for z in (0.06, 0.94):
        add("cyl", (0, 0, z), (0.46, 0.46, 0.07), m=mat((110, 100, 90), **STEEL))
    add("cyl", (0, 0, 1.06), (0.15, 0.15, 0.08), m=mat((110, 100, 90), **STEEL))


def cells(glow):
    add("cube", (0, 0, 0.18), (1.0, 0.5, 0.36), m=mat((50, 54, 62), **STEEL), bevel=0.04)
    for x in (-0.3, 0, 0.3):
        add("cyl", (x, 0, 0.6), (0.12, 0.12, 0.3), m=mat(glow, rough=0.2, emit=glow, strength=3))
        add("cyl", (x, 0, 0.93), (0.13, 0.13, 0.04), m=mat((190, 194, 204), **STEEL))
    add("cube", (0, -0.26, 0.2), (0.7, 0.02, 0.08), m=mat(glow, emit=glow, strength=2))


def keycard(color):
    card = add("cube", (0, 0, 0.62), (0.8, 0.05, 1.2), rot=(0, 12, 0), m=mat(color, rough=0.3), bevel=0.04)
    add("cube", (0.02, -0.035, 0.84), (0.7, 0.02, 0.16), rot=(0, 12, 0), m=mat((245, 245, 250), emit=(240, 240, 255), strength=0.6))
    add("cube", (-0.14, -0.035, 0.42), (0.22, 0.02, 0.18), rot=(0, 12, 0), m=mat((230, 190, 70), metal=0.9, rough=0.3))
    add("cube", (0, 0, 0.03), (0.5, 0.3, 0.06), m=mat((70, 74, 82), **STEEL))
    return card


def vest():
    plate = mat((80, 110, 78), metal=0.3, rough=0.55)
    add("cube", (0, 0, 0.55), (0.9, 0.4, 1.0), m=plate, bevel=0.12)
    for x in (-0.55, 0.55):
        add("sphere", (x, 0, 0.98), (0.26, 0.26, 0.16), m=mat((66, 92, 64), metal=0.3, rough=0.5))
    add("cyl", (0, -0.05, 1.08), (0.22, 0.22, 0.1), m=mat((25, 25, 30)))
    stripe = mat((215, 205, 90), rough=0.4)
    add("cube", (0, -0.21, 0.72), (0.7, 0.02, 0.07), m=stripe)
    add("cube", (0, -0.21, 0.45), (0.7, 0.02, 0.07), m=stripe)


def jetpack():
    metal = mat((205, 210, 218), **STEEL)
    for x in (-0.36, 0.36):
        add("cyl", (x, 0, 0.62), (0.27, 0.27, 0.5), m=metal)
        add("sphere", (x, 0, 1.12), (0.27, 0.27, 0.27), m=metal)
        add("torus", (x, 0, 0.8), (1, 1, 1), m=mat((230, 170, 40), metal=0.6, rough=0.35), major_radius=0.28, minor_radius=0.035)
        add("cone", (x, 0, 0.05), (1, 1, 1), m=mat((70, 74, 84), **STEEL), radius1=0.12, radius2=0.2, depth=0.24)
        add("cone", (x, 0, -0.22), (1, 1, 1), rot=(180, 0, 0), m=mat((130, 220, 255), emit=(120, 220, 255), strength=6),
            radius1=0.14, radius2=0.0, depth=0.34)
    add("cube", (0, 0.12, 0.66), (0.44, 0.24, 0.9), m=mat((60, 64, 74), metal=0.6, rough=0.45), bevel=0.04)
    add("cube", (0, -0.03, 0.85), (0.22, 0.06, 0.1), m=mat((60, 230, 120), emit=(60, 255, 120), strength=4))


def weapon_crate(glow):
    add("cube", (0, 0, 0.4), (1.2, 0.7, 0.8), m=mat((74, 80, 90), metal=0.7, rough=0.4), bevel=0.05)
    add("cube", (0, -0.36, 0.4), (1.0, 0.02, 0.1), m=mat(glow, emit=glow, strength=4))
    for x in (-0.55, 0.55):
        add("cube", (x, 0, 0.4), (0.1, 0.74, 0.84), m=mat((220, 170, 40), metal=0.5, rough=0.4))
    # a rifle silhouette stencilled on the lid
    add("cube", (0, -0.36, 0.62), (0.6, 0.02, 0.07), m=mat((230, 230, 235)))
    add("cube", (-0.12, -0.36, 0.55), (0.1, 0.02, 0.1), m=mat((230, 230, 235)))


# ---------------------------------------------------------------- the drone (Afrit)

def drone(pose):
    tilt = {"walk0": -12, "walk1": 12}.get(pose, 0)
    hull = mat((250, 230, 210), emit=(255, 170, 110), strength=1.5) if pose == "pain" else mat((168, 174, 186), metal=0.75, rough=0.35)
    dark = mat((64, 68, 78), metal=0.7, rough=0.45)
    parts = []
    # saucer body with a glass dome, seen from the front
    parts.append(add("sphere", (0, 0, 1.0), (1.0, 0.8, 0.5), m=hull))
    parts.append(add("torus", (0, 0, 0.95), (1, 0.8, 1), m=dark, major_radius=1.0, minor_radius=0.1))
    parts.append(add("sphere", (0, 0.1, 1.4), (0.42, 0.36, 0.3), m=mat((90, 170, 220), rough=0.1, emit=(60, 140, 220), strength=0.8)))
    # side thruster pods with blue jets
    for x in (-1.15, 1.15):
        parts.append(add("cyl", (x, 0, 0.85), (0.2, 0.2, 0.32), m=dark))
        parts.append(add("cone", (x, 0, 0.42), (1, 1, 1), rot=(180, 0, 0), m=mat((120, 220, 255), emit=(120, 220, 255), strength=6),
                         radius1=0.17, radius2=0.0, depth=0.46))
    # the big red sensor eye on the front, in a dark housing
    eye_r = 0.3 if pose == "attack" else 0.22
    parts.append(add("cyl", (0, -0.78, 0.95), (0.36, 0.36, 0.12), rot=(90, 0, 0), m=mat((28, 28, 34), metal=0.5, rough=0.5)))
    parts.append(add("sphere", (0, -0.86, 0.95), (eye_r, 0.14, eye_r), m=mat((255, 60, 30), emit=(255, 50, 20), strength=4 if pose == "attack" else 2)))
    if pose == "attack":
        parts.append(add("sphere", (0, -1.2, 0.95), (0.2, 0.2, 0.2), m=mat((255, 230, 160), emit=(255, 210, 120), strength=12)))
    parts.append(add("cyl", (0.35, 0, 1.75), (0.025, 0.025, 0.35), m=mat((200, 200, 210), **STEEL)))
    parts.append(add("sphere", (0.35, 0, 2.1), (0.08, 0.08, 0.08), m=mat((255, 60, 60), emit=(255, 50, 50), strength=6)))
    # bank the whole drone as it weaves
    bpy.ops.object.empty_add(location=(0, 0, 1.0))
    pivot = bpy.context.object
    for p in parts:
        p.parent = pivot
        p.matrix_parent_inverse = pivot.matrix_world.inverted()
    pivot.rotation_euler = (0, math.radians(tilt), 0)
    bpy.context.view_layer.update()


# ---------------------------------------------------------------- wall and floor textures

def base(rgb, metal=0.6, rough=0.5):
    add("cube", (0.5, 0.5, -0.05), (1, 1, 0.1), m=mat(rgb, metal=metal, rough=rough))


def panel(x0, y0, x1, y1, m, h=0.03, bevel=0.012):
    add("cube", ((x0 + x1) / 2, (y0 + y1) / 2, h / 2), (x1 - x0, y1 - y0, h), m=m, bevel=bevel)


def bolt(x, y, m):
    add("cyl", (x, y, 0.035), (0.014, 0.014, 0.012), m=m, vertices=10)


def wall_panels():
    """Station wall: two tall plates, the lower one with a vent grille, a hazard stripe along the join."""
    base((20, 22, 26))
    plate = mat((88, 92, 102), metal=0.75, rough=0.45)
    rivet = mat((150, 154, 164), **STEEL)
    panel(0.02, 0.52, 0.98, 0.98, plate)
    panel(0.02, 0.02, 0.98, 0.42, plate)
    for x in (0.07, 0.93):
        for y in (0.07, 0.37, 0.57, 0.93):
            bolt(x, y, rivet)
    vent = mat((34, 36, 42), metal=0.5, rough=0.6)
    for i in range(6):
        panel(0.3, 0.1 + i * 0.045, 0.7, 0.125 + i * 0.045, vent, h=0.045, bevel=0.004)
    stripe_y = 0.47
    for i in range(10):
        m = mat((230, 180, 40), rough=0.5) if i % 2 == 0 else mat((30, 30, 34), rough=0.5)
        panel(i * 0.1, stripe_y - 0.025, i * 0.1 + 0.1, stripe_y + 0.025, m, h=0.02, bevel=0.0)


def wall_white():
    """Clean white hull plating with an orange accent line and a small status light."""
    base((70, 72, 78))
    plate = mat((200, 204, 210), metal=0.2, rough=0.4)
    panel(0.015, 0.015, 0.49, 0.985, plate)
    panel(0.51, 0.015, 0.985, 0.985, plate)
    panel(0.0, 0.68, 1.0, 0.72, mat((225, 120, 40), rough=0.4), h=0.04, bevel=0.0)
    panel(0.08, 0.2, 0.42, 0.24, mat((160, 164, 172), metal=0.3, rough=0.5), h=0.035, bevel=0.004)
    add("cyl", (0.75, 0.3, 0.04), (0.03, 0.03, 0.02), m=mat((80, 255, 140), emit=(80, 255, 140), strength=5))


def wall_pipes():
    """Service wall: vertical pipes held by brackets over a dark backing plate."""
    base((30, 32, 38))
    panel(0.02, 0.02, 0.98, 0.98, mat((52, 56, 64), metal=0.6, rough=0.55), h=0.01)
    pipe_mats = [mat((150, 120, 80), metal=0.9, rough=0.35), mat((120, 126, 138), **STEEL), mat((150, 120, 80), metal=0.9, rough=0.35)]
    for i, x in enumerate((0.2, 0.5, 0.8)):
        add("cyl", (x, 0.5, 0.07), (0.075, 0.075, 0.49), rot=(90, 0, 0), m=pipe_mats[i])
    bracket = mat((60, 64, 72), metal=0.7, rough=0.5)
    for y in (0.22, 0.78):
        panel(0.05, y - 0.03, 0.95, y + 0.03, bracket, h=0.16, bevel=0.01)
    add("cyl", (0.5, 0.5, 0.15), (0.05, 0.05, 0.02), m=mat((255, 70, 50), emit=(255, 70, 50), strength=4))


def deck_floor():
    """Deck plating: four tread plates with raised studs, bolted at the corners."""
    base((24, 26, 30))
    plate = mat((128, 132, 142), metal=0.6, rough=0.5)
    stud = mat((176, 180, 190), metal=0.6, rough=0.3)
    for (x0, y0) in ((0, 0), (0.5, 0), (0, 0.5), (0.5, 0.5)):
        panel(x0 + 0.015, y0 + 0.015, x0 + 0.485, y0 + 0.485, plate, h=0.025)
        for i in range(4):
            for j in range(4):
                sx, sy = x0 + 0.09 + i * 0.105, y0 + 0.09 + j * 0.105
                rot = 45 if (i + j) % 2 == 0 else -45
                add("cube", (sx, sy, 0.03), (0.05, 0.016, 0.012), rot=(0, 0, rot), m=stud)
        for bx, by in ((0.04, 0.04), (0.46, 0.04), (0.04, 0.46), (0.46, 0.46)):
            bolt(x0 + bx, y0 + by, mat((150, 154, 164), **STEEL))


# ---------------------------------------------------------------- build everything

def main():
    sprite("sprites", "vial", stim)
    sprite("sprites", "flask", medkit)
    sprite("sprites", "urn", nano)
    sprite("sprites", "bluemana", lambda: cells((70, 150, 255)))
    sprite("sprites", "greenmana", lambda: cells((80, 240, 110)))
    sprite("sprites", "steelkey", lambda: keycard((50, 120, 240)), fill=0.62)
    sprite("sprites", "firekey", lambda: keycard((230, 60, 50)), fill=0.62)
    sprite("sprites", "armor", vest)
    sprite("sprites", "jetpack", jetpack)
    sprite("sprites", "weapon2", lambda: weapon_crate((80, 160, 255)))
    sprite("sprites", "weapon3", lambda: weapon_crate((80, 240, 110)))
    # the neutral pain pose first sets the framing; the banked walk frames and the bigger attack glow fit inside it
    for pose in ("pain", "walk0", "walk1", "attack"):
        sprite("monsters", "afrit_" + pose, lambda: drone(pose), tilt=14, fill=0.86, bottom=6, group="afrit")
    texture("stone", wall_panels)
    texture("marble", wall_white)
    texture("brick", wall_pipes)
    texture("floor", deck_floor)


main()
