# DOTS JetSki
With this project I am exploring how DOTS can be leveraged to enable highly detailed procedurally generated meshes.
Through the use of Jobs and the Entity Component System I plan to explore the composition of:
 - Highly detailed wave simulation with Jobs
   - Detailed buoyancy physics with Jobs
   - Splash effects using ECS
 - Procedural land generation
   - Islands
   - Bluffs
   - Canyons
 - Waterfall effects
 - Aquatic wildlife and behaviors using ECS

# Dev Log 1 | 10/25/2020
![JetSki First Image](https://raw.githubusercontent.com/JSchoppe/DOTS-JetSki/master/ReadMeImages/jetski-0.1.jpg)
## Commits
 - [adds mock jetski assets](https://github.com/JSchoppe/DOTS-JetSki/commit/fecc7104d677bb628f10a7c5bcbfadc0bd2c938b)
 - [implements jetski scene with water simulation base](https://github.com/JSchoppe/DOTS-JetSki/commit/0d04b86a7d5273e1320e342c06c5da1eff357f82)
## Overview
I am keeping the other DOTS examples loaded for now as reference. Everything I work on will be in the JetskiRiff folder.
In this first batch of commits I explored the use the Jobs system to handle updating a large amount of vertices in a fluid body.
This fluid body follows the camera which means that it can be a fixed size of geometry (excellent for parallelization).
In a future iteration I would like to explore mesh generation algorithms that place more detail near the camera. My first intuition
would be to explore tiling strategies for triangles instead of quads. <br>
<img src="https://raw.githubusercontent.com/JSchoppe/DOTS-JetSki/master/ReadMeImages/quads-generation.jpg" width="45%">
<img src="https://raw.githubusercontent.com/JSchoppe/DOTS-JetSki/master/ReadMeImages/tris-lod-generation.jpg" width="45%">
