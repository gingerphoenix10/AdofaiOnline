SETLOCAL ENABLEDELAYEDEXPANSION
set /p version=<VERSION.txt
mkdir tmp
cd tmp
mkdir AdofaiOnline
copy "..\Info.json" ".\AdofaiOnline"
copy "..\AdofaiOnline\bin\UMM\net48\AdofaiOnline.dll" ".\AdofaiOnline"

cd AdofaiOnline
for /f "delims=" %%a in (Info.json) do (
    SET s=%%a
    SET s=!s:$VERSION=%version%!
    echo !s!
)>>"InfoChanged.json"
del Info.json
rename InfoChanged.json Info.json
cd ..

tar -a -c -f AdofaiOnline-%version%.zip AdofaiOnline
move AdofaiOnline-%version%.zip ..
cd ..
rmdir /S /Q tmp
pause