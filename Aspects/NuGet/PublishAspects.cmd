pushd
NuGet Update -self
call "%VS140COMNTOOLS%vsvars32.bat"
if not .%1.==.. NuGet SetApiKey %1
cd %~dp0..
msbuild vm.Aspects.csproj /t:Rebuild /p:Configuration=Release /m
cd Model
msbuild vm.Aspects.Model.csproj /t:Rebuild /p:Configuration=Release /m
cd ..\Parsers
msbuild vm.Aspects.Parsers.csproj /t:Rebuild /p:Configuration=Release /m
cd ..\Wcf
msbuild vm.Aspects.Wcf.csproj /t:Rebuild /p:Configuration=Release /m
if errorlevel 1 goto exit
cd ..
NuGet Pack NuGet\vm.Aspects.nuspec -Prop Configuration=Release
if errorlevel 1 goto exit
@echo Press any key to push to NuGet... > con:
@pause > nul:
NuGet Push *.nupkg
:exit
del *.nupkg
popd
pause