"""glb2cs.py: GLB -> Unity-convention mesh C# base64 + base color PNG.
usage: python tools/glb2cs.py name:CamelName[:rotYdeg] ...   (reads gen/models/<name>.glb)
e.g.   python tools/glb2cs.py gold_bar:GoldBar coilhead:Coilhead:180
Writes mod/assets/models/<name>.png, mod/Model_<CamelName>.cs, gen/models/preview_<name>.png
"""
import sys, os, base64, struct
import numpy as np, trimesh
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAXTRI = 6000


def load(path):
    sc = trimesh.load(path, force='scene')
    return [g for g in sc.dump() if isinstance(g, trimesh.Trimesh)]


def get_texture_and_uv(m):
    vis = m.visual
    if isinstance(vis, trimesh.visual.texture.TextureVisuals) and vis.uv is not None:
        mat = vis.material
        img = getattr(mat, 'baseColorTexture', None) or getattr(mat, 'image', None)
        if img is not None:
            return img.convert('RGB'), np.asarray(vis.uv, dtype=np.float64)
        return getattr(mat, 'baseColorFactor', None), np.asarray(vis.uv, dtype=np.float64)
    return None, None


def decimate_cluster(m, target):
    size = m.extents.max() / 64
    while True:
        v = np.round(m.vertices / size).astype(np.int64)
        uniq, inv = np.unique(v, axis=0, return_inverse=True)
        inv = inv.reshape(-1)
        nv = np.zeros((len(uniq), 3))
        np.add.at(nv, inv, m.vertices)
        nv /= np.bincount(inv)[:, None]
        f = inv[m.faces]
        f = f[(f[:, 0] != f[:, 1]) & (f[:, 1] != f[:, 2]) & (f[:, 0] != f[:, 2])]
        if len(f) <= target:
            return trimesh.Trimesh(nv, f, process=True)
        size *= 1.05


def decimate(m, target):
    if len(m.faces) <= target:
        return m
    try:
        return m.simplify_quadric_decimation(face_count=target)
    except Exception as e:
        print('  quadric unavailable (%s); vertex clustering' % e)
        size = m.extents.max() / 64
        while True:
            v = np.round(m.vertices / size).astype(np.int64)
            uniq, inv = np.unique(v, axis=0, return_inverse=True)
            inv = inv.reshape(-1)
            nv = np.zeros((len(uniq), 3))
            np.add.at(nv, inv, m.vertices)
            nv /= np.bincount(inv)[:, None]
            f = inv[m.faces]
            f = f[(f[:, 0] != f[:, 1]) & (f[:, 1] != f[:, 2]) & (f[:, 0] != f[:, 2])]
            if len(f) <= target:
                return trimesh.Trimesh(nv, f, process=True)
            size *= 1.1


def convert(name, cam, rot=0.0):
    ms = load(os.path.join(ROOT, 'gen', 'models', name + '.glb'))
    m = trimesh.util.concatenate(ms) if len(ms) > 1 else ms[0]
    tex, uv = get_texture_and_uv(m)
    if uv is None and len(ms) > 1:
        tex, uv = get_texture_and_uv(ms[0]); m = ms[0]
    if uv is None:
        vc = np.asarray(m.visual.to_color().vertex_colors)[:, :3]
        tex = Image.new('RGB', (64, 64), tuple(int(x) for x in vc.mean(axis=0)))
        uv = np.full((len(m.vertices), 2), 0.5)
    elif not isinstance(tex, Image.Image):
        c = tex if tex is not None else (200, 200, 200, 255)
        tex = Image.new('RGB', (64, 64), tuple(int(x) for x in c[:3]))
    verts = np.asarray(m.vertices, dtype=np.float64)
    faces = np.asarray(m.faces)
    orig = trimesh.Trimesh(verts, faces, process=False)
    normals_pre = None
    if len(faces) > MAXTRI:
        mesh0 = trimesh.Trimesh(verts, faces, process=True)
        d = decimate(mesh0, MAXTRI)
        if len(d.faces) > MAXTRI:  # quadric stalled (non-manifold borders): finish by vertex clustering
            d = decimate_cluster(d, MAXTRI)
        # Bake: the decimated mesh gets its own trivial atlas (one square tile per triangle, tiles ordered along a Z-curve),
        # and every texel is filled with the ORIGINAL texture color found at the matching point of the original surface.
        from scipy.spatial import cKDTree
        dv, df = np.asarray(d.vertices), np.asarray(d.faces)
        cen = dv[df].mean(axis=1)
        key = np.zeros(len(cen), dtype=np.int64)
        q = ((cen - cen.min(0)) / (np.ptp(cen, axis=0) + 1e-9) * 1023).astype(np.int64)
        for bit in range(10):
            for ax in range(3):
                key |= ((q[:, ax] >> bit) & 1) << (bit * 3 + ax)
        df = df[np.argsort(key)]
        nf = len(df)
        ATL = 1024
        g = int(np.ceil(np.sqrt(nf)))
        T = min(24, ATL // g)
        assert T >= 6, 'too many triangles for the atlas'
        otri = orig.triangles
        kd = cKDTree(orig.triangles_center)
        ys, xs = np.mgrid[0:T, 0:T]
        lx = (xs + 0.5).reshape(-1); ly = (ys + 0.5).reshape(-1)
        # triangle inside the tile: p0=(1,1) p1=(T-1,1) p2=(1,T-1); barycentric of each texel center
        p0 = np.array([1.0, 1.0]); p1 = np.array([T - 1.0, 1.0]); p2 = np.array([1.0, T - 1.0])
        den = (p1[0] - p0[0]) * (p2[1] - p0[1]) - (p2[0] - p0[0]) * (p1[1] - p0[1])
        w1 = ((lx - p0[0]) * (p2[1] - p0[1]) - (p2[0] - p0[0]) * (ly - p0[1])) / den
        w2 = ((p1[0] - p0[0]) * (ly - p0[1]) - (lx - p0[0]) * (p1[1] - p0[1])) / den
        w0 = 1 - w1 - w2
        B = np.clip(np.stack([w0, w1, w2], 1), 0, None); B /= B.sum(1, keepdims=True)   # clamped: extrapolates into the tile
        timg = np.asarray(tex.convert('RGB'), dtype=np.float64)
        th, tw = timg.shape[:2]
        atlas = np.zeros((ATL, ATL, 3), dtype=np.uint8)
        corners = dv[df]
        uvs = np.zeros((nf, 3, 2))
        for fi0 in range(0, nf, 256):
            fi = np.arange(fi0, min(nf, fi0 + 256))
            pts = np.einsum('tk,fkc->ftc', B, corners[fi]).reshape(-1, 3)       # F*T*T, 3
            _, cand = kd.query(pts, k=8)
            best = np.full(len(pts), 1e18); near = cand[:, 0].copy(); bbc = np.full((len(pts), 3), 1.0 / 3)
            for j in range(cand.shape[1]):
                tr = otri[cand[:, j]]
                bc = trimesh.triangles.points_to_barycentric(tr, pts)
                bc[~np.isfinite(bc).all(axis=1)] = 1.0 / 3
                bc = np.clip(bc, 0, 1); bc /= bc.sum(axis=1, keepdims=True)
                cp = np.einsum('nk,nkc->nc', bc, tr)
                dist = ((cp - pts) ** 2).sum(1)
                m = dist < best
                best[m] = dist[m]; near[m] = cand[m, j]; bbc[m] = bc[m]
            suv = (uv[faces[near]] * bbc[:, :, None]).sum(axis=1)
            px = np.clip(suv[:, 0] * tw - 0.5, 0, tw - 1.001); py = np.clip(suv[:, 1] * th - 0.5, 0, th - 1.001)
            x0 = px.astype(int); y0 = py.astype(int); fx = (px - x0)[:, None]; fy = (py - y0)[:, None]
            col = (timg[y0, x0] * (1 - fx) * (1 - fy) + timg[y0, x0 + 1] * fx * (1 - fy) + timg[y0 + 1, x0] * (1 - fx) * fy + timg[y0 + 1, x0 + 1] * fx * fy)
            col = col.reshape(len(fi), T, T, 3)
            for k, f in enumerate(fi):
                cx, cy = (f % g) * T, (f // g) * T
                atlas[cy:cy + T, cx:cx + T] = np.clip(col[k], 0, 255).astype(np.uint8)
        fidx = np.arange(nf)
        ox = (fidx % g) * T; oy = (fidx // g) * T
        for c, pc in enumerate([p0, p1, p2]):
            uvs[:, c, 0] = (ox + pc[0]) / ATL
            uvs[:, c, 1] = (oy + pc[1]) / ATL
        tex = Image.fromarray(atlas, 'RGB')
        vn = np.asarray(d.vertex_normals)
        # d.faces was reordered above: recompute the matching vertex normals through the same order
        verts = corners.reshape(-1, 3)
        normals_pre = vn[df].reshape(-1, 3)
        uv = uvs.reshape(-1, 2)
        faces = np.arange(nf * 3).reshape(-1, 3)
    if rot:
        a = np.radians(rot)
        R = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
        verts = verts @ R.T
    normals = normals_pre.copy() if normals_pre is not None else np.asarray(trimesh.Trimesh(verts, faces, process=False).vertex_normals).copy()
    mn, mx = verts.min(0), verts.max(0)
    verts = (verts - np.array([(mn[0] + mx[0]) / 2, mn[1], (mn[2] + mx[2]) / 2])) / (mx[1] - mn[1])
    verts[:, 0] *= -1
    normals[:, 0] *= -1
    faces = faces[:, [0, 2, 1]]
    uv = np.stack([uv[:, 0], 1 - uv[:, 1]], axis=1)
    V, I = len(verts), faces.size
    blob = (struct.pack('<ii', V, I) + verts.astype('<f4').tobytes() + normals.astype('<f4').tobytes()
            + uv.astype('<f4').tobytes() + faces.astype('<i4').tobytes())
    if max(tex.size) > 1024:
        s = 1024 / max(tex.size)
        tex = tex.resize((max(1, int(tex.size[0] * s)), max(1, int(tex.size[1] * s))), Image.LANCZOS)
    os.makedirs(os.path.join(ROOT, 'mod', 'assets', 'models'), exist_ok=True)
    tex.save(os.path.join(ROOT, 'mod', 'assets', 'models', name + '.png'))
    with open(os.path.join(ROOT, 'mod', 'Model_%s.cs' % cam), 'w') as f:
        f.write('public static partial class ModelData { public const string %s = "%s"; }\n'
                % (cam, base64.b64encode(blob).decode()))
    print(name, cam, 'V=%d tris=%d tex=%s' % (V, I // 3, tex.size))


def decode(cam):
    s = open(os.path.join(ROOT, 'mod', 'Model_%s.cs' % cam)).read().split('"')[1]
    b = base64.b64decode(s)
    V, I = struct.unpack('<ii', b[:8])
    assert len(b) == 8 + V * 24 + V * 8 + I * 4, 'size mismatch'
    p = np.frombuffer(b, '<f4', V * 3, 8).reshape(V, 3)
    uv = np.frombuffer(b, '<f4', V * 2, 8 + V * 24).reshape(V, 2)
    idx = np.frombuffer(b, '<i4', I, 8 + V * 32)
    assert idx.max() < V and I % 3 == 0
    return p, uv, idx.reshape(-1, 3)


def preview(name, cam):
    from PIL import ImageDraw
    p, uv, f = decode(cam)
    tex = np.asarray(Image.open(os.path.join(ROOT, 'mod', 'assets', 'models', name + '.png')).convert('RGB'))
    th, tw = tex.shape[:2]
    S = 400
    sheet = Image.new('RGB', (S * 2, S), (68, 68, 68))
    for k, yaw in enumerate([float(v) for v in os.environ.get('PREVIEW_YAWS', '0,150').split(',')]):
        a = np.radians(yaw)
        # camera on +Z side looking toward -Z; x right in view = unity x (left-handed: mirror handled by +Z viewer)
        x = p[:, 0] * np.cos(a) + p[:, 2] * np.sin(a)
        z = -p[:, 0] * np.sin(a) + p[:, 2] * np.cos(a)   # larger z = closer to viewer
        pts = np.stack([x, p[:, 1], z], 1)
        tri = pts[f]
        n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
        n /= (np.linalg.norm(n, axis=1, keepdims=True) + 1e-9)
        # unity front faces are clockwise seen from outside; viewer-facing normal is -n here
        shade = 0.5 + 0.5 * np.clip(-n @ np.array([0.3, 0.5, 0.8]), 0, 1)
        cuv = uv[f].mean(1)
        px = np.clip((cuv[:, 0] % 1) * (tw - 1), 0, tw - 1).astype(int)
        py = np.clip(((1 - cuv[:, 1]) % 1) * (th - 1), 0, th - 1).astype(int)
        col = tex[py, px] * shade[:, None]
        order = np.argsort(tri[:, :, 2].mean(1))
        img = Image.new('RGB', (S, S), (68, 68, 68))
        d = ImageDraw.Draw(img)
        sc = S * 0.85 / max(1.0, float(np.abs(p[:, [0, 2]]).max() * 2))
        for i in order:
            q = [(S / 2 + t[0] * sc, S * 0.5 + 0.5 * sc - t[1] * sc) for t in tri[i]]
            d.polygon(q, fill=tuple(int(c) for c in col[i]))
        sheet.paste(img, (k * S, 0))
    sheet.save(os.path.join(ROOT, 'gen', 'models', 'preview_%s.png' % name))


if __name__ == '__main__':
    for a in sys.argv[1:]:
        parts = a.split(':')
        rot = float(parts[2]) if len(parts) > 2 else 0.0
        convert(parts[0], parts[1], rot)
        preview(parts[0], parts[1])
