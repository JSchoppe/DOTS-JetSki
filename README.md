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

## Development Logs
<details>
<summary>Beaches and Fish | 11/1/2020<br><img src="https://github.com/JSchoppe/DOTS-JetSki/blob/master/ReadMeImages/fish-explosion.gif?raw=true" width="45%"></summary>

### Notable Commits
 - [abstracts ECS from rock spire, fixes render bounds generation](https://github.com/JSchoppe/DOTS-JetSki/commit/95cfd84daf93374e908e4b16692822355cac87fa)
 - [abstracts plane mesh generation into helper class](https://github.com/JSchoppe/DOTS-JetSki/commit/5514c7fc1ebb37a550a5ab538fe77effd69131f8)
 - [implements basic fish component system](https://github.com/JSchoppe/DOTS-JetSki/commit/54fdfa4e51c3442091dbdf31ac1a67605e745c36)
 
### Overview
In this update better OOP was implemented to seperate the water simulation from general noise landscapes. This is reflected with ground layer in the scene.
The new abstracted class uses octaves to further define the noise generation. Plane generation has also been asbtracted into a helper class. The second
addition in this update is a rough implementation of a fish behavior system. Initially I wanted it to communicate directly with the water elevation but I
think this will require me rethink how the fluid object interfaces with the fish system. The added fish behavior uses some random parameters to define
target directions per each fish.

### TODO
Some things I would like to address now:
 - Procedural landscapes do not reflect fine perlin noise well (snap the following grid to the nearest grid unit)
 - Determine how the fish can effectively poll the water level from within the system
 - Add behavior for fish to jump out of the water every so often
 - Add torus generation and deformation to create rock arches in the scene<br>

Some things I would like to address but might defer for now:
 - Instantiate the procedural meshes during runtime (load and unload them based on coordinates)
 - Have fish targeting follow the player explicitly (stay within the range of the player)
 - Have fish targeting acknowledge elevation

</details>
<details>
<summary>Waves and Spires | 10/25/2020<br><img src="https://github.com/JSchoppe/DOTS-JetSki/blob/master/ReadMeImages/hello-spires.gif?raw=true" width="45%"></summary>

### Notable Commits
 - [adds mock jetski assets](https://github.com/JSchoppe/DOTS-JetSki/commit/fecc7104d677bb628f10a7c5bcbfadc0bd2c938b)
 - [implements jetski scene with water simulation base](https://github.com/JSchoppe/DOTS-JetSki/commit/0d04b86a7d5273e1320e342c06c5da1eff357f82)
 - [adds uv scrolling to fluid using jobs](https://github.com/JSchoppe/DOTS-JetSki/commit/6b2eb5e903b38e3e6f620fe10302bbd8a314bdce)
 - [adds transform observer for OnValidate](https://github.com/JSchoppe/DOTS-JetSki/commit/d2498dfafc4983d1b4c5a57d0db262a7c57a0a67)
 - [adds mock assets for rock spire](https://github.com/JSchoppe/DOTS-JetSki/commit/1d9b2d01ea0bd01001551466e904821c8b3b543a)
 - [adds rock spire generation](https://github.com/JSchoppe/DOTS-JetSki/commit/ad3121f923c6e8cb74b9dfc4c77c35a5cee7dfda)
 
### Overview
I am keeping the other DOTS examples loaded for now as reference. Everything I work on will be in the JetskiRiff folder. <br>
In this first batch of commits I explored the use the Jobs system to handle updating a large amount of vertices in a fluid body.
This fluid body follows the camera which means that it can be a fixed size of geometry (excellent for parallelization).
In a future iteration I would like to explore mesh generation algorithms that place more detail near the camera. My first intuition
would be to explore tiling strategies for triangles instead of quads. Something like this: <br>
<img src="https://raw.githubusercontent.com/JSchoppe/DOTS-JetSki/master/ReadMeImages/tris-lod-generation.jpg" width="45%"> <br>
In addition to the fluid body I also implemented rock spire generation that renders using the hybrid renderer. These are procedurally
generated and seeded based on their location in the scene. Some tools were made to ensure that the generation could be previewed in
the scene. <br>
<img src="https://github.com/JSchoppe/DOTS-JetSki/blob/master/ReadMeImages/spire-scene-editor.jpg?raw=true" width="45%">

### TODO
There are some bugs with the ECS rendering (I suspect I need to manually calculate the render bounds since I am procedurally generating geometry.
There is some testing that should be done to see if in some placing using float3 instead of Vector3 saves some performance, I also hypothesize
that the job logic could be further optimized so that it can be even more parallel.<br>
My next main focus will be land generation and perhaps a system to simulate a school of fish below the surface of the water.

</details>
