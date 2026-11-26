@echo off
rem Publishes the game: uploads everything committed so far, plus any edit to the word list or math settings.
cd /d "%~dp0"
git add world/StreamingAssets
git commit -m "words and settings update" >nul 2>&1
git push
echo.
echo Done. The live game updates in a minute or two: klinares.github.io/hall_pass/world/
pause
