using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BlenderRenderGuard
{
    internal class Program
    {
        static void Main(string[] args)
        {                           
            var skipUserInput = args.Length > 0 && args.Contains("-s"); // Skip asking for user settings, and just use whatever is stored in the settings file (or use default config, if no file exists)
            var putPcToSleep = args.Length > 0 && args.Contains("-z"); // Put computer to sleep after the rendering is finished

            var settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            var settings = new Settings()
            {
                BlenderExePath = @"C:\Program Files\Blender Foundation\Blender 3.6\blender.exe",
                BlendFilePath = @"C:\project.blend",
                RenderDirectoryPath = @"C:\",
                EndFrame = 250,                
                FileExtension = "png",
                TimeLimit = 120
            };

            if(File.Exists(settingsFilePath)) // If user settings exists, load it and overwrite the defaults                           
                settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsFilePath));                                    

            Console.WriteLine("=== BLENDER RENDER GUARD ===");
            Console.WriteLine("Type in the requested data and press Enter, or leave it empty and press enter to use the default option in the parentheses. " +
                "If blender isn't running it will be started automatically, so you only need to launch this app. " +
                "Please note that this app will run Blender in command line mode, there will be no GUI, this is normal." +
                "Do not run the guard, unless you are rendering, or it will keep killing your blender instance. If you want to put the computer to sleep after the rendering finished, run this .exe with a -z argument");
            Console.WriteLine();

            if (!skipUserInput)
            {
                Console.WriteLine($"Location of Blender executable: ({settings.BlenderExePath})");
                var blenderExePath = Console.ReadLine();
                settings.BlenderExePath = string.IsNullOrWhiteSpace(blenderExePath) ? settings.BlenderExePath : blenderExePath;

                Console.WriteLine($"Location of project .blend file: ({settings.BlendFilePath})");
                var blendFilePath = Console.ReadLine();
                settings.BlendFilePath = string.IsNullOrWhiteSpace(blendFilePath) ? settings.BlendFilePath : blendFilePath;

                Console.WriteLine($"Enter directory path where blender outputs rendered frames: ({settings.RenderDirectoryPath})");
                var directoryPath = Console.ReadLine();
                settings.RenderDirectoryPath = string.IsNullOrWhiteSpace(directoryPath) ? settings.RenderDirectoryPath : directoryPath;

                Console.WriteLine($"Enter extension of the watched files: ({settings.FileExtension})");
                var fileExtension = Console.ReadLine();
                settings.FileExtension = string.IsNullOrWhiteSpace(fileExtension) ? settings.FileExtension : fileExtension;


                Console.WriteLine($"Restart blender if new image file wasn't created in this many seconds, since the last one: ({settings.TimeLimit})");

                if (int.TryParse(Console.ReadLine(), out int checkTimeLimit))
                    settings.TimeLimit = checkTimeLimit;


                Console.WriteLine($"End frame of the animation: {settings.EndFrame}");

                if (int.TryParse(Console.ReadLine(), out int lastFrame))
                    settings.EndFrame = lastFrame;


                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(settingsFilePath, json);
            }
            else            
                Console.WriteLine("-s flag was used, therefore skipping user input, if you want change the settings, then run this app again without the -s argument.");
            

            // ** End of user settings inputting **


            if (!Directory.Exists(settings.RenderDirectoryPath))
            {
                Console.WriteLine("Error: Directory doesn't exist.");
                Console.ReadLine();
                return;
            }


            Console.WriteLine("*All good, watchdog is running*");
            Console.WriteLine();

            var totalBlenderReboots = 0;

            while (true)
            {                
                Thread.Sleep((settings.TimeLimit * 1000));
                //Thread.Sleep(1000);
                Console.Write(".");
                string[] files = Directory.GetFiles(settings.RenderDirectoryPath, $"*.{settings.FileExtension}");

                if (files.Length == 0)
                {
                    Console.WriteLine("No image files were found in the directory.");                    
                    continue;
                }

                var highestFrameNumber = files.Select(Path.GetFileNameWithoutExtension) // Get the file name which has the highest frame number. We could do this by simply sorting by file creation date, but that could cause problems in case we manually rerender some earlier frames etc
                                .Where(fileName => int.TryParse(fileName, out _))
                                .OrderByDescending(fileName => int.Parse(fileName))
                                .ToArray().FirstOrDefault();

                if (highestFrameNumber == null) // If this is null then no files had names that could be parsed as integers, the files should be named like 0005.png for example.
                {
                    Console.WriteLine("There was a problem parsing the image file names into numbers, they need to be named something like 0012.png");
                    continue;
                }

                if(int.Parse(highestFrameNumber) == settings.EndFrame)
                {
                    Console.WriteLine($"***Rendering of all frames was successfully finished, YAY!***");
                    Console.WriteLine($"Total blender restarts: {totalBlenderReboots}");
                    Console.WriteLine($"Guard exiting, {DateTime.Now.ToString("G")}");

                    if(putPcToSleep)                    
                        PutPcToSleep();
                    
                    break;
                }

                var latestFrameFilePath = Path.Combine(settings.RenderDirectoryPath, $"{highestFrameNumber}.{settings.FileExtension}");
                var frameFileInfo = new FileInfo(latestFrameFilePath);                

                if ((DateTime.Now - frameFileInfo.CreationTime).TotalSeconds <= settings.TimeLimit)                                    
                    continue;

                Console.WriteLine();
                Console.WriteLine($"{DateTime.Now.ToString("G")}: Last frame {highestFrameNumber} is {string.Format("{0:N0}", Math.Round((DateTime.Now - frameFileInfo.CreationTime).TotalSeconds))} seconds old");

                Console.WriteLine("***TIME LIMIT PASSED, RENDERING IS LIKELY STUCK***");
                Console.WriteLine($"Executing blender restart number {(totalBlenderReboots + 1)}");

                if(KillBlender())
                    Thread.Sleep(6000); // Give time for blender to exit

                StartBlender(settings.BlenderExePath, settings.BlendFilePath, int.Parse(highestFrameNumber) + 1, settings.EndFrame);   
                Thread.Sleep(10000); // Give time to blender to start
                totalBlenderReboots++;
            }

        }

        /// <summary>
        /// Used to store the user preferences so they doesn't have to fill them in every time they runs this app
        /// </summary>
        private class Settings
        {
            public string BlenderExePath { get; set; }
            public string BlendFilePath { get; set; }
            public string RenderDirectoryPath { get; set; }
            public string FileExtension { get; set; }
            public int EndFrame { get; set; }
            public int TimeLimit { get; set; }

        }

        /// <summary>
        /// Kill the currently open blender instance
        /// </summary>
        /// <returns>true if blender was killed, false no blender proccess was found</returns>
        static bool KillBlender()
        {
            // Specify the name of the Blender process without the file extension
            string processName = "Blender"; 

            // Find the Blender process by name
            Process[] blenderProcesses = Process.GetProcessesByName(processName);

            if (blenderProcesses.Length > 0)
            {
                // Assuming you want to terminate the first found instance
                Process blenderProcess = blenderProcesses[0];
                blenderProcess.Kill();

                Console.WriteLine("Blender process terminated.");
                return true;
            }
            
            Console.WriteLine("No running Blender process found.");
            return false;
            
        }

        /// <summary>
        /// Start animation rendering in the command line blender interface and render the specified frame range
        /// </summary>
        static void StartBlender(string blenderExePath, string blendFilePath, int startFrame, int endFrame)
        {
            // Command-line arguments for Blender
            string arguments = $"-b \"{blendFilePath}\" -s {startFrame} -e {endFrame} -a";


            Console.WriteLine("Spinning up blender...");

            // Start the Blender process
            Process blenderProcess = new Process();
            blenderProcess.StartInfo.FileName = blenderExePath;
            blenderProcess.StartInfo.Arguments = arguments;
            blenderProcess.StartInfo.UseShellExecute = true; // Run Blender in separate window
            blenderProcess.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            blenderProcess.Start();

            Console.WriteLine("Done. Blender should now be running as command line process in another window.");
            
            // blenderProcess.WaitForExit();


        }

        [DllImport("powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
        static void PutPcToSleep()
        {
            Console.WriteLine("Putting comptuer to sleep...");
            SetSuspendState(false, false, false);            
        }

    }
}