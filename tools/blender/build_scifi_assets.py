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
import random
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


def lights(key=4.0, fill=1.2, key_rot=(50, 15, 35), rim=0.0):
    """A key sun from the front left, a cool fill from the right and, for monsters, a rim light from behind so
    silhouettes stay crisp at 64 pixels."""
    def sun(energy, rot, color=(1, 1, 1)):
        bpy.ops.object.light_add(type="SUN", location=(0, 0, 5))
        o = bpy.context.object
        o.data.energy = energy
        o.data.color = color
        o.rotation_euler = [math.radians(a) for a in rot]
    sun(key, key_rot, (1.0, 0.97, 0.92))
    sun(fill, (60, -20, -150), (0.85, 0.9, 1.0))
    if rim:
        sun(rim, (-55, 0, 10), (0.8, 0.9, 1.0))


def meshes():
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def make_camera(tilt, yaw):
    """Orthographic camera looking at the model from the front (-Y), turned `yaw` degrees around it and tilted
    down by `tilt` degrees."""
    bpy.ops.object.camera_add()
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.rotation_euler = (math.radians(90 - tilt), 0, math.radians(yaw))
    cam.location = cam.rotation_euler.to_matrix() @ Vector((0, 0, 20))
    bpy.context.scene.camera = cam
    bpy.context.view_layer.update()
    return cam


def extents(cam):
    """The model's bounds in the camera's view: (min x, max x, min y, max y)."""
    inv = cam.matrix_world.inverted()
    xs, ys = [], []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in meshes():
        ev = o.evaluated_get(dg)
        for v in ev.data.vertices:
            p = inv @ (o.matrix_world @ v.co)
            xs.append(p.x)
            ys.append(p.y)
    return min(xs), max(xs), min(ys), max(ys)


def fit_camera(tilt=12.0, fill=0.8, bottom=1.5, yaw=0.0, box=None):
    """Frames the model (or the given view-space box) so it fills `fill` of the frame and its lowest point sits
    `bottom` final pixels above the bottom edge."""
    cam = make_camera(tilt, yaw)
    x0, x1, y0, y1 = box or extents(cam)
    xs, ys = (x0, x1), (y0, y1)
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
    """Shrinks a 4x render to game pixels (the output is len(rows)/4 by len(rows[0])/4)."""
    H, W = len(rows) // SCALE, len(rows[0]) // SCALE
    out = []
    for y in range(H):
        line = []
        for x in range(W):
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
        edge = [[False] * W for _ in range(H)]
        for y in range(H):
            for x in range(W):
                if out[y][x][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < W and 0 <= ny < H and out[ny][nx][3]:
                        edge[y][x] = True
        for y in range(H):
            for x in range(W):
                if edge[y][x]:
                    out[y][x] = (*DARK, 255)
    return out


def write_png(path, rows):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    raw = b"".join(b"\x00" + bytes(c for px in row for c in px) for row in rows)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", len(rows[0]), len(rows), 8, 6, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)
    print("wrote", os.path.relpath(path, ROOT))


def wanted(name):
    return not FILTER or any(f in name for f in FILTER)


def sprite(folder, name, build, tilt=12.0, fill=0.8, bottom=1.5):
    """Renders one pickup sprite."""
    if not wanted(name):
        return
    reset(True)
    build()
    lights()
    fit_camera(tilt, fill, bottom)
    write_png(os.path.join(OUT, folder, name + ".png"), shrink(render(), True))


POSES = ("walk0", "walk1", "attack", "pain")


def monster(name, build, tilt=8.0, yaw=0.0, fill=0.92, bottom=0.0):
    """Renders a monster's four live poses. Every pose is measured first and all of them are framed together,
    so the monster keeps its size between frames and a raised arm or a muzzle flash never gets cropped."""
    if not wanted(name):
        return
    box = None
    for pose in POSES:
        reset(True)
        build(pose)
        x0, x1, y0, y1 = extents(make_camera(tilt, yaw))
        box = (x0, x1, y0, y1) if box is None else (min(box[0], x0), max(box[1], x1), min(box[2], y0), max(box[3], y1))
    for pose in POSES:
        reset(True)
        build(pose)
        lights(key=4.5, fill=1.6, rim=3.5)
        fit_camera(tilt, fill, bottom, yaw, box)
        write_png(os.path.join(OUT, "monsters", "%s_%s.png" % (name, pose)), shrink(render(), True))


def texture(name, build):
    """A tileable 1x1 texture seen straight on from above: keep all detail inside the square."""
    if not wanted(name):
        return
    reset(False)
    bpy.context.scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.25
    build()
    lights(key=4.2, fill=0.6, key_rot=(40, -25, 20))
    for o in bpy.context.scene.objects:
        if o.type == "LIGHT":
            o.data.angle = math.radians(6)   # soft contact shadows read as real metal, not CG
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


# ---------------------------------------------------------------- monsters
# Built bolder than the pickups: brighter hulls, strong emissive eyes and weapons, and a rim light, so they
# still read at a distance once shrunk to 64 pixels. Poses: walk0/walk1 (alternate steps), attack, pain.

def pivot(parts, loc, rot):
    """Rotates a group of parts (an arm, a whole body) about a joint at `loc` by `rot` degrees."""
    bpy.ops.object.empty_add(location=loc)
    e = bpy.context.object
    for o in parts:
        o.parent = e
        o.matrix_parent_inverse = e.matrix_world.inverted()
    e.rotation_euler = [math.radians(a) for a in rot]
    bpy.context.view_layer.update()
    return e


def limb(a, b, r, m):
    """A cylinder from point a to point b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    o = add("cyl", (a + b) / 2, (r, r, d.length / 2), m=m, vertices=12)
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return o


def hot(rgb):
    """The pain frame: the hull flashes white-hot."""
    return mat((250, 230, 215), emit=(255, 170, 110), strength=1.6)


def glow(rgb, strength=6.0):
    return mat(rgb, emit=rgb, strength=strength)


def step_of(pose):
    return {"walk0": 1, "walk1": -1}.get(pose, 0)


def drone(pose):
    tilt = {"walk0": -12, "walk1": 12}.get(pose, 0)
    hull = hot(None) if pose == "pain" else mat((178, 186, 200), metal=0.5, rough=0.3)
    dark = mat((58, 62, 72), metal=0.6, rough=0.45)
    parts = []
    # saucer body with a glass dome, seen from the front
    parts.append(add("sphere", (0, 0, 1.0), (1.0, 0.8, 0.5), m=hull))
    parts.append(add("torus", (0, 0, 0.95), (1, 0.8, 1), m=dark, major_radius=1.0, minor_radius=0.1))
    parts.append(add("sphere", (0, 0.1, 1.4), (0.42, 0.36, 0.3), m=mat((90, 170, 220), rough=0.1, emit=(60, 140, 220), strength=0.8)))
    # side thruster pods with blue jets
    for x in (-1.15, 1.15):
        parts.append(add("cyl", (x, 0, 0.85), (0.2, 0.2, 0.32), m=dark))
        parts.append(add("cone", (x, 0, 0.42), (1, 1, 1), rot=(180, 0, 0), m=glow((120, 220, 255)), radius1=0.17, radius2=0.0, depth=0.46))
    # the big red sensor eye on the front, in a dark housing
    eye_r = 0.3 if pose == "attack" else 0.22
    parts.append(add("cyl", (0, -0.78, 0.95), (0.36, 0.36, 0.12), rot=(90, 0, 0), m=mat((28, 28, 34), metal=0.5, rough=0.5)))
    parts.append(add("sphere", (0, -0.86, 0.95), (eye_r, 0.14, eye_r), m=mat((255, 60, 30), emit=(255, 50, 20), strength=4 if pose == "attack" else 2)))
    if pose == "attack":
        parts.append(add("sphere", (0, -1.2, 0.95), (0.2, 0.2, 0.2), m=glow((255, 230, 160), 12)))
    parts.append(add("cyl", (0.35, 0, 1.75), (0.025, 0.025, 0.35), m=mat((200, 200, 210), **STEEL)))
    parts.append(add("sphere", (0.35, 0, 2.1), (0.08, 0.08, 0.08), m=glow((255, 60, 60))))
    pivot(parts, (0, 0, 1.0), (0, tilt, 0))   # bank as it weaves


def brute(pose):
    """Brute mech (Ettin): a bipedal walker with twin sensor heads, a glowing core and an arm cannon."""
    step = step_of(pose)
    hull = hot(None) if pose == "pain" else mat((140, 148, 160), metal=0.45, rough=0.35)
    plate = mat((226, 176, 40), metal=0.3, rough=0.4)
    dark = mat((46, 50, 58), metal=0.5, rough=0.5)
    for side in (-1, 1):
        lift = 0.2 if step == side else 0.0
        x = side * 0.4
        add("cube", (x, -0.06, 0.1 + lift), (0.46, 0.66, 0.2), m=hull, bevel=0.04)       # foot
        add("cube", (x, 0, 0.55 + lift), (0.3, 0.34, 0.72), m=dark, bevel=0.03)          # shin
        add("cube", (x, 0, 1.05 + lift * 0.5), (0.38, 0.42, 0.44), m=hull, bevel=0.04)   # thigh
        add("sphere", (x, -0.2, 0.78 + lift), (0.12, 0.12, 0.12), m=plate)               # knee
    add("cube", (0, 0, 1.32), (0.95, 0.52, 0.26), m=dark, bevel=0.03)                     # pelvis
    add("cube", (0, 0, 1.88), (1.55, 0.92, 0.98), m=hull, bevel=0.09)                     # torso
    add("cube", (0, -0.47, 2.12), (1.1, 0.04, 0.2), m=plate)                             # hazard plate
    add("cube", (0, -0.47, 1.66), (0.56, 0.04, 0.34), m=dark)                            # vent
    add("sphere", (0, -0.5, 1.66), (0.16, 0.09, 0.16), m=glow((255, 150, 60), 7))        # reactor core
    for x in (-0.4, 0.4):                                                                 # twin sensor heads
        add("cube", (x, 0, 2.58), (0.44, 0.52, 0.38), m=hull, bevel=0.05)
        add("cube", (x, -0.27, 2.6), (0.32, 0.04, 0.1), m=glow((255, 50, 30), 6))
    # left arm swings with the stride; the right arm is a cannon, raised to fire
    left = [add("sphere", (-1.0, 0, 2.08), (0.28, 0.28, 0.28), m=plate),
            add("cube", (-1.05, 0, 1.58), (0.28, 0.32, 0.8), m=dark, bevel=0.03),
            add("cube", (-1.05, -0.03, 1.06), (0.4, 0.44, 0.36), m=hull, bevel=0.05)]
    pivot(left, (-1.0, 0, 2.08), (18 * step, 0, 0))
    right = [add("sphere", (1.0, 0, 2.08), (0.28, 0.28, 0.28), m=plate),
             add("cube", (1.05, 0, 1.58), (0.32, 0.36, 0.8), m=dark, bevel=0.03),
             add("cyl", (1.05, 0, 1.02), (0.22, 0.22, 0.3), m=hull),
             add("cyl", (1.05, 0, 0.72), (0.12, 0.12, 0.1), m=dark)]
    if pose == "attack":
        right.append(add("sphere", (1.05, 0, 0.56), (0.26, 0.26, 0.26), m=glow((255, 215, 110), 14)))
    pivot(right, (1.0, 0, 2.08), (0, -150, 0) if pose == "attack" else (-18 * step, 0, 0))


def strider(pose, siege):
    """Strider (Centaur) and siege strider (Slaughtaur): a four-legged walker with a gun turret."""
    step = step_of(pose)
    base = (160, 82, 72) if siege else (150, 164, 128)
    hull = hot(None) if pose == "pain" else mat(base, metal=0.35, rough=0.4)
    dark = mat((48, 52, 58), metal=0.5, rough=0.5)
    trim = mat((70, 74, 80), metal=0.6, rough=0.4)
    add("sphere", (0, 0, 1.3), (0.8, 1.15, 0.42), m=hull)                                 # body
    add("cube", (0, 0, 1.22), (1.4, 1.7, 0.14), m=trim, bevel=0.03)                       # armour skirt
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        lift = 0.22 if sx * sy == step else 0.0                                           # diagonal pairs step together
        hip, knee, foot = (sx * 0.62, sy * 0.72, 1.2), (sx * 1.25, sy * 0.95, 0.95 + lift), (sx * 1.12, sy * 1.05, 0.08 + lift)
        limb(hip, knee, 0.1, dark)
        limb(knee, foot, 0.085, dark)
        add("sphere", knee, (0.13, 0.13, 0.13), m=hull)
        add("cube", foot, (0.26, 0.26, 0.12), m=trim, bevel=0.02)
    add("cube", (0, -0.15, 1.78), (0.85, 0.95, 0.5), m=hull, bevel=0.07)                  # turret
    add("cube", (0, -0.63, 1.84), (0.6, 0.04, 0.18), m=glow((90, 220, 255), 3.5))         # canopy slit
    add("sphere", (0, 0.1, 0.88), (0.28, 0.28, 0.12), m=glow((255, 150, 70), 4))          # belly engine
    if siege:
        for x in (-0.28, 0.28):                                                            # twin heavy cannons
            add("cyl", (x, -0.95, 1.72), (0.13, 0.13, 0.55), rot=(90, 0, 0), m=dark)
            add("cyl", (x, -1.5, 1.72), (0.16, 0.16, 0.08), rot=(90, 0, 0), m=trim)
            if pose == "attack":
                add("sphere", (x, -1.72, 1.72), (0.3, 0.3, 0.3), m=glow((255, 70, 40), 14))
    else:
        add("cyl", (0.34, -1.0, 1.9), (0.07, 0.07, 0.6), rot=(90, 0, 0), m=trim)            # long rail gun
        if pose == "attack":
            add("sphere", (0.34, -1.65, 1.9), (0.24, 0.24, 0.24), m=glow((170, 230, 255), 14))
            limb((0.34, -1.65, 1.9), (0.1, -2.1, 2.3), 0.04, glow((200, 240, 255), 10))


def wraith(pose):
    """Psi wraith (Dark Bishop): a floating robed alien with a glowing spine, casting with raised hands."""
    bob = {"walk0": 0.1, "walk1": -0.1}.get(pose, 0.0)
    robe = hot(None) if pose == "pain" else mat((96, 62, 170), rough=0.75)
    skin = mat((170, 200, 176), rough=0.5)
    z = 0.25 + bob
    add("cone", (0, 0, z + 1.0), (1, 0.8, 1), m=robe, radius1=0.72, radius2=0.14, depth=1.6)       # robe
    add("cone", (0, 0, z + 0.05), (1, 0.8, 1), rot=(180, 0, 0), m=mat((70, 44, 128), rough=0.8), radius1=0.62, radius2=0.0, depth=0.5)
    add("cube", (0, -0.36, z + 1.1), (0.1, 0.04, 1.1), m=glow((110, 235, 255), 5))                 # glowing spine
    add("sphere", (0, 0, z + 1.8), (0.36, 0.32, 0.18), m=robe)                                        # collar
    add("sphere", (0, 0, z + 2.18), (0.36, 0.34, 0.42), m=skin)                                       # bulbous head
    add("sphere", (0, 0.04, z + 2.46), (0.3, 0.3, 0.2), m=mat((200, 140, 220), rough=0.5))          # crest
    for x in (-0.15, 0.15):
        add("sphere", (x, -0.3, z + 2.16), (0.1, 0.05, 0.07), m=mat((24, 12, 34)))
        add("sphere", (x, -0.34, z + 2.16), (0.04, 0.02, 0.04), m=glow((200, 140, 255), 8))
    for side in (-1, 1):
        sh = (side * 0.42, 0, z + 1.72)
        if pose == "attack":
            hand = (side * 1.05, -0.2, z + 2.3)
            limb(sh, hand, 0.09, robe)
            add("sphere", hand, (0.28, 0.28, 0.28), m=glow((110, 255, 160), 3.5))
        else:
            hand = (side * 0.62, -0.15, z + 0.95)
            limb(sh, hand, 0.09, robe)
            add("sphere", hand, (0.1, 0.1, 0.1), m=skin)


def overmind(pose):
    """The Overmind (Heresiarch): a brain in a jar on a hover base, guarded by three orbiting cores."""
    sway = {"walk0": -7, "walk1": 7}.get(pose, 0)
    brain = mat((255, 215, 230), emit=(255, 150, 200), strength=2.5) if pose == "pain" else mat((222, 130, 172), rough=0.45)
    metal = mat((80, 86, 98), metal=0.6, rough=0.4)
    add("cyl", (0, 0, 0.42), (1.05, 1.05, 0.2), m=metal)                                          # hover base
    add("torus", (0, 0, 0.42), (1, 1, 1), m=glow((120, 220, 255), 4), major_radius=1.05, minor_radius=0.05)
    add("cone", (0, 0, 0.1), (1, 1, 1), rot=(180, 0, 0), m=glow((120, 220, 255), 5), radius1=0.5, radius2=0.0, depth=0.25)
    for side in (-1, 1):
        limb((side * 0.7, -0.2, 0.4), (side * 1.4, -0.5, 0.02), 0.06, mat((60, 50, 70), rough=0.6))   # cables
    jar = [add("cyl", (0, 0, 0.72), (0.8, 0.8, 0.12), m=metal),
           add("sphere", (0, 0.2, 1.62), (0.95, 0.75, 0.95), m=mat((26, 34, 56), metal=0.3, rough=0.2)),  # dark back of the jar
           add("torus", (0, 0, 1.62), (1, 1, 1), rot=(90, 0, 0), m=mat((150, 220, 255), metal=0.2, rough=0.1), major_radius=0.95, minor_radius=0.07),
           add("sphere", (0, -0.25, 1.6), (0.72, 0.6, 0.64), m=brain)]
    for i in range(4):                                                                              # brain folds
        jar.append(add("torus", (0, -0.25, 1.35 + i * 0.16), (0.72 * (1 - abs(i - 1.5) * 0.18), 0.6, 0.4),
                       m=mat((170, 90, 130), rough=0.5), major_radius=1.0, minor_radius=0.05))
    jar.append(add("sphere", (-0.28, -0.72, 1.95), (0.1, 0.06, 0.1), m=glow((255, 255, 255), 3)))   # glint
    pivot(jar, (0, 0, 0.7), (0, sway, 0))
    for i, color in enumerate(((255, 80, 80), (80, 255, 120), (80, 150, 255))):                   # orbiting cores
        a = math.radians(-90 + i * 120 + sway * 3)
        add("cube", (math.cos(a) * 1.45, math.sin(a) * 0.6, 1.3 + (0.9 if i == 2 else 0)), (0.36, 0.36, 0.36), rot=(45, 45, 0), m=glow(color, 3))
    if pose == "attack":
        for side in (-1, 1):
            tip = (side * 1.6, -0.6, 2.4)
            add("sphere", tip, (0.3, 0.3, 0.3), m=glow((255, 90, 255), 3.5))
            limb((side * 0.5, -0.6, 1.9), tip, 0.06, glow((255, 140, 255), 3.5))


# ---------------------------------------------------------------- wall and floor textures
# Surfaces use a procedural weathering shader: colour and roughness variation, grime pooled in crevices
# (ambient occlusion) and running down walls in streaks, worn bright edges (bevel normal vs true normal),
# fine scratches and a micro bump. All noise is sampled on a 4D torus, so every texture still tiles.

def _math(nt, op, a, b=None):
    n = nt.nodes.new("ShaderNodeMath")
    n.operation = op
    for i, v in enumerate((a, b)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            n.inputs[i].default_value = v
        else:
            nt.links.new(v, n.inputs[i])
    return n.outputs[0]


def _range(nt, v, lo, hi, to_lo=0.0, to_hi=1.0):
    n = nt.nodes.new("ShaderNodeMapRange")
    n.clamp = True
    nt.links.new(v, n.inputs["Value"])
    n.inputs["From Min"].default_value, n.inputs["From Max"].default_value = lo, hi
    n.inputs["To Min"].default_value, n.inputs["To Max"].default_value = to_lo, to_hi
    return n.outputs["Result"]


def _mix(nt, fac, a, b):
    n = nt.nodes.new("ShaderNodeMix")
    n.data_type = "RGBA"
    n.clamp_factor = True
    nt.links.new(fac, n.inputs[0])
    ins = [s for s in n.inputs if s.type == "RGBA"]
    for sock, v in zip(ins, (a, b)):
        if isinstance(v, tuple):
            sock.default_value = (*v, 1)
        else:
            nt.links.new(v, sock)
    return [s for s in n.outputs if s.type == "RGBA"][0]


def _torus(nt, rx, ry, seed):
    """Tile position -> (vector, w) on a torus with radii rx, ry. A small ry stretches features vertically."""
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Position"], sep.inputs[0])
    ax = _math(nt, "MULTIPLY", sep.outputs[0], 2 * math.pi)
    ay = _math(nt, "MULTIPLY", sep.outputs[1], 2 * math.pi)
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(_math(nt, "MULTIPLY", _math(nt, "COSINE", ax), rx), comb.inputs[0])
    nt.links.new(_math(nt, "MULTIPLY", _math(nt, "SINE", ax), rx), comb.inputs[1])
    nt.links.new(_math(nt, "ADD", _math(nt, "MULTIPLY", _math(nt, "COSINE", ay), ry), seed * 3.7), comb.inputs[2])
    w = _math(nt, "ADD", _math(nt, "MULTIPLY", _math(nt, "SINE", ay), ry), seed * 5.3)
    return comb.outputs[0], w


def _noise(nt, coords, scale, detail=4.0, rough=0.55):
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = "4D"
    nt.links.new(coords[0], n.inputs["Vector"])
    nt.links.new(coords[1], n.inputs["W"])
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    return n.outputs["Fac"]


def surf(rgb, metal=0.6, rough=0.5, dirt=0.5, wear=0.5, streak=0.0, scratch=0.3, bump=0.15,
         bare=None, grime=(30, 26, 22)):
    """A weathered, tileable surface. `bare` is what worn edges and scratches expose (default: brighter self)."""
    key = ("surf", tuple(rgb), metal, rough, dirt, wear, streak, scratch, bump, bare, grime)
    if key in _mats:
        return _mats[key]
    seed = len(_mats) + 1
    m = bpy.data.materials.new("s%d" % len(_mats))
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    L = nt.links
    sq = _torus(nt, 1.0, 1.0, seed)

    fine = _noise(nt, sq, 5.0, 4.0, 0.55)
    blotch = _noise(nt, sq, 1.4, 3.0, 0.5)
    breakup = _noise(nt, sq, 4.0, 5.0, 0.65)

    # colour: gentle value variation
    col = _mix(nt, _range(nt, fine, 0.3, 0.7), srgb(tuple(c * 0.9 for c in rgb)), srgb(tuple(min(255, c * 1.06) for c in rgb)))

    # worn edges
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    bev = nt.nodes.new("ShaderNodeBevel")
    bev.samples = 8
    bev.inputs["Radius"].default_value = 0.012
    dot = nt.nodes.new("ShaderNodeVectorMath")
    dot.operation = "DOT_PRODUCT"
    L.new(bev.outputs["Normal"], dot.inputs[0])
    L.new(geo.outputs["Normal"], dot.inputs[1])
    edge = _range(nt, dot.outputs["Value"], 0.985, 0.85)
    worn = _math(nt, "MULTIPLY", _math(nt, "MULTIPLY", edge, _range(nt, breakup, 0.38, 0.6)), wear)

    # scratches: thin cell edges of a stretched voronoi, broken up
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    vor.voronoi_dimensions = "4D"
    vor.feature = "DISTANCE_TO_EDGE"
    sc = _torus(nt, 1.0, 0.35, seed + 11)
    L.new(sc[0], vor.inputs["Vector"])
    L.new(sc[1], vor.inputs["W"])
    vor.inputs["Scale"].default_value = 3.0
    scr = _math(nt, "MULTIPLY", _math(nt, "MULTIPLY", _range(nt, vor.outputs["Distance"], 0.012, 0.0), _range(nt, blotch, 0.45, 0.65)), scratch)
    exposed = _math(nt, "MAXIMUM", worn, scr)
    bare_rgb = srgb(bare or tuple(min(255, c * 1.45 + 30) for c in rgb))
    col = _mix(nt, exposed, col, bare_rgb)

    # grime: blotches, crevices and (on walls) vertical run-off streaks
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.samples = 8
    ao.inputs["Distance"].default_value = 0.08
    crev = _range(nt, ao.outputs["AO"], 0.97, 0.4)
    dirty = _math(nt, "MAXIMUM", _range(nt, blotch, 0.44, 0.72), crev)
    if streak:
        st = _noise(nt, _torus(nt, 1.0, 0.12, seed + 23), 4.0, 2.0, 0.5)
        dirty = _math(nt, "MAXIMUM", dirty, _math(nt, "MULTIPLY", _range(nt, st, 0.46, 0.64), streak))
    dirty = _math(nt, "MULTIPLY", _math(nt, "MULTIPLY", dirty, dirt), _math(nt, "SUBTRACT", 1.0, exposed))
    col = _mix(nt, dirty, col, srgb(grime))
    L.new(col, b.inputs["Base Color"])

    r = _math(nt, "ADD", _math(nt, "MULTIPLY", dirty, 0.35), rough)
    r = _math(nt, "SUBTRACT", r, _math(nt, "MULTIPLY", exposed, 0.3))
    r = _math(nt, "ADD", r, _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", fine, 0.5), 0.2))
    L.new(_range(nt, r, 0.0, 1.0, 0.05, 1.0), b.inputs["Roughness"])
    L.new(_math(nt, "MULTIPLY", _math(nt, "SUBTRACT", 1.0, _math(nt, "MULTIPLY", dirty, 0.7)), metal), b.inputs["Metallic"])

    bn = nt.nodes.new("ShaderNodeBump")
    bn.inputs["Strength"].default_value = bump
    bn.inputs["Distance"].default_value = 0.004
    L.new(_math(nt, "ADD", _noise(nt, sq, 8.0, 4.0, 0.6), _math(nt, "MULTIPLY", scr, -0.6)), bn.inputs["Height"])
    L.new(bn.outputs["Normal"], b.inputs["Normal"])
    _mats[key] = m
    return m


def base(rgb, metal=0.6, rough=0.5):
    add("cube", (0.5, 0.5, -0.05), (1, 1, 0.1), m=surf(rgb, metal=metal, rough=rough, dirt=0.8, wear=0.0, scratch=0.0))


def panel(x0, y0, x1, y1, m, h=0.03, bevel=0.012):
    add("cube", ((x0 + x1) / 2, (y0 + y1) / 2, h / 2), (x1 - x0, y1 - y0, h), m=m, bevel=bevel)


def bolt(x, y, m, z=0.035):
    add("cyl", (x, y, z), (0.016, 0.016, 0.014), m=m, vertices=12, bevel=0.004)


RIVET = dict(metal=0.85, rough=0.35, dirt=0.6, wear=0.8, scratch=0.0)
PAINT_CHIP = (150, 148, 145)   # steel under chipped paint


def wall_panels():
    """Station wall: two tall plates, the lower one with a vent grille, a hazard stripe along the join."""
    base((20, 22, 26))
    plate = surf((88, 92, 102), metal=0.75, rough=0.45, dirt=0.75, wear=0.7, streak=0.8)
    plate2 = surf((80, 84, 94), metal=0.75, rough=0.5, dirt=0.75, wear=0.7, streak=0.8)
    rivet = surf((150, 154, 164), **RIVET)
    panel(0.02, 0.52, 0.49, 0.98, plate)
    panel(0.51, 0.52, 0.98, 0.98, plate2)
    panel(0.02, 0.02, 0.98, 0.42, plate)
    panel(0.1, 0.6, 0.4, 0.9, plate2, h=0.045, bevel=0.01)          # inset access hatch
    for x in (0.07, 0.44, 0.56, 0.93):
        for y in (0.57, 0.93):
            bolt(x, y, rivet)
    for x in (0.07, 0.93):
        for y in (0.07, 0.37):
            bolt(x, y, rivet)
    vent = surf((34, 36, 42), metal=0.5, rough=0.6, dirt=0.9, wear=0.4, scratch=0.0)
    panel(0.27, 0.07, 0.73, 0.37, surf((18, 18, 22), metal=0.3, rough=0.8, dirt=0.3, wear=0.0, scratch=0.0), h=0.02, bevel=0.006)
    for i in range(6):
        panel(0.3, 0.1 + i * 0.045, 0.7, 0.125 + i * 0.045, vent, h=0.045, bevel=0.006)
    stripe_y = 0.47
    yellow = surf((220, 170, 36), metal=0.1, rough=0.55, dirt=0.5, wear=0.9, scratch=0.6, bare=PAINT_CHIP)
    black = surf((30, 30, 34), metal=0.1, rough=0.55, dirt=0.3, wear=0.9, scratch=0.6, bare=PAINT_CHIP)
    for i in range(10):
        panel(i * 0.1, stripe_y - 0.03, i * 0.1 + 0.1, stripe_y + 0.03, yellow if i % 2 == 0 else black, h=0.02, bevel=0.0)


def wall_white():
    """White hull plating with an orange accent line and a small status light, scuffed and grimy."""
    base((70, 72, 78))
    plate = surf((196, 200, 206), metal=0.15, rough=0.4, dirt=0.6, wear=0.8, streak=0.75, scratch=0.5, bare=(120, 124, 130),
                 grime=(92, 84, 72))
    panel(0.015, 0.015, 0.49, 0.985, plate)
    panel(0.51, 0.015, 0.985, 0.985, plate)
    orange = surf((225, 120, 40), metal=0.1, rough=0.45, dirt=0.4, wear=0.8, scratch=0.5, bare=PAINT_CHIP)
    panel(0.0, 0.68, 1.0, 0.72, orange, h=0.04, bevel=0.004)
    trim = surf((150, 154, 162), metal=0.6, rough=0.45, dirt=0.6, wear=0.5)
    panel(0.08, 0.2, 0.42, 0.24, trim, h=0.035, bevel=0.006)
    panel(0.66, 0.22, 0.84, 0.38, trim, h=0.035, bevel=0.006)                 # light housing
    add("cyl", (0.75, 0.3, 0.05), (0.03, 0.03, 0.02), m=mat((80, 255, 140), emit=(80, 255, 140), strength=5))
    rivet = surf((170, 174, 180), **RIVET)
    for x in (0.05, 0.455, 0.545, 0.95):
        for y in (0.06, 0.94):
            bolt(x, y, rivet)


def wall_pipes():
    """Service wall: vertical pipes held by brackets over a dark backing plate."""
    base((30, 32, 38))
    panel(0.02, 0.02, 0.98, 0.98, surf((52, 56, 64), metal=0.6, rough=0.55, dirt=0.75, wear=0.3, streak=0.8), h=0.01)
    copper = surf((150, 110, 72), metal=0.9, rough=0.35, dirt=0.6, wear=0.7, streak=0.4, grime=(50, 70, 58))  # verdigris
    steel = surf((120, 126, 138), metal=0.85, rough=0.3, dirt=0.55, wear=0.6, streak=0.4)
    for m, x in ((copper, 0.2), (steel, 0.5), (copper, 0.8)):
        add("cyl", (x, 0.5, 0.07), (0.075, 0.075, 0.49), rot=(90, 0, 0), m=m, vertices=32)
    bracket = surf((60, 64, 72), metal=0.7, rough=0.5, dirt=0.6, wear=0.8)
    rivet = surf((150, 154, 164), **RIVET)
    for y in (0.22, 0.78):
        panel(0.05, y - 0.03, 0.95, y + 0.03, bracket, h=0.16, bevel=0.012)
        for x in (0.08, 0.92):
            bolt(x, y, rivet, z=0.165)
    add("cyl", (0.5, 0.5, 0.15), (0.05, 0.05, 0.02), m=mat((255, 70, 50), emit=(255, 70, 50), strength=4))


def deck_floor():
    """Deck plating: four scuffed tread plates with raised diamond studs, bolted at the corners."""
    base((24, 26, 30))
    plates = [surf((124, 128, 138), metal=0.65, rough=0.5, dirt=0.75, wear=0.7, scratch=0.8, bump=0.2),
              surf((114, 118, 128), metal=0.65, rough=0.55, dirt=0.75, wear=0.7, scratch=0.8, bump=0.2)]
    stud = surf((170, 174, 184), metal=0.7, rough=0.25, dirt=0.3, wear=1.0, scratch=0.0)
    rivet = surf((150, 154, 164), **RIVET)
    for k, (x0, y0) in enumerate(((0, 0), (0.5, 0), (0, 0.5), (0.5, 0.5))):
        panel(x0 + 0.015, y0 + 0.015, x0 + 0.485, y0 + 0.485, plates[k % 3 % 2], h=0.025)
        for i in range(3):
            for j in range(3):
                sx, sy = x0 + 0.115 + i * 0.135, y0 + 0.115 + j * 0.135
                rot = 45 if (i + j) % 2 == 0 else -45
                add("cube", (sx, sy, 0.036), (0.085, 0.03, 0.022), rot=(0, 0, rot), m=stud, bevel=0.006)
        for bx, by in ((0.045, 0.045), (0.455, 0.045), (0.045, 0.455), (0.455, 0.455)):
            bolt(x0 + bx, y0 + by, rivet)


def rubble():
    """Cave-in rubble ('K'): faceted asteroid rocks packed in grit, with glinting ore. Rocks that cross the tile's
    edge are repeated on the far side, so the tile still wraps."""
    rng = random.Random(11)
    base((20, 18, 22))
    rocks = [surf(c, metal=0.05, rough=0.8, dirt=0.55, wear=0.5, scratch=0.15, bump=0.4, grime=(26, 22, 20))
             for c in ((104, 94, 86), (90, 84, 82), (118, 108, 96))]
    tex = bpy.data.textures.new("rock", "CLOUDS")
    tex.noise_scale = 0.6
    n = 3
    for i in range(n):
        for j in range(n):
            cx = (i + 0.5 + (j % 2) * 0.5) / n + rng.uniform(-0.04, 0.04)
            cy = (j + 0.5) / n + rng.uniform(-0.03, 0.03)
            r = rng.uniform(0.175, 0.205)
            sc = (r * rng.uniform(0.95, 1.15), r * rng.uniform(0.85, 1.0), r * 0.55)
            rot = (rng.uniform(-20, 20), rng.uniform(-20, 20), rng.uniform(0, 360))
            m = rocks[rng.randrange(len(rocks))]
            for ox in (-1, 0, 1):
                for oy in (-1, 0, 1):
                    x, y = cx % 1.0 + ox, cy + oy
                    if x + r < 0 or x - r > 1 or y + r < 0 or y - r > 1:
                        continue
                    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1, location=(x, y, 0.02),
                                                          rotation=[math.radians(a) for a in rot])
                    o = bpy.context.object
                    o.scale = sc
                    o.data.materials.append(m)
                    d = o.modifiers.new("displace", "DISPLACE")
                    d.texture = tex
                    d.strength = 0.55
    ore = mat((70, 200, 230), emit=(70, 200, 230), strength=3)
    for _ in range(6):
        x, y = rng.uniform(0.08, 0.92), rng.uniform(0.08, 0.92)
        add("cone", (x, y, 0.07), (1, 1, 1), rot=(rng.uniform(-25, 25), rng.uniform(-25, 25), 0), m=ore,
            radius1=0.018, radius2=0.0, depth=0.07, vertices=5)


# ---------------------------------------------------------------- first-person weapons
# Seen from behind and above, the way you hold them: the barrel runs up the screen and your sleeve comes in from the
# bottom edge. One fixed camera for all of them, so they share a scale. Each has a resting and a firing frame.

WPN_W, WPN_H = 128, 80           # the game's first-person weapon frame
SLEEVES = [(70, 100, 70), (200, 120, 40), (90, 70, 150)]   # Marine, Engineer, Psion armour


def weapon_camera():
    """The player's eye: a perspective camera with the game's 74-degree field of view, cropped (with lens shift) to
    the part of the 320x168 view the weapon frame covers - 128x80, centred 20 pixels right of middle, sitting on
    the bottom edge. Barrels pointing straight ahead then run toward the crosshair, as they do in the game."""
    bpy.ops.object.camera_add(location=(0, 0, 0), rotation=(math.radians(90), 0, 0))
    cam = bpy.context.object
    cam.data.type = "PERSP"
    cam.data.sensor_fit = "HORIZONTAL"
    view_w, view_h, half_fov = 320, 168, math.radians(37)
    half = math.tan(half_fov) * WPN_W / view_w
    cam.data.angle = 2 * math.atan(half)
    cam.data.shift_x = 20 / WPN_W
    cam.data.shift_y = -(view_h / 2 - (WPN_H - 4) / 2) / WPN_W
    cam.data.clip_start = 0.01
    bpy.context.scene.camera = cam


# where a weapon model (built around its grip, pointing along +Y, about a unit long) sits in front of the eye
WPN_HOLD = dict(loc=(0.0, 0.75, -0.22), scale=0.5, rot=(-2, 0, 8))


# melee weapons are held lower and further out, so a raised blade or a punch stays in frame
MELEE_HOLD = dict(loc=(0.05, 1.0, -0.3), scale=0.5, rot=(0, 0, 0))


def weapon(name, build, hold=WPN_HOLD):
    """Renders a weapon's resting and firing frames as <name>_idle and <name>_fire."""
    if not wanted(name):
        return
    for frame in ("idle", "fire"):
        sc = reset(True)
        sc.render.resolution_x, sc.render.resolution_y = WPN_W * SCALE, WPN_H * SCALE
        build(frame == "fire")
        bpy.ops.object.empty_add(location=(0, 0, 0))
        rig = bpy.context.object
        for o in list(bpy.context.scene.objects):
            if o.type in ("MESH", "EMPTY") and o is not rig and o.parent is None:
                o.parent = rig
        rig.location = hold["loc"]
        rig.scale = (hold["scale"],) * 3
        rig.rotation_euler = [math.radians(v) for v in hold["rot"]]
        bpy.context.view_layer.update()
        lights(key=2.6, fill=1.0, key_rot=(-40, 25, 15), rim=1.2)
        weapon_camera()
        write_png(os.path.join(OUT, "weapons", "%s_%s.png" % (name, frame)), shrink(render(), True))


def arm(cls, hand, back=(0.62, -2.2, -1.0), r=0.13):
    """Your sleeve and glove, from the hand back out of the bottom of the view."""
    limb(hand, back, r, mat(SLEEVES[cls], metal=0.2, rough=0.6))
    add("sphere", hand, (r * 1.05, r * 1.05, r * 1.05), m=mat((48, 52, 60), metal=0.3, rough=0.55))


def gun_body(pos, length, width, body, accent, barrel_r=0.04, barrels=(0.0,)):
    """A gun pointing straight ahead: receiver, barrel(s) and a glowing accent strip."""
    x, y, z = pos
    add("cube", (x, y, z), (width, length * 0.55, width * 0.9), m=body, bevel=0.02)
    for bx in barrels:
        add("cyl", (x + bx, y + length * 0.55, z + 0.02), (barrel_r, barrel_r, length * 0.35), rot=(90, 0, 0), m=mat((70, 74, 84), metal=0.8, rough=0.35))
    add("cube", (x, y + length * 0.05, z + width * 0.46), (width * 0.3, length * 0.4, 0.012), m=mat(accent, emit=accent, strength=3))
    return y + length * 0.9      # where the muzzle is


def flash(pos, color, size=0.18, strength=10):
    add("sphere", pos, (size, size, size), m=mat(color, emit=color, strength=strength))


def power_fist(fire):
    """Marine: armoured gauntlets; the right one punches forward crackling with energy."""
    metal = mat((150, 156, 168), metal=0.8, rough=0.3)
    for side in (-1, 1):
        punching = fire and side == 1
        hand = (0.15, 0.6, 0.12) if punching else ((-0.28, 0.0, 0.0) if side < 0 else (0.5, 0.0, 0.0))
        arm(0, hand, back=(hand[0] + side * 0.3, -1.0, -2.0), r=0.13)
        add("cube", hand, (0.3, 0.26, 0.22), m=metal, bevel=0.04)
        for k in range(4):
            add("cube", (hand[0] - 0.1 + k * 0.067, hand[1] + 0.13, hand[2] + 0.06), (0.05, 0.05, 0.06), m=mat((90, 96, 108), metal=0.8, rough=0.3))
        add("cube", (hand[0], hand[1] - 0.02, hand[2] + 0.12), (0.2, 0.12, 0.03), m=mat((120, 200, 255), emit=(120, 200, 255), strength=3 if punching else 1.5))
    if fire:
        flash((0.15, 0.8, 0.14), (140, 210, 255), 0.16, 8)


def vibro_blade(fire):
    """Marine: a humming energy blade, held up and then swung across."""
    hand = (0.15, 0.2, 0.0) if fire else (0.4, 0.0, -0.05)
    arm(0, hand, back=(0.8, -1.0, -2.0))
    parts = [add("cyl", hand, (0.05, 0.05, 0.14), m=mat((50, 54, 60), metal=0.6, rough=0.4)),
             add("cube", (hand[0], hand[1], hand[2] + 0.16), (0.16, 0.05, 0.04), m=mat((150, 156, 168), metal=0.8, rough=0.3)),
             add("cube", (hand[0], hand[1], hand[2] + 0.46), (0.06, 0.015, 0.56), m=mat((200, 240, 255), emit=(120, 220, 255), strength=4))]
    pivot(parts, hand, (0, -75, 0) if fire else (-10, -18, 0))


def grav_launcher(fire):
    """Marine: a heavy launcher with a purple graviton coil."""
    kick = -0.08 if fire else 0.0
    muzzle = gun_body((0.16, 0.05 + kick, -0.02), 0.95, 0.24, mat((110, 116, 128), metal=0.7, rough=0.35), (200, 120, 255), barrel_r=0.08)
    for k in range(3):
        add("torus", (0.16, 0.1 + kick + k * 0.14, 0.02), (1, 1, 1), rot=(90, 0, 0), m=mat((200, 120, 255), emit=(200, 120, 255), strength=3), major_radius=0.12, minor_radius=0.02)
    arm(0, (0.22, -0.32 + kick, -0.08))
    if fire:
        flash((0.16, muzzle + 0.05, 0.02), (210, 140, 255), 0.24)


def shock_baton(fire):
    """Engineer: a baton with a crackling tip, jabbed forward."""
    hand = (0.25, 0.35, -0.02) if fire else (0.4, 0.0, -0.05)
    arm(1, hand, back=(0.8, -1.0, -2.0))
    parts = [add("cyl", (hand[0], hand[1], hand[2] + 0.25), (0.035, 0.035, 0.3), m=mat((60, 62, 70), metal=0.6, rough=0.4)),
             add("cyl", (hand[0], hand[1], hand[2] + 0.56), (0.06, 0.06, 0.05), m=mat((120, 200, 255), emit=(120, 200, 255), strength=3 if fire else 1.5))]
    if fire:
        parts.append(add("sphere", (hand[0], hand[1], hand[2] + 0.62), (0.12, 0.12, 0.12), m=mat((170, 230, 255), emit=(150, 220, 255), strength=8)))
    pivot(parts, hand, (-55, -10, 0) if fire else (-10, -15, 0))


def bio_rifle(fire):
    """Engineer: a rifle fed from a glowing green canister."""
    kick = -0.06 if fire else 0.0
    muzzle = gun_body((0.14, 0.05 + kick, -0.02), 1.0, 0.16, mat((84, 96, 84), metal=0.5, rough=0.45), (120, 255, 120))
    add("cyl", (0.3, -0.05 + kick, 0.02), (0.07, 0.07, 0.2), rot=(90, 0, 0), m=mat((70, 230, 100), rough=0.2, emit=(60, 220, 90), strength=2))
    arm(1, (0.2, -0.3 + kick, -0.08))
    if fire:
        flash((0.14, muzzle + 0.05, 0.02), (130, 255, 130), 0.18)


def flamer(fire):
    """Engineer: a fuel tank and nozzle; firing throws a burst of flame."""
    kick = -0.04 if fire else 0.0
    muzzle = gun_body((0.14, 0.05 + kick, -0.02), 0.85, 0.2, mat((110, 100, 90), metal=0.5, rough=0.45), (255, 150, 40), barrel_r=0.06)
    add("cyl", (0.34, -0.1 + kick, 0.0), (0.1, 0.1, 0.32), rot=(90, 0, 0), m=mat((200, 80, 40), metal=0.3, rough=0.4))
    add("sphere", (0.14, muzzle, 0.02), (0.05, 0.05, 0.05), m=mat((255, 140, 40), emit=(255, 140, 40), strength=2))
    arm(1, (0.2, -0.3 + kick, -0.08))
    if fire:
        for k in range(3):
            flash((0.14 + (k - 1) * 0.06, muzzle + 0.15 + k * 0.12, 0.05 + k * 0.03), (255, 160 - k * 30, 50), 0.14 + k * 0.05, 8)


def blaster(fire):
    """Psion: a compact sidearm."""
    kick = -0.06 if fire else 0.0
    muzzle = gun_body((0.16, 0.0 + kick, -0.04), 0.55, 0.13, mat((130, 136, 148), metal=0.7, rough=0.3), (80, 160, 255))
    arm(2, (0.19, -0.26 + kick, -0.1), r=0.11)
    if fire:
        flash((0.16, muzzle + 0.03, 0.0), (110, 180, 255), 0.14)


def shard_gun(fire):
    """Psion: three barrels firing a spread of ice-blue shards."""
    kick = -0.06 if fire else 0.0
    muzzle = gun_body((0.15, 0.03 + kick, -0.03), 0.8, 0.26, mat((120, 128, 142), metal=0.7, rough=0.3), (160, 230, 255),
                      barrel_r=0.035, barrels=(-0.08, 0.0, 0.08))
    arm(2, (0.2, -0.3 + kick, -0.1), r=0.11)
    if fire:
        for bx in (-0.08, 0.0, 0.08):
            flash((0.15 + bx, muzzle + 0.05, 0.0), (170, 235, 255), 0.09)


def arc_rifle(fire):
    """Psion: a coil rifle that throws lightning."""
    kick = -0.05 if fire else 0.0
    muzzle = gun_body((0.15, 0.05 + kick, -0.02), 1.0, 0.18, mat((80, 76, 100), metal=0.6, rough=0.35), (220, 220, 255), barrel_r=0.05)
    for k in range(4):
        add("torus", (0.15, 0.15 + kick + k * 0.12, 0.02), (1, 1, 1), rot=(90, 0, 0), m=mat((150, 160, 255), emit=(120, 140, 255), strength=1.5), major_radius=0.08, minor_radius=0.015)
    arm(2, (0.21, -0.3 + kick, -0.08), r=0.11)
    if fire:
        pts = [(0.15, muzzle, 0.02), (0.05, muzzle + 0.2, 0.12), (0.2, muzzle + 0.4, 0.1), (0.08, muzzle + 0.62, 0.2)]
        for a, b in zip(pts, pts[1:]):
            limb(a, b, 0.018, mat((150, 180, 255), emit=(130, 170, 255), strength=4))
        flash(pts[0], (150, 180, 255), 0.1, 5)


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
    monster("afrit", drone, tilt=14, fill=0.86, bottom=6)
    monster("ettin", brute, tilt=6, yaw=18, fill=0.98)
    monster("centaur", lambda pose: strider(pose, False), tilt=12, yaw=35)
    monster("slaughtaur", lambda pose: strider(pose, True), tilt=12, yaw=35)
    monster("bishop", wraith, tilt=6, yaw=10, bottom=2)
    monster("heresiarch", overmind, tilt=8, yaw=0)
    texture("stone", wall_panels)
    texture("marble", wall_white)
    texture("brick", wall_pipes)
    texture("floor", deck_floor)
    texture("rubble", rubble)
    weapon("fighter_0", power_fist, MELEE_HOLD)
    weapon("fighter_1", vibro_blade, MELEE_HOLD)
    weapon("fighter_2", grav_launcher)
    weapon("cleric_0", shock_baton, MELEE_HOLD)
    weapon("cleric_1", bio_rifle)
    weapon("cleric_2", flamer)
    weapon("mage_0", blaster)
    weapon("mage_1", shard_gun)
    weapon("mage_2", arc_rifle)


main()
