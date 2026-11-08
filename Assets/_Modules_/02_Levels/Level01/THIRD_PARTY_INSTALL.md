# Level01 third-party installation

The repository deliberately excludes the raw Microsoft JhengHei font, the Nicrom Low Poly Wind assets, and the EmojiOne sprite asset and image whose redistribution terms have not been established. Level01 uses the included Noto Sans CJK TC TMP asset for HUD text and does not require the Microsoft font.

## Required local vegetation package

- Low Poly Wind, publisher Nicrom; Unity Asset Store product ID 182586, version 1.4.0.
- Official listing: https://assetstore.unity.com/packages/vfx/shaders/low-poly-wind-182586
- Confirm that the user and team have all required licenses and seats before obtaining or sharing the raw asset. The repository is not a distribution source for this package.
- Obtain version 1.4.0 using a properly licensed Unity account. The accepted project uses the package files under `Assets/_Modules_/02_Levels/Level01/Imported/Nicrom/Shaders/Wind/`; the package's original asset paths recorded in meta files start with `Assets/Nicrom/Shaders/Wind/`. Preserve the original subdirectories and matching .meta files when installing into the Level01 path. Do not overwrite unrelated project assets. Retain the package's original GUIDs.
- Package Manager may import to the original location. Do not create duplicate copies with the same GUID in the Unity project; remove or relocate only in a safe, user-controlled installation procedure. Confirm the destination paths and GUIDs before moving any files. A different package version can have different content or GUIDs.

The optional original EmojiOne sprite/image are also absent; TMP Settings may report a missing default sprite reference. If required, obtain the matching TMP Essential Resources through Unity with appropriate rights, and verify its GUIDs before placement; do not assume the copied sample can be redistributed.

The expected GUID for the Nicrom root directory meta is `0e1bfc751edb1c149b48b7323806ee29`.

## GUID verification

Compare each installed .meta GUID with the list below. For example in PowerShell, inspect a meta using `Select-String -LiteralPath 'Assets/_Modules_/02_Levels/Level01/Imported/Nicrom/Shaders/Wind/Prefabs/LPW_Flower_A1_H70cm_01.prefab.meta' -Pattern '^guid:'`. For each line below append .meta to the path relative to the Nicrom destination above. Do not generate new meta files as a substitute for missing originals.

| Relative asset or directory | Expected GUID |
| --- | --- |
| `Shaders` | `4c1a47b356b1ed84ab371b4a767a93aa` |
| `Shaders/Wind` | `09d3ce14ddee24240a7b21d8b8dea3ac` |
| `Shaders/Wind/Assets` | `1901b25bd6440714fa00476a03b0550d` |
| `Shaders/Wind/Assets/Editor` | `e76a32374fb3ca841ab21c732f1cbaaa` |
| `Shaders/Wind/Assets/Editor/Scripts` | `fad4a64fa05f16f42824a8eb306222ad` |
| `Shaders/Wind/Assets/Editor/Scripts/LPVegetation_MaterialInspector.cs` | `597615af27a084144b8307b32b72a684` |
| `Shaders/Wind/Assets/Editor/Scripts/LPVegetationTransparent_MaterialInspector.cs` | `ad5c407a98376814b88ddc265aecdcb8` |
| `Shaders/Wind/Assets/Materials` | `8e9cc0c089d774b4eb1f4d3e9bd72706` |
| `Shaders/Wind/Assets/Materials/LPW_Flower_A1_01.mat` | `426667a7d6a2f5b4eb289cdb68d8960e` |
| `Shaders/Wind/Assets/Materials/LPW_Grass_A1_01.mat` | `f3c124f836b5e5340a91904bbaa1bf07` |
| `Shaders/Wind/Assets/Materials/LPW_Grass_B1_01.mat` | `df92e6d0cb68a9547babf0370c8bf4e6` |
| `Shaders/Wind/Assets/Materials/LPW_Ground_02.mat` | `3d8367cece3b5b94b9fefc8fddb97cb7` |
| `Shaders/Wind/Assets/Materials/LPW_TextureAtlas_01.mat` | `48193fab71b29ba49b4f5e803f5b1d46` |
| `Shaders/Wind/Assets/Models` | `1000e6be4f1cbe14dbf326f040c44ff7` |
| `Shaders/Wind/Assets/Models/LPW_Flower_A1_H70cm_01.fbx` | `9715bf1e661b47842977d36abed88781` |
| `Shaders/Wind/Assets/Models/LPW_Grass_A1_40cm_01.fbx` | `2b44582523d7fdf48ae9a30dd4e38897` |
| `Shaders/Wind/Assets/Models/LPW_Grass_A1_50cm_01.fbx` | `02e48aea181058a47acc4c8a06b88f13` |
| `Shaders/Wind/Assets/Models/LPW_Grass_B1_40cm_01.fbx` | `737ebd30c66793f498ef51c8913789f2` |
| `Shaders/Wind/Assets/Models/LPW_Ground_A1_100x100_02.fbx` | `9900244c1a644074d9fb129310a0542b` |
| `Shaders/Wind/Assets/Models/LPW_Rock_Boulder_A1_01.fbx` | `f3a70418971d85f47a436c1493b944d8` |
| `Shaders/Wind/Assets/Models/LPW_Rock_Wall_A1_01.fbx` | `21e4e23f629a66042b0804449f9cdf4e` |
| `Shaders/Wind/Assets/Shaders` | `c215f2dc03c091b40bc48461383ba865` |
| `Shaders/Wind/Assets/Shaders/ASE` | `1a34ae99d119b8b41bb5764b12e345cd` |
| `Shaders/Wind/Assets/Shaders/ASE/LPW_Vegetation_Transparent.shader` | `0dee0a5c73cc4d746a63652331a3c92f` |
| `Shaders/Wind/Assets/Shaders/ASE/LPW_Vegetation.shader` | `62e570c62a5cc054694b49c038ef660b` |
| `Shaders/Wind/Assets/Textures` | `76647bd02fa353c4793ee05797156a62` |
| `Shaders/Wind/Assets/Textures/LPW_Grass_Flat_01.png` | `2c4e51a73d7c73d40935ba83394b9014` |
| `Shaders/Wind/Assets/Textures/LPW_NoiseMap_01.png` | `325d48ff66bae3245b768b66049c7718` |
| `Shaders/Wind/Assets/Textures/LPW_TextureAtlas_01.png` | `225da740038919f4ca9eaff413945956` |
| `Shaders/Wind/Prefabs` | `ee179d1c4df23284dbb3da9b334d2c57` |
| `Shaders/Wind/Prefabs/LPW_Flower_A1_H70cm_01.prefab` | `962996e23ffcbc44d92fb20cd03940cd` |
| `Shaders/Wind/Prefabs/LPW_Grass_A1_40cm_01.prefab` | `b995a96a7ca36d04b851e46870b5aadf` |
| `Shaders/Wind/Prefabs/LPW_Grass_A1_50cm_01.prefab` | `9c6178fe943216540b276b975dafae16` |
| `Shaders/Wind/Prefabs/LPW_Grass_B1_40cm_01.prefab` | `f37c8cfcbf935f6428718bf11e4a8de8` |
| `Shaders/Wind/Prefabs/LPW_Ground_A1_100x100_02.prefab` | `14d1ebaf454fb5d4daead1c6ae0281c4` |
| `Shaders/Wind/Prefabs/LPW_Rock_Boulder_A1_01.prefab` | `aa0ba387f794005419a74fdfff3da4df` |
| `Shaders/Wind/Prefabs/LPW_Rock_Wall_A1_01.prefab` | `25cb56d59f80f614b9da0e9a36c48a05` |

## Verify in Unity

Open the project in the recorded Unity version, wait for import and compilation, then open `Assets/_Modules_/02_Levels/Level01/Imported/Scenes/WTF.unity`. Check Console for missing scripts, missing references and shader errors; inspect vegetation materials and prefabs for missing or pink shaders. Run Level01 from the development selection screen and verify scanning, digging, all ten targets, door opening, exit and completion UI. Compare visually with the accepted local project.

A GitHub checkout alone does **not** reproduce the accepted Level01 scene. Installation, matching GUIDs, team permissions and the existing ONLYfactory siteC_floor material mapping all require confirmation; successful installation is not guaranteed solely by downloading the package.
