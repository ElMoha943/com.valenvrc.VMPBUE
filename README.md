# Val's Material Property Block Udon Editor (VMPBUE) tool.
This tool is desgined to help you asign and apply material property blocks on your vrchat scene.

### Usage

1. Drag and drop mesh renderers into the editor script to access and modify the different properties of their materials.
2. Hit Apply (or Apply to All) button to preview the result.
3. Hit Export to Udon button to create a new UdonBehaviour on your scene that will automatically asign the material property blocks upon loading the scene.

<img width="404" height="229" alt="image" src="https://github.com/user-attachments/assets/716ae5f3-d1ad-4b56-b84e-d05c5d9a7119" />

<img width="873" height="592" alt="image" src="https://github.com/user-attachments/assets/a01d384a-3a4d-4588-b78e-82e1fc39b6fc" />

### Why use material property blocks instead of different materials?

Material Property Blocks (MPBs) let you override material properties per renderer without touching the shared material asset. Compared to changing material properties directly or creating multiple materials, they provide clear advantages:

- No material instancing: Changing a material at runtime creates a unique instance, increasing memory usage. MPBs avoid this entirely.
- Better batching & GPU instancing: Objects can still share the same material and be batched or GPU-instanced, improving draw-call performance.
- Lower memory footprint: One material, many visual variations (colors, floats, textures) without duplicating materials.
- Safe runtime changes: Modifying materials directly can unintentionally affect all objects using that material; MPBs are isolated per renderer.
