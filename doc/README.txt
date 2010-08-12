Simian Quick Start


Finding Help
------------

If you need any help we have a couple of resources, the primary one being 
the #simian IRC channel on EFNet. The project website hosts all relevant 
documentation and is located at:

  http://openmetaverse.googlecode.com/


Compiling on Windows
====================================================================================

Prerequisites (all freely available)
--------------------------------------
Microsoft .NET Framework 3.5 - Get directly from Windows Update.
Visual C# Express - http://msdn.microsoft.com/vstudio/express/visualcsharp/


Using Visual Studio 2010/Visual C# Express 2010
1. Open Explorer and browse to the directory you extracted the source distribution to
2. Double click the runprebuild2010.bat file, this will create the necessary solution and project files
3. Open the solution Simian.sln from within Visual Studio
4. From the Build Menu choose Build Solution

The Simian.exe executable and supporting libraries will be in either bin/Debug/ or bin/Release/ 
depending on whether the project was compiled as Debug or Release.


Compiling on Linux
====================================================================================

Prerequisites (all freely available)
--------------------------------------
Latest release of Mono (tested with 2.6.x) - http://www.mono-project.com/


Using mono xbuild:
1. Change to the directory you extracted the source distribution to
2. run the prebuild file: % sh runprebuild.sh - This will generate the solution files for xbuild
3. Compile the solution with the command: % xbuild Simian.sln

The library, example applications and tools will be in the bin/Debug/ or bin/Release/ directory
depending on whether the project was compiled as Debug or Release.


Running (all platforms)
====================================================================================

Inside the directory that contains Simian.exe you should see a Config/ directory that contains an LLRegions/
folder, a SimianDefaults.ini, possibly a Simian.ini file and any other config files needed by Simian. 
SimianDefaults.ini contains the default config options for Simian application modules and regions and should 
only be used as a reference, never modified. Make all of your application-wide changes in Simian.ini (create 
a new file next to SimianDefaults.ini called Simian.ini if that file does not already exist) and per-region 
changes in the individual config files in LLRegions/. The default Simian configuration has a number of 
example regions in LLRegions/ that you can use right away or use as examples when defining your own regions.

Once your configuration files are customized to your liking (the default configuration should run without any 
modifications) you are ready to run Simian.exe. By default Simian will run in standalone mode with a login 
server listening on port 12043. Point a Second Life(tm) compatible viewer at Simian with 
"-loginuri http://localhost:12043/" as a command line parameter to the SL viewer and log in.


Happy fiddling,
-- OpenMetaverse Ninjas 
