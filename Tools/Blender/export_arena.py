"""
FakeBlade - Exporta una arena a FBX, lista para Unity.

Coge un objeto de malla (por defecto "Arena V2 Plana") y lo exporta a
Assets/3D Models/Arenas/<Nombre>.fbx separado en dos mallas:
- "<Nombre> Suelo": las caras que miran hacia arriba a la altura más baja (el suelo plano).
- "<Nombre> Paredes": todo lo demás (pared, borde superior, exterior).
FakeBladeProjectSetup distingue las paredes porque su nombre contiene "Paredes".

En Unity no hay que ajustar nada:
- Se exporta una copia temporal con modificadores, rotación y escala aplicados. El .blend no se toca.
- La copia se centra en X/Y y el suelo queda a altura 0.
- Ejes y escala para Unity: Y arriba, -Z adelante, "Apply Transform" y "FBX All".

Uso en Blender: pestaña Scripting > Abrir este archivo > Run Script (con el .blend guardado).
Uso sin abrir Blender:
    blender -b bayblade_01.blend --python export_arena.py [-- --object "Arena V2 Plana" --out <archivo.fbx>]
"""

import os
import re
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

# === Ajustes ===
ARENA_OBJECT = "Arena V2 Plana"
OUTPUT_FOLDER = "//../Arenas"  # "//" = carpeta del .blend (Assets/3D Models/Bayblade 01)
FLOOR_NORMAL_MIN = 0.99         # cara de suelo: normal casi vertical hacia arriba
FLOOR_HEIGHT_TOLERANCE = 0.002  # fracción del tamaño de la arena


def safe_file_name(name):
    """'Arena V2 Plana' -> 'Arena_V2_Plana' (sin espacios ni caracteres raros)."""
    return re.sub(r"[^A-Za-z0-9_-]+", "_", name).strip("_")


def cli_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = {}
    for key in ("--object", "--out"):
        if key in argv:
            args[key] = argv[argv.index(key) + 1]
    return args


def baked_mesh(obj, depsgraph):
    """Malla temporal con modificadores y transformación aplicados."""
    evaluated = obj.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
    matrix = obj.matrix_world.copy()
    mesh.transform(matrix)
    if matrix.determinant() < 0.0:
        mesh.flip_normals()
    mesh.update()
    return mesh


def floor_face_indices(mesh):
    """Caras que miran hacia arriba a la altura más baja (el suelo)."""
    up_faces = [p for p in mesh.polygons if p.normal.z >= FLOOR_NORMAL_MIN]
    if not up_faces:
        raise RuntimeError("No hay caras mirando hacia arriba: no se encuentra el suelo.")
    size = max(mesh.vertices[i].co.length for p in up_faces for i in p.vertices) or 1.0
    floor_z = min(p.center.z for p in up_faces)
    tolerance = size * FLOOR_HEIGHT_TOLERANCE
    return {p.index for p in up_faces if p.center.z <= floor_z + tolerance}


def sub_mesh(mesh, face_indices, name):
    """Copia de la malla con solo las caras indicadas."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in face_indices], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    result = bpy.data.meshes.new(name)
    bm.to_mesh(result)
    bm.free()
    return result


def export_fbx(objects, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        use_space_transform=True,
        bake_space_transform=True,
        add_leaf_bones=False,
        bake_anim=False,
        use_custom_props=False,
        path_mode="AUTO",
    )


def main():
    args = cli_args()
    name = args.get("--object", ARENA_OBJECT)
    source = bpy.data.objects.get(name)
    if source is None or source.type != "MESH":
        raise RuntimeError(f'No existe el objeto de malla "{name}". Guarda el .blend si acabas de crearlo.')

    if "--out" in args:
        path = os.path.abspath(args["--out"])
    else:
        if not bpy.data.filepath:
            raise RuntimeError("Guarda el .blend antes de exportar (la carpeta de salida es relativa a él).")
        path = os.path.join(bpy.path.abspath(OUTPUT_FOLDER), safe_file_name(name) + ".fbx")
    os.makedirs(os.path.dirname(path), exist_ok=True)

    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    previous_active = bpy.context.view_layer.objects.active
    previous_selection = [o for o in bpy.context.view_layer.objects if o.select_get()]

    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = baked_mesh(source, depsgraph)
    created_meshes = [mesh]
    created_objects = []
    try:
        floor = floor_face_indices(mesh)
        walls = {p.index for p in mesh.polygons} - floor

        # Centrar en X/Y con el suelo y dejarlo a altura 0
        floor_verts = [mesh.vertices[i].co for i in {i for p in mesh.polygons if p.index in floor for i in p.vertices}]
        min_v = Vector((min(v.x for v in floor_verts), min(v.y for v in floor_verts), min(v.z for v in floor_verts)))
        max_v = Vector((max(v.x for v in floor_verts), max(v.y for v in floor_verts), max(v.z for v in floor_verts)))
        offset = Vector(((min_v.x + max_v.x) * -0.5, (min_v.y + max_v.y) * -0.5, -min_v.z))
        mesh.transform(Matrix.Translation(offset))

        parts = [(f"{name} Suelo", floor), (f"{name} Paredes", walls)]
        for part_name, faces in parts:
            if not faces:
                continue
            part_mesh = sub_mesh(mesh, faces, part_name)
            created_meshes.append(part_mesh)
            obj = bpy.data.objects.new(part_name, part_mesh)
            bpy.context.scene.collection.objects.link(obj)
            created_objects.append(obj)

        export_fbx(created_objects, path)
        flatness = max_v.z - min_v.z
        print(f"[FakeBlade] Arena exportada: {path}")
        print(f"  suelo: {len(floor)} caras, diámetro {max_v.x - min_v.x:.3f}, desnivel {flatness:.5f}")
        print(f"  paredes: {len(walls)} caras")
    finally:
        for o in created_objects:
            bpy.data.objects.remove(o, do_unlink=True)
        for m in created_meshes:
            bpy.data.meshes.remove(m)
        bpy.ops.object.select_all(action="DESELECT")
        for o in previous_selection:
            if o.name in bpy.context.view_layer.objects:
                o.select_set(True)
        bpy.context.view_layer.objects.active = previous_active
    return path


if __name__ == "__main__":
    main()
