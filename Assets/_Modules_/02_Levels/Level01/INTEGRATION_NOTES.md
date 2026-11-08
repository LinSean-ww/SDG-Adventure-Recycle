# Level01 integration status

The imported scene and its dependencies are isolated under `Imported`. The original
source project remains unchanged. The HUD uses the shared OFL-licensed Noto Sans CJK TC font through a Level01 dynamic TMP asset.
The unused original MSJH font and its derived atlas remain local and are excluded from Git.
The scene still requires the local Nicrom Low Poly Wind 1.4.0 package (Asset Store ID 182586);
its files are excluded from Git until the team confirms the applicable licenses and seats.
A checkout without these assets cannot reproduce the accepted scene.

`ONLYfactory.fbx` retains its original unresolved `siteC_floor` external material
mapping (GUID `7c8c4814fdafb95408320b72e4324330`). The scene renderer check found no
empty material or shader slots, but the original mapping needs manual visual review.

The original power-door sequence is retained. Completion is reported only after all
ten targets are collected, the power door finishes opening, and the player crosses
the Level01 Exit Trigger from the entry side. Level 2 is not yet available.
