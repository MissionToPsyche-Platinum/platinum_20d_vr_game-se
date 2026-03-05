TG-22: Free Space & NASA 3D Model Research
10 key assets — all free, all usable for the Psyche VR Game

PSYCHE MISSION — SPACECRAFT & ASTEROID
Psyche Spacecraft (5-Panel Solar Array)

Link: https://nasa3d.arc.nasa.gov/detail/psyche-5-panel
Source: NASA 3D Resources
Format: GLB / USDZ
License: Public Domain (credit NASA)
What it is: Official Maxar Technologies model of the Psyche orbiter with 5-panel solar arrays. This is the actual mission spacecraft geometry used by NASA.
How to implement: Import GLB into Unity via glTFast plugin (free on Asset Store). Use as the centerpiece spacecraft model the player assembles in the bedroom puzzle. Reduce textures to 1024x1024 for Quest performance.

Psyche Mission Models & Materials Hub

Link: https://psyche.ssl.berkeley.edu/get-involved/models-and-materials/
Source: Official Psyche Mission Site
Format: Various (STL, PDF, images)
License: Public Domain (NASA)
What it is: Central hub for all official Psyche assets: 3D models, posters, educational materials, Psyche Inspired student artworks. Dr. Bowman is directly connected to this program.
How to implement: Bookmark as your one-stop reference. Use posters/images as in-game textures for the display board prop (D6.1). Educational text feeds into D2.1. Great for sponsor alignment since Dr. Bowman is involved with this program.

Psyche Spacecraft (3-Part Buildable)

Link: https://www.thingiverse.com/thing:2373526
Source: Thingiverse (Psyche Inspired student)
Format: STL → Blender → FBX
License: Public Domain (NASA/JPL-Caltech/ASU)
What it is: 3 separate parts: spacecraft body + 2 solar panels. Scale model, buildable.
How to implement: STL → import into Blender → export FBX for Unity. The 3-part breakdown is perfect for the spacecraft puzzle mechanic — player grabs and assembles each piece. Nawang can handle the Blender conversion.

16 Psyche Asteroid Concept Model

Link: https://www.thingiverse.com/thing:2373526
Source: Thingiverse (by Mission PI & Peter Rubin)
Format: STL → Blender → FBX
License: Public Domain
What it is: Created by the actual Psyche Mission Principal Investigator using Arecibo radar data. Includes surface faults and craters based on best scientific models.
How to implement: STL → Blender → FBX. Use as a prop in mission control (hologram or monitor display) and in the telescope interaction — player looks through telescope and sees this asteroid. Nawang can add metallic textures in Blender.


NASA GENERAL — MODELS, IMAGES & TEXTURES
Psyche Mission Image Gallery

Link: https://psyche.ssl.berkeley.edu/galleries/
Source: Psyche Mission Site
Format: JPG / PNG
License: Public Domain (NASA/JPL-Caltech/ASU)
What it is: Mission-specific images: spacecraft renders, asteroid visualizations, instrument diagrams, infographics, launch photos, Psyche Inspired student art.
How to implement: Primary source for ALL in-game educational textures. Use instrument diagrams for the instruction manual prop, infographics for the display board, spacecraft renders for modeling reference. Directly feeds D2.1 and D6.1.

NASA 3D Resources (GitHub Repo)

Link: https://github.com/nasa/NASA-3D-Resources
Source: GitHub (nasa/NASA-3D-Resources)
Format: GLB, OBJ, STL (varies)
License: Public Domain
What it is: 551 commits. Includes spacecraft, satellites, instruments, spacesuits, tools, plus an Images and Textures folder. Everything free.
How to implement: Clone the repo. Grab satellite dishes, solar panels, and instrument models for mission control props. Use Images/Textures folder for in-game posters, monitor displays, and educational overlays. Feeds D2.1, D6.1, and D7.1.

NASA Image Gallery

Link: https://www.nasa.gov/images/
Source: NASA
Format: JPG / PNG
License: Public Domain (credit NASA)
What it is: Thousands of high-res photos: mission control rooms, spacecraft, launches, crew. Richard already sourced the NASA logo from here.
How to implement: Use as textures for posters, monitor displays, display board content. Grab real JPL control room photos as reference for building the mission control scene. Apply as materials on quad meshes for wall decorations.


BEDROOM SCENE — FURNITURE
Furniture FREE - Low Poly 3D Models Pack

Link: https://assetstore.unity.com/packages/3d/props/furniture/furniture-free-low-poly-3d-models-pack-260522
Source: Unity Asset Store (ithappy)
Format: .unitypackage (native Unity)
License: Unity Asset Store EULA
What it is: 40 unique low-poly furniture models. ~275 triangles per model. Real-world scale. Collision included. Compatible with Built-in/URP/HDRP. Updated for Unity 6. 1,293 favorites.
How to implement: Best starting point for bedroom. Import via Package Manager — zero conversion. Drag-and-drop prefabs for bed, desk, chair, bookshelf, lamp. The ultra-low poly count is perfect for Quest's 72 FPS target. Combine with space-themed textures for the kid's room vibe.


INTERACTIVE PROPS — TELESCOPE
Antique Telescope

Link: https://sketchfab.com/3d-models/antique-telescope-391f41447c00421d9cbc5445c0d535b6
Source: Sketchfab (radioape)
Format: GLTF / FBX / OBJ
License: Free download (check Sketchfab license)
What it is: Low-poly game-ready antique telescope. Clean design, simple geometry.
How to implement: Download FBX → Unity import. Add XR Grab Interactable for VR interaction. When player looks through it, trigger a UI overlay showing the Psyche asteroid and spacecraft location data (D6.1). Antique style fits a kid's bedroom perfectly.