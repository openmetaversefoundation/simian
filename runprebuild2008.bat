@echo off
::
:: Prebuild generator for Simian
::
:: Command Line Options:
:: (none)            - create solution/project files and create compile.bat file to build solution
:: msbuild           - Create project files, compile solution
:: msbuild runtests  - create project files, compile solution, run unit tests
:: msbuild docs      - create project files, compile solution, build API documentation
:: msbuild docs dist - Create project files, compile solution, run unit tests, build api documentation, create binary zip
::                   - and exe installer
::

echo ##########################################
echo creating prebuild files for: vs2008
echo Parameters: %1 %2
echo ##########################################

:: run prebuild to generate solution/project files from prebuild.xml configuration file
Prebuild.exe /target vs2008

:: build compile.bat file based on command line parameters
echo @echo off > compile.bat
if(.%1)==(.) echo C:\WINDOWS\Microsoft.NET\Framework\v3.5\msbuild Simian.sln >> compile.bat

if(.%1)==(.msbuild) echo echo ==== COMPILE BEGIN ==== >> compile.bat
if(.%1)==(.msbuild) echo %SystemRoot%\Microsoft.NET\Framework\v3.5\MSBuild.exe /p:Configuration=Release Simian.sln >> compile.bat
if(.%1)==(.msbuild) echo IF ERRORLEVEL 1 GOTO FAIL >> compile.bat

if(.%1)==(.nant) echo nant >> compile.bat
if(.%1)==(.nant) echo IF ERRORLEVEL 1 GOTO FAIL >> compile.bat

if(.%2)==(.docs) echo echo ==== GENERATE DOCUMENTATION BEGIN ==== >> compile.bat
if(.%2)==(.docs) echo %SystemRoot%\Microsoft.NET\Framework\v3.5\MSBuild.exe /p:Configuration=Release docs\Simian.shfbproj >> compile.bat
if(.%2)==(.docs) echo IF ERRORLEVEL 1 GOTO FAIL >> compile.bat
if(.%2)==(.docs) echo 7z.exe a -tzip docs\documentation.zip docs\trunk >> compile.bat
if(.%2)==(.docs) echo IF ERRORLEVEL 1 GOTO FAIL >> compile.bat

if(.%2)==(.runtests) echo echo ==== UNIT TESTS BEGIN ==== >> compile.bat
if(.%2)==(.runtests) echo nunit-console bin\Tests.Simian.dll /exclude:Network /nodots /labels /xml:testresults.xml >> compile.bat

if(.%2)==(.runtests) echo IF ERRORLEVEL 1 GOTO FAIL >> compile.bat

:: nsis compiler needs to be in path
if(.%3)==(.dist) echo echo ==== GENERATE DISTRIBUTION BEGIN ==== >> compile.bat
if(.%3)==(.dist) echo makensis.exe /DPlatform=test docs\Simian-installer.nsi >> compile.bat
if(.%3)==(.dist) echo IF ERRORLEVEL 1 GOTO FAIL >> compile.bat
if(.%3)==(.dist) echo 7z.exe a -tzip dist\simian-dist.zip @docs\distfiles.lst >> compile.bat
if(.%3)==(.dist) echo IF ERRORLEVEL 1 GOTO FAIL >> compile.bat

echo :SUCCESS >> compile.bat
echo echo Build Successful! >> compile.bat
echo exit /B 0 >> compile.bat
echo :FAIL >> compile.bat
echo echo Build Failed, check log for reason >> compile.bat
echo exit /B 1 >> compile.bat

:: perform the appropriate action
if(.%1)==(.msbuild) compile.bat
if(.%1)==(.nant) compile.bat
if(.%1)==(.dist) compile.bat

