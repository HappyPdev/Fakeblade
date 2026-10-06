"""
FakeBlade - Exporta cada pieza de la peonza a su propio FBX, listo para Unity.

Exporta todas las mallas de las subcolecciones de "BayBlade 01" (Body, Rings, Punta, Nucleo),
una por archivo, en <carpeta del .blend>/Parts/<Coleccion>/<Nombre>.fbx. Los objetos que
están directamente en "BayBlade 01" (por ejemplo la arena) no se exportan.

En Unity no hay que ajustar nada:
- Se exporta una copia temporal de cada pieza con sus modificadores, rotación y escala ya
  aplicados (también las escalas negativas, corrigiendo las normales). El .blend no se toca.
- La posición se mantiene: todas las piezas comparten el origen (0, 0, 0), así que al ponerlas
  en el mismo punto en Unity quedan montadas.
- Ejes y escala para Unity: Y arriba, -Z adelante, "Apply Transform" y "FBX All". En Unity
  llegan con rotación (0, 0, 0) y escala (1, 1, 1).
- El nombre del objeto dentro del FBX es el de Blender ("Body Type B", "Ring Type A"...), que
  FakeBlade usa para saber qué pieza es al pintarla (Body/Ring/Punta/Nucleo).

Uso en Blender: pestaña Scripting > Abrir este archivo > Run Script (con el .blend guardado).
Uso sin abrir Blender:
    blender -b bayblade_01.blend --python export_blade_parts.py [-- --out <carpeta>]
"""

import os
import re
import sys

import bpy

# === Ajustes ===
ROOT_COLLECTION = "BayBlade 01"
OUTPUT_FOLDER = "//Parts"  # "//" = carpeta del .blend
SKIP_NAMES = set()           # nombres exactos que no se exportan, p. ej. {"Ring"}
SKIP_CONTAINS = ("Type 0",)  # tampoco los que contienen esto (los tipos 0 son los modelos base)


def is_skipped(name):
    return name in SKIP_NAMES or any(part in name for part in SKIP_CONTAINS)


def safe_file_name(name):
    """'Body Type B' -> 'Body_Type_B' (sin espacios ni caracteres raros)."""
    return re.sub(r"[^A-Za-z0-9_-]+", "_", name).strip("_")


def output_root():
    # Carpeta indicada por línea de comandos: -- --out <carpeta>
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--out" in argv:
        return os.path.abspath(argv[argv.index("--out") + 1])
    if not bpy.data.filepath:
        raise RuntimeError("Guarda el .blend antes de exportar (la carpeta de salida es relativa a él).")
    return bpy.path.abspath(OUTPUT_FOLDER)


def baked_copy(obj, depsgraph):
    """Copia temporal con modificadores y transformación aplicados (posición incluida)."""
    evaluated = obj.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
    matrix = obj.matrix_world.copy()
    mesh.transform(matrix)
    if matrix.determinant() < 0.0:
        mesh.flip_normals()  # escala negativa: sin esto la malla saldría del revés
    mesh.update()
    return bpy.data.objects.new(obj.name, mesh)


def export_fbx(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
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
    root = bpy.data.collections.get(ROOT_COLLECTION)
    if root is None:
        raise RuntimeError(f'No existe la colección "{ROOT_COLLECTION}".')

    out_root = output_root()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    scene_collection = bpy.context.scene.collection

    previous_active = bpy.context.view_layer.objects.active
    previous_selection = [o for o in bpy.context.view_layer.objects if o.select_get()]
    if bpy.context.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    exported = []
    for collection in root.children:
        folder = os.path.join(out_root, safe_file_name(collection.name))
        os.makedirs(folder, exist_ok=True)

        # Lista cerrada antes de empezar: crear y borrar las copias invalida el iterador de Blender
        sources = [o for o in collection.all_objects if o.type == "MESH" and not is_skipped(o.name)]
        for source in sources:
            # La copia lleva el nombre original (el que lee Unity); el original se aparta un momento
            original_name = source.name
            source.name = original_name + "__fakeblade_src"
            copy = baked_copy(source, depsgraph)
            copy.name = original_name
            scene_collection.objects.link(copy)
            try:
                path = os.path.join(folder, safe_file_name(original_name) + ".fbx")
                export_fbx(copy, path)
                exported.append(path)
            finally:
                mesh = copy.data
                bpy.data.objects.remove(copy, do_unlink=True)
                bpy.data.meshes.remove(mesh)
                source.name = original_name

    # Deja la selección como estaba
    bpy.ops.object.select_all(action="DESELECT")
    for o in previous_selection:
        if o.name in bpy.context.view_layer.objects:
            o.select_set(True)
    bpy.context.view_layer.objects.active = previous_active

    print(f"[FakeBlade] {len(exported)} piezas exportadas en {out_root}")
    for path in exported:
        print("  " + os.path.relpath(path, out_root))
    return exported


if __name__ == "__main__":
    main()
