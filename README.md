# Unity Portals

> A seamless portal implementation inspired by Valve's Portal and Demruth's Antichamber

## NOTE: Please do not use this in commercial products. This is a learning project and there are still bugs.

This project is a portal implementation written for Unity. It uses multiple cameras, RenderTextures, and volumetric geometry to create a seamless transition from one point in space to another. Currently, it only supports forward rendering on the standard render pipeline. It is currently built with Unity v2019.3.4f1, and has not been tested on other versions.

[You can try the WebGL demo here.](https://codand.github.io/PortalDemo/index.html) Please note that this runs much slower on the web because Unity does not support multithreaded rendering for WebGL. Tested on Windows 10 with a GTX 1060 graphics card.
## Features
---

### Seamless transitions

Portals are rendered as an inverted cube with the front face flipped in order to avoid near-plane clipping.

![Recusive Rendering](https://thumbs.gfycat.com/InsidiousFarBoto-size_restricted.gif)

---

### Gravity modification

Users can specify per-portal if gravity should be realligned.

![Gravity reallignement](https://thumbs.gfycat.com/DelectableFarFrogmouth-small.gif)

---

### Recursive rendering

Portals can render recursively to a user-defined maximum depth. Portals beyond the maximum depth can fake recursion by reprojecting previous frames. This example is rendered with only two recursions.

![Recusive Rendering](https://thumbs.gfycat.com/OnlyCanineGemsbok-small.gif)

---

### Scale modification

Portals will scale and shrink objects and players seamlessly.

![Scale adjustment](https://thumbs.gfycat.com/FrenchEqualGourami-small.gif)

---

### Image effects

Portals are rendered into textures which enables image effects on portals.

![Image effects](https://thumbs.gfycat.com/EnchantingAbleCutworm-small.gif)

---

### Per-object planar clipping

Objects entering a portal are not rendered behind the portal.

![Per-object planar clipping](https://thumbs.gfycat.com/HatefulBarrenAustraliankestrel-size_restricted.gif)

---

### Physics

Every object in a portal has a clone object on the other side which can share forces with the other. This works well enough to be believable most of the time, but it does not behave the same as normal physics.

![Portal scale adjustment](https://thumbs.gfycat.com/CalculatingNegativeCob-size_restricted.gif)

---

### Efficient rendering

Portals only render visible pixels to improve performance

![Portal scale adjustment](https://thumbs.gfycat.com/SomberShallowDassierat-small.gif)

---

### Transparency masks

Portal transparency can me masked with a texture

---

## Missing features

* VR Support. It's actually already 90% of the way there for VR support, I just don't have a VR headset to test it!
* Deferred rendering support
* Depth buffer and motion vector reconstruction
* URP and HDRP support
* Occlusion culling support. Not currently fixable due to https://issuetracker.unity3d.com/issues/setting-the-cameras-culling-matrix-breaks-culling-for-oblique-matrices. Portals currently employ a raycasting based solution to avoid rendering portals behind walls, but it is not perfect, and relies on all objects having accurate colliders.
* Scene-to-scene teleportation
* Real time light support. I have explored this, but haven't yet been able to come up with a solution that handles shadows gracefully without requiring scenes to use custom shaders.
* Baked lighting support. Haven't explored this - I doubt Unity will allow custom rendering during the baking process.

## Known bugs

* Player can sometimes fall outside of the level. Have not yet diagnosed the cause, but I suspect it is caused by the clone object.
* Anti-aliasing produces visible seams at the edges of portals due to the spatial discontinuity. This seam can be hidden if using a transparency mask, but I haven't yet thought of a way to mitigate it.
* Projection matrix is sometimes (very rarely) invalid which causes an assertion failure, but does not cause any rendering artifacts. I suspect this has to do with sub-single-pixel width portals.
