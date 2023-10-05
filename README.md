**Bledner render guard**

Blender render guard  is a simple, light weight commandline application _(don't worry, it's super simple to use)_, which will watch the target directory and if a new frame of animation wasn't generated for the specified amount of time, it will restart Blender and resume the
animation rendering from the last existing frame, based on the already rendered images in the target directory. So for example if the image with the higheast frame number is 0598.png, the rendering will resume at frame 599.

**How to use**

- Download the .zip file
- Extract it somewhere
- Run the .exe
- Type in the requested information
- Done

Blender render guard will remember the settings you typed in, so when you run it next time, you don't have to fill it in again.

**Special options**

You can use these arguments to enable special functionality (simply place them after the .exe, for example "blenderRenderGuard.exe -z -s"

-s - Don't ask the user for any information, simply load whatever was used last time and start the guard. <br>
-z - Put the computer to sleep after rendering finishes

**Screenshot**

![image](https://github.com/mike-d3v/BlenderRenderGuard/assets/106062742/332e7d4a-d1b8-40cd-83ec-1a23c94718ef)

