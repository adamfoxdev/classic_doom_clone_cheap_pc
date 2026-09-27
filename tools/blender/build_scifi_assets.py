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


main()
