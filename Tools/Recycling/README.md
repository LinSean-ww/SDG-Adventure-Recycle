# Recycling model source

`python Tools/Recycling/generate_meshes.py` (Python 3 and numpy) regenerates the 41 OBJ meshes, 14 shared materials and manifest. Existing GUIDs are preserved. It never edits PNG pixels. The four original owner images are in `Assets/_Modules_/02_Levels/Level03/Art/Recycling/SourceImages`.

The model library is `Art/Recycling/Resources/RecyclingArt`. Models use metres, Y up, readable native meshes, valid normals/UVs, and no imported colliders/material instances. Built-in Standard opaque materials sample **interior** regions of the source atlases; silhouettes/alpha fringes are excluded by UV placement. Plastic has a glossy opaque treatment to avoid transparency sorting on small moving items. Photographic lighting is baked into the references; final lighting and artistic fidelity need Unity Game/Scene View review.

There are five semantic variants each for Metal, Plastic and Paper, with Body and Details meshes under one `RAW_MATERIAL_VISUAL_ROOT`. All vertices fit radius 0.118m inside the unchanged 0.12m transport proxy. No glass is generated. Per-material arrival order selects variants deterministically.

Official bales combine a 3D relief shell, front relief and solid wrapped straps. Their union is exactly `(0.52,0.43,0.51)`m. Source 09 supplies the front and 10 the other faces. Source 07 supplies the three separate warehouse bales and wood pallet; their objects never create cargo, receipts or batch records.

No authored `.prefab` is needed: `C3RecyclingVisuals` instantiates the imported OBJ model assets and shared `.mat` assets. The official product/cargo Transform keeps its existing identity. Resources and shaders must be verified in Unity 2023.1.22f1. Asset source checks do not imply Unity import, compilation or PlayMode PASS.
